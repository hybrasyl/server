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
using log4net;

namespace Hybrasyl
{

    public class HybrasylTime
    {
        public static ILog Logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string Age;
        public int Year;
        public int Moon;
        public int Sun;
        public int Hour;
        public int Minute;

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
                       return String.Empty;
               }
            }
        }

        public const long YearTicks = 12 * MoonTicks;
        public const long MoonTicks = 28 * SunTicks;
        public const long SunTicks = 24 * HourTicks;
        public const long HourTicks = 60 * MinuteTicks;
        public const long MinuteTicks = 60 * TimeSpan.TicksPerSecond;

        public static readonly List<string> RegexStringList = new List<string>
        {
            @"(?<Age>[A-Za-z _]*) (?<Year>\d*)(\s*,\s*(?<Moon>\d*)\s*(rd|st|nd|th) moon,\s*(?<Sun>\d*)\s*(rd|st|nd|th) sun,\s*(?<Hour>\d{0,2}):(?<Minute>\d{0,2})\s*(?<TimeMeridian>am|pm|a.m.|p.m.)){0,1}",
            @"(?<Age>[A-Za-z _]*)\s*(?<Year>\d*)(\s*,\s*Moon\s*(?<Moon>\d*),\s*Sun\s*(?<Sun>\d*)\s*(?<Hour>\d{0,2}):(?<Minute>\d{0,2})\s*(?<TimeMeridian>am|pm|a.m.|p.m.)){0,1}"
        };

        public static List<Regex> RegexList = new List<Regex>();

        public long HybrasylTicks
        {
            get
            {
                var yearsElapsed = Year;
                if (FirstYearInAge(Age) != 1)
                    yearsElapsed = Year - FirstYearInAge(Age);
                return (yearsElapsed * YearTicks + Moon * MoonTicks + Sun * SunTicks + Hour * HourTicks + Minute * MinuteTicks);

            }
        }


        public long TerranTicks => HybrasylTicks / 8;

        public static string DefaultAge => Game.Config.Time.ServerStart.DefaultAge != string.Empty ? Game.Config.Time.ServerStart.DefaultAge : "Hybrasyl";
        public static int DefaultYear => Game.Config.Time.ServerStart.DefaultYear != 1 ? Game.Config.Time.ServerStart.DefaultYear : 1;

        public static bool ValidAge(string age)
        {
            return Game.Config.Time.Ages.First(a => a.Name == age) != null || DefaultAge == age;
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
                $"{Age} {Year}, {Moon.DisplayWithOrdinal()} moon, {Sun.DisplayWithOrdinal()} sun, {hour}:{Minute.ToString("d2")} {ampm}";
        }

        public static string CurrentAge
        {
            get
            {
                var now = DateTime.Now;
                if (Game.Config.Time.Ages.Count == 0)
                    return DefaultAge;
                var currentAge = Game.Config.Time.Ages.First(age => age.DateInAge(now));
                return currentAge == null ? DefaultAge : currentAge.Name;
            }
        }

        public static int CurrentYear => Now().Year;

        public static int FirstYearInAge(string age)
        {
            if (!ValidAge(age))
                throw new ArgumentException("Age is unknown to server; check time/age configuration in config.xml", age);

            var theAge = Game.Config.Time.Ages.First(a => a.Name == age);
            if (theAge == null)
                return DefaultYear;
            return theAge.StartYear != 1 ? 1 : theAge.StartYear;
        }

        static HybrasylTime()
        {
            foreach (var regex in RegexStringList)
            {
                RegexList.Add(new Regex(regex,
                    RegexOptions.Singleline | RegexOptions.Compiled));
            }
        }

        public HybrasylTime(int year = 1, int moon = 1, int sun = 1, int hour = 1, int minute = 1)
        {
            Year = year;
            Moon = moon;
            Sun = sun;
            Hour = hour;
            Minute = minute;
            Age = DefaultAge;
        }

        public HybrasylTime(string age, int year = 1, int moon = 1, int sun = 1, int hour = 1, int minute = 1)
        {
            if (!ValidAge(age))
                throw new ArgumentException("Age is unknown to server; check time/age configuration in config.xml", nameof(age));
            Age = age;
            Year = year;
            Moon = moon;
            Sun = sun;
            Hour = hour;
            Minute = minute;
        }

        public void AdvanceDateFromTerranTicks(long ticks)
        {
            var hurr = ticks * 8;
            AdvanceDateFromHybrasylTicks(hurr);
        }

        public void AdvanceDateFromHybrasylTicks(long ticks)
        {
            while (ticks > HourTicks)
            {
                // Year
                if (ticks >= YearTicks)
                {
                    ticks -= YearTicks;
                    Year++;
                    continue;
                }
                if (ticks >= MoonTicks)
                {
                    ticks -= MoonTicks;
                    Moon++;
                    continue;
                }
                if (ticks >= SunTicks)
                {
                    ticks -= SunTicks;
                    Sun++;
                    continue;
                }
                if (ticks >= HourTicks)
                {
                    ticks -= HourTicks;
                    Hour++;
                }
            }
            Minute = (int)(ticks / MinuteTicks);

        }

        public static HybrasylTime Now()
        {
            var hybrasylTime = new HybrasylTime();
            var terranNow = DateTime.Now;
            var timeElapsed = DateTime.Now.Ticks - World.StartDate.Ticks;

            if (Game.Config.Time.Ages.Count > 0)
            {
                var currentAge = Game.Config.Time.Ages.First(age => age.DateInAge(terranNow));
                if (currentAge == null)
                {
                    // Age configuration is screwy, simply return default age
                    Logger.ErrorFormat("Age configuration is nonsensical, using default age");
                }
                else
                {
                    // Calculate the time that has passed from the start of the current age, to now
                    timeElapsed = terranNow.Ticks - currentAge.StartDate.Ticks;
                    hybrasylTime.Age = currentAge.Name;
                    if (currentAge.StartYear != 1)
                        hybrasylTime.Year = currentAge.StartYear;
                }
            }
            else
            {
                hybrasylTime.Age = Game.Config.Time.ServerStart.DefaultAge != string.Empty
                    ? Game.Config.Time.ServerStart.DefaultAge
                    : DefaultAge;

                if (Game.Config.Time.ServerStart.DefaultYear != 1)
                    hybrasylTime.Year += Game.Config.Time.ServerStart.DefaultYear;
            }

            hybrasylTime.AdvanceDateFromTerranTicks(timeElapsed);

            return hybrasylTime;
        }

        public static DateTime ConvertToTerran(HybrasylTime hybrasyltime)
        {
            var thisAge = Game.Config.Time.Ages.First(age => age.Name == hybrasyltime.Age);
            return thisAge != null ? new DateTime(thisAge.StartDate.Ticks + hybrasyltime.TerranTicks) : new DateTime(World.StartDate.Ticks + hybrasyltime.TerranTicks);
        }

        public static HybrasylTime ConvertToHybrasyl(DateTime datetime)
        {
            if (datetime.Ticks < World.StartDate.Ticks)
                throw new ArgumentException("Date passed occurs before known time", nameof(datetime));

            var hybrasylTime = new HybrasylTime();
            var thisAge = Game.Config.Time.Ages.FirstOrDefault(age => age.DateInAge(datetime));
            var timeElapsed = datetime.Ticks - World.StartDate.Ticks;

            if (thisAge == null)
            {
                hybrasylTime.Age = DefaultAge;
                hybrasylTime.Year = DefaultYear;
            }
            else
            {
                hybrasylTime.Age = thisAge.Name;
                timeElapsed = datetime.Ticks - thisAge.StartDate.Ticks;
                hybrasylTime.Year = thisAge.StartYear;
            }

            hybrasylTime.AdvanceDateFromTerranTicks(timeElapsed);
            return hybrasylTime;
        }

        public static HybrasylTime FromString(string hybrasyldate)
        {
            // Supported formats:
            // <Age> <Year>, [<cardinal> moon, <cardinal> sun, HH:MM (a.m. | p.m.)]
            // <Age <Year>, [Moon <moon>, Sun <sun>, HH:MM (a.m. | p.m.)]

            var searchString = hybrasyldate.ToLower();
            foreach (var regex in RegexList)
            {
                var theMatch = regex.Match(hybrasyldate);
                if (!theMatch.Success) continue;
                var yearInt = Int32.Parse(theMatch.Groups["Year"].Value);
                var minuteInt = theMatch.Groups["Minute"].Value != String.Empty
                    ? Int32.Parse(theMatch.Groups["Minute"].Value)
                    : 0;
                var hourInt = theMatch.Groups["Hour"].Value != String.Empty
                    ? Int32.Parse(theMatch.Groups["Hour"].Value)
                    : 0;
                var moonInt = theMatch.Groups["Moon"].Value != String.Empty
                    ? Int32.Parse(theMatch.Groups["Moon"].Value)
                    : 1;
                var sunInt = theMatch.Groups["Sun"].Value != String.Empty
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

        public static HybrasylTime FromDateTimeString(string datetimestring)
        {
            var datetime = DateTime.Parse(datetimestring);
            return ConvertToHybrasyl(datetime);
        }

    }
}
