/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *
 */
 
 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hybrasyl.Config;
using log4net;
using MoonSharp.Interpreter;

namespace Hybrasyl
{
    /// <summary>
    /// A class for representing time in Hybrasyl. There are generally two types of time - "current" time, e.g. time since server start,
    /// and "historical" time - anything before that. We use DateTime to represent both, meaning a Hybrasyl age has an upper limit of  
    /// 16,144 years in the past (that is to say, the start of the age cannot be before DateTime.MinValue).
    /// 
    /// In the absence of age definitions in config.xml we use a default age "Hybrasyl", whose first year (Hybrasyl 1) is equivalent to
    /// the server start time.
    /// </summary>
    [MoonSharpUserData]
    public class HybrasylTime
    {
        public static ILog Logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string AgeName => Age.Name;
        public HybrasylAge Age => Game.Config.Time.GetAgeFromTerranDatetime(TerranDateTime);

        public long HybrasylTicks => TerranTicks * 8;
        public long TerranTicks => (TerranDateTime - Age.StartDate).Ticks;
        public long YearTicks => (HybrasylTicks / TicksPerYear) * TicksPerYear;
        public long MoonTicks => ((HybrasylTicks - YearTicks) / TicksPerMoon) * TicksPerMoon;
        public long SunTicks => ((HybrasylTicks - YearTicks - MoonTicks) / TicksPerSun) * TicksPerSun;
        public long HourTicks => ((HybrasylTicks - YearTicks - MoonTicks - SunTicks) / TicksPerHour) * TicksPerHour;
        public long MinuteTicks => ((HybrasylTicks - YearTicks - MoonTicks - SunTicks - HourTicks) / TicksPerMinute) * TicksPerMinute;
 
        // Yes, overflows are possible here; instead of complaining, ask yourself why you 
        // need an in-game year that is > maxint
        public int Year => Convert.ToInt32(HybrasylTicks / TicksPerYear) + Age.StartYear;

        public int Moon => Convert.ToInt32(MoonTicks / TicksPerMoon);

        public int Sun => Convert.ToInt32(SunTicks / TicksPerSun);
        public int Hour => Convert.ToInt32(HourTicks / TicksPerHour);
        public int Minute => Convert.ToInt32(MinuteTicks / TicksPerMinute);

        // The TerranDateTime is always the basis for everything else
        public DateTime TerranDateTime;

        public string Season
        {
            get
            {
                switch (Moon)
               {
                    case 12:
                    case 1:
                    case 2:
                       return "Winter";
                    case 3:
                    case 4:
                    case 5:
                        return "Spring";
                    case 6:
                    case 7:
                    case 8:
                        return "Summer";
                    case 9:
                    case 10:
                    case 11:
                        return "Fall";
                    default:
                       return string.Empty;
               }
            }
        }

        public const long TicksPerYear = 12 * TicksPerMoon;
        public const long TicksPerMoon = 28 * TicksPerSun;
        public const long TicksPerSun = 24 * TicksPerHour;
        public const long TicksPerHour = 60 * TicksPerMinute;
        public const long TicksPerMinute = 60 * TimeSpan.TicksPerSecond;
        // 378.80
        public static readonly List<string> RegexstringList = new List<string>
        {
            @"(?<Age>[A-Za-z _]*) (?<Year>\d*)(\s*,\s*(?<Moon>\d*)\s*(rd|st|nd|th) moon,\s*(?<Sun>\d*)\s*(rd|st|nd|th) sun,\s*(?<Hour>\d{0,2}):(?<Minute>\d{0,2})\s*(?<TimeMeridian>am|pm|a.m.|p.m.)){0,1}",
            @"(?<Age>[A-Za-z _]*)\s*(?<Year>\d*)(\s*,\s*Moon\s*(?<Moon>\d*),\s*Sun\s*(?<Sun>\d*)\s*(?<Hour>\d{0,2}):(?<Minute>\d{0,2})\s*(?<TimeMeridian>am|pm|a.m.|p.m.)){0,1}"
        };

        public static List<Regex> RegexList = new List<Regex>();

        public static string DefaultAgeName => Game.Config != null ? Game.Config.Time?.ServerStart?.DefaultAge != string.Empty ? Game.Config.Time?.ServerStart?.DefaultAge : "Hybrasyl" : "Hybrasyl";
        public static int DefaultYear => Game.Config != null ? Game.Config.Time?.ServerStart?.DefaultYear != 1 ? Game.Config.Time.ServerStart.DefaultYear : 1 : 1;

        /// <summary>
        /// Default age is "Hybrasyl", year 1 is either the recorded ServerStart time in config.xml, or, 
        /// if we can't find that, the current running server's start time
        /// </summary>
        public static HybrasylAge DefaultAge => new HybrasylAge() { Name = "Hybrasyl", StartYear = 1, StartDate = Game.Config.Time?.ServerStart?.Value ?? Game.StartDate };

        public static HybrasylAge GetAgeFromTerranDate(DateTime datetime)
        {
            if (Game.Config.Time.Ages.Count == 0)
                return DefaultAge;
            var currentAge = Game.Config.Time.Ages?.Where(age => age.DateInAge(datetime));
            return currentAge.Count() == 0 ? DefaultAge : currentAge.First();
        }

        public static bool ValidAge(string age)
        {
            return (Game.Config.Time?.Ages?.Where(a => a.Name == age).Count() > 0) || DefaultAgeName == age;
        }

        public override string ToString()
        {
            var hour = Hour;
            string ampm;
            if (hour > 12)
            {
                hour -= 12;
                ampm = "p.m.";
            }
            else
            {
                ampm = "a.m.";
            }

            return
                $"{AgeName} {Year}, {Moon.DisplayWithOrdinal()} moon, {Sun.DisplayWithOrdinal()} sun, {hour}:{Minute.ToString("d2")} {ampm}";
        }

        public static string CurrentAgeName => CurrentAge.Name;

        public static HybrasylAge CurrentAge
        {
            get
            {
                var now = DateTime.Now;
                if (Game.Config.Time.Ages.Count == 0)
                    return DefaultAge;
                var currentAge = Game.Config.Time.Ages?.Where(age => age.DateInAge(now));
                return currentAge.Count() == 0 ? DefaultAge : currentAge.First();
            }
        }


        public static List<HybrasylAge> Ages()
        { 
            if (Game.Config.Time.Ages.Count == 0)
            {
                // Construct and return our default
                return new List<HybrasylAge> { new HybrasylAge() { Name = "Hybrasyl", StartYear = 1, StartDate = Game.Config.Time.ServerStart.Value } };
            }
            return Game.Config.Time.Ages;
        }

        public static int CurrentYear => Now.Year;

        public static int FirstYearInAge(string age)
        {
            if (!ValidAge(age))
                throw new ArgumentException("Age is unknown to server; check time/age configuration in config.xml", age);

            var theAge = Game.Config.Time?.Ages?.Where(a => a.Name == age);
            if (theAge.Count() == 0)
                return DefaultYear;
            return theAge.First().StartYear != 1 ? 1 : theAge.First().StartYear;
        }

        static HybrasylTime()
        {
            foreach (var regex in RegexstringList)
            {
                RegexList.Add(new Regex(regex,
                    RegexOptions.Singleline | RegexOptions.Compiled));
            }
        }

        /// <summary>
        /// Construct a Hybrasyl datetime from a Terran datetime
        /// </summary>
        /// <param name="datetime"></param>
        public HybrasylTime(DateTime datetime)
        {
            TerranDateTime = datetime;
        }

        /// <summary>
        /// Construct a Hybrasyl datetime from a Hybrasyl date
        /// </summary>
        /// <param name="age">The age of the date (e.g. Hybrasyl)</param>
        /// <param name="year">The year (e.g. 1)</param>
        /// <param name="moon">The moon</param>
        /// <param name="sun">The sun</param>
        /// <param name="hour">The hour</param>
        /// <param name="minute">The minute </param>
        public HybrasylTime(string age, int year = 1, int moon = 1, int sun = 1, int hour = 1, int minute = 1)
        {
            if (!ValidAge(age))
                throw new ArgumentException("Age is unknown to server; check time/age configuration in config.xml", nameof(age));
            var hybticks = year * TicksPerYear + moon * TicksPerMoon + sun * TicksPerSun + hour + TicksPerHour + minute * TicksPerMinute;
            TerranDateTime = Game.Config.Time.Ages.First(a => a.Name == age).StartDate.AddTicks(hybticks / 8);
        }

        /// <summary>
        /// Construct a Hybrasyl datetime from the current age
        /// </summary>
        /// <param name="year">The year (e.g. 1)</param>
        /// <param name="moon">The moon</param>
        /// <param name="sun">The sun</param>
        /// <param name="hour">The hour</param>
        /// <param name="minute">The minute</param>
        public HybrasylTime(int year = 1, int moon = 1, int sun = 1, int hour = 1, int minute = 1)
        {
            var hybticks = year * TicksPerYear + moon * TicksPerMoon + sun * TicksPerSun + hour + TicksPerHour + minute * TicksPerMinute;
            TerranDateTime = CurrentAge.StartDate.AddTicks(hybticks / 8);
        }
            
        // Some convenience functions for scripting
        public void SubtractInGameTime(int year = 0, int moon = 0, int sun = 0, int hour = 0, int minute = 0)
        {
            var hybticks = year * TicksPerYear + moon * TicksPerMoon + sun * TicksPerSun + hour + TicksPerHour + minute * TicksPerMinute;
            TerranDateTime = TerranDateTime.AddTicks((hybticks / 8) * -1);
        }

        public void AddInGameTime(int year = 0, int moon = 0, int sun = 0, int hour = 0, int minute = 0)
        {
            var hybticks = year * TicksPerYear + moon * TicksPerMoon + sun * TicksPerSun + hour + TicksPerHour + minute * TicksPerMinute;
            TerranDateTime = TerranDateTime.AddTicks(hybticks / 8);
        }


        public static HybrasylTime Now => new HybrasylTime(DateTime.Now);

        public static DateTime ConvertToTerran(HybrasylTime hybrasyltime)
        {
            var thisAge = Game.Config.Time?.Ages?.Where(age => age.Name == hybrasyltime.AgeName);
            return thisAge.Count() > 0 ? new DateTime(thisAge.First().StartDate.Ticks + hybrasyltime.TerranTicks) : new DateTime(World.StartDate.Ticks + hybrasyltime.TerranTicks);
        }

        public static HybrasylTime ConvertToHybrasyl(DateTime datetime) => new HybrasylTime(datetime);

        public static HybrasylTime FromString(string hybrasyldate)
        {
            // Supported formats:
            // <Age> <Year>, [<cardinal> moon, <cardinal> sun, HH:MM (a.m. | p.m.)]
            // <Age <Year>, [Moon <moon>, Sun <sun>, HH:MM (a.m. | p.m.)]

            var searchstring = hybrasyldate.ToLower();
            foreach (var regex in RegexList)
            {
                var theMatch = regex.Match(hybrasyldate);
                if (!theMatch.Success) continue;
                var yearInt = Int32.Parse(theMatch.Groups["Year"].Value);
                var minuteInt = theMatch.Groups["Minute"].Value != string.Empty
                    ? Int32.Parse(theMatch.Groups["Minute"].Value)
                    : 0;
                var hourInt = theMatch.Groups["Hour"].Value != string.Empty
                    ? Int32.Parse(theMatch.Groups["Hour"].Value)
                    : 0;
                var moonInt = theMatch.Groups["Moon"].Value != string.Empty
                    ? Int32.Parse(theMatch.Groups["Moon"].Value)
                    : 1;
                var sunInt = theMatch.Groups["Sun"].Value != string.Empty
                    ? Int32.Parse(theMatch.Groups["Sun"].Value)
                    : 1;

                if (hourInt < 0 || hourInt > 12 || (hourInt == 12 && theMatch.Groups["TimeMeridian"].Value == "am"))
                    hourInt = 0;
                if (minuteInt < 0 || minuteInt > 59)
                    minuteInt = 0;

                if (theMatch.Groups["TimeMeridian"].Value == "pm" || theMatch.Groups["TimeMeridian"].Value == "p.m.")
                {
                    hourInt += 12;
                }

                return new HybrasylTime(theMatch.Groups["Age"].Value,
                    yearInt, moonInt, sunInt, hourInt, minuteInt);
            }
            throw new ArgumentException("Date / time could not be parsed", nameof(hybrasyldate));
        }

        public static HybrasylTime FromDateTimestring(string datetimestring)
        {
            var datetime = DateTime.Parse(datetimestring);
            return ConvertToHybrasyl(datetime);
        }

    }
}
