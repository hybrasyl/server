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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Hybrasyl
{
    internal class DescendingComparer<T> : IComparer<T>
        where T: IComparable<T>
    {
        public int Compare(T x, T y)
        {
            return y.CompareTo(x);
        }
    }

    public class Throttle
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public int Time { get; set; }
        public int SquelchCount { get; set; }
        public int SquelchWithin { get; set; }
        public int SquelchDuration { get; set; }
        public int DisconnectAfter { get; set; }

        public Throttle(int time, int squelchCount, int squelchWithin, int squelchDuration, int disconnectAfter)
        {
            Time = time;
            SquelchCount = squelchCount;
            SquelchWithin = squelchWithin;
            SquelchDuration = squelchDuration;
            DisconnectAfter = disconnectAfter;
        }
    }

    public class ThrottleInfo
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool Throttled { get; set; }
        private Int64 PreviousReceived { get; set; }
        private Int64 LastReceived { get; set; }
        public int TotalReceived { get; set; }
        public int TotalSquelched { get; set; }
        public int TotalThrottled { get; set; }
        public int SquelchCount { get; set; }

        private SortedDictionary<long, object> SquelchedObjects { get; set; }

        private Dictionary<object, Tuple<int, long>> SeenObjects { get; set; }
        public Throttle Throttle { get; private set; }

        private void ClearExpiredSquelches()
        {
            var now = DateTime.Now.Ticks;
            Logger.InfoFormat("Clearing expired squelches");

            var keys = new List<long>(SquelchedObjects.Keys);

            foreach (var when in keys)
            {
                var span = new TimeSpan(now - when);
                if (span.TotalMilliseconds > Throttle.SquelchDuration)
                {
                    Logger.InfoFormat("Removing squelched entry");
                    SquelchedObjects.Remove(when);
                }
                else
                {
                    break;
                }
            }
        }

        public bool IsThrottled
        {
            get
            {
                var elapsed = new TimeSpan(LastReceived - PreviousReceived);
                if (Throttled && elapsed.TotalMilliseconds > Throttle.Time)
                {
                    Logger.DebugFormat("Unthrottled: difference since last throttle is {0}ms", elapsed.TotalMilliseconds);
                    Throttled = false;
                }
                else
                {
                    if (elapsed.TotalMilliseconds < Throttle.Time)
                    {
                        Logger.DebugFormat("Throttled: difference is {0}ms", elapsed.TotalMilliseconds);
                        TotalThrottled++;
                        Throttled = true;
                    }
                    else
                    {
                        Logger.DebugFormat("Not throttled: difference is {0}ms", elapsed.TotalMilliseconds);
                    }
                }
                return Throttled;
            }
        }

        public bool IsSquelched(Object obj = null)
        {
            var now = DateTime.Now.Ticks;
            ClearExpiredSquelches();

            if (obj == null)
            {
                obj = String.Empty;
            }
            if (SquelchedObjects.ContainsValue(obj))
            {
                return true;
            }
            Tuple<int, long> seen;
            if (SeenObjects.TryGetValue(obj, out seen))
            {
                var elapsed = new TimeSpan(now - seen.Item2);
                if (elapsed.TotalMilliseconds > Throttle.SquelchWithin)
                {
                    Logger.Info("SeenObjects being reset with new tuple");
                    SeenObjects[obj] = new Tuple<int, long>(0, now);
                }
                else
                {
                    if (seen.Item1 + 1 >= Throttle.SquelchCount)
                    {
                        Logger.Info("Squelched");
                        SquelchedObjects[now] = obj;
                        return true;
                    }
                    else
                    {
                        SeenObjects[obj] = new Tuple<int, long>(seen.Item1 + 1, now);
                    }
                }
            }
            else
            {
                SeenObjects[obj] = new Tuple<int, long>(0, now);
            }
            return false;
        }

        public ThrottleInfo(byte opcode)
        {
            if (Constants.PACKET_THROTTLES.ContainsKey(opcode))
            {
                Throttle = Constants.PACKET_THROTTLES[opcode];
                LastReceived = DateTime.Now.Ticks;
                TotalReceived = 0;
                TotalSquelched = 0;
                SquelchCount = 0;
                SquelchedObjects = new SortedDictionary<long, object>(new DescendingComparer<long>());
                SeenObjects = new Dictionary<object, Tuple<int, long>>();
                Throttled = false;
            }
            else
            {
                throw new ArgumentException(
                    String.Format("Can't throttle opcode {0} as it doesn't exist in PACKET_THROTTLES constant!", opcode));
            }
        }

        /// <summary>
        /// Set lastReceived to now.
        /// </summary>
        public void Received()
        {
            PreviousReceived = LastReceived;
            LastReceived = DateTime.Now.Ticks;
            TotalReceived++;
        }
    }

    internal static class ControlOpcodes
    {
        public const int CleanupUser = 0;
        public const int SaveUser = 1;
        public const int ChaosRising = 2;
        public const int ShutdownServer = 3;
        public const int RegenUser = 4;
        public const int LogoffUser = 5;
    }

    internal static class ServerTypes
    {
        public const int Lobby = 0;
        public const int Login = 1;
        public const int World = 2;
    }

    internal static class Constants
    {
        public static Regex PercentageRegex = new Regex(@"(\+|\-){0,1}(\d{0,4})%", RegexOptions.Compiled);
        public const int VIEWPORT_SIZE = 24;
        public const byte MAXIMUM_INVENTORY = 59;
        public const int MAXIMUM_DROP_DISTANCE = 2;
        public const int PICKUP_DISTANCE = 2;
        public const int EXCHANGE_DISTANCE = 5;
        public const uint MAXIMUM_GOLD = 1000000000;
        public const int VARIANT_ID_START = 100000;
        public const int DEFAULT_LOG_LEVEL = Hybrasyl.LogLevels.INFO;
        public const string EF_METADATA = "metadata=res://*/Properties.Hybrasyl.csdl|res://*/Properties.Hybrasyl.ssdl|res://*/Properties.Hybrasyl.msl;provider=MySql.Data.MySqlClient;";
        public const string EF_CONNSTRING_TEMPLATE = @"provider connection string=""server={0};user id={1};password={2};convertzerodatetime=true;allowzerodatetime=true;persist security info=True;database={3}""";

        public static string DataDirectory;
        public static string ConnectionString;

        public const ushort LAG_MAP = 1001;

        public const int NATION_SPAWN_TIMEOUT = 10800;

        public const int BYTE_HEARTBEAT_INTERVAL = 60;
        public const int TICK_HEARTBEAT_INTERVAL = 60;
        public const int REAP_HEARTBEAT_INTERVAL = 5;


        public const int REFRESH_THROTTLE_TIME = 1000;
        public const int REFRESH_REPEAT_TIMES = 2;
        public const int REFRESH_REPEAT_WITHIN = 1000;
        public const int REFRESH_SQUELCH_DURATION = 1000;
        public const int REFRESH_DISCONNECT_TRIGGER = 500;

        public const int SPEAK_THROTTLE_TIME = 250;
        public const int SPEAK_REPEAT_TIMES = 3;
        public const int SPEAK_REPEAT_WITHIN = 10000;
        public const int SPEAK_SQUELCH_DURATION = 10000;
        public const int SPEAK_DISCONNECT_TRIGGER = 200;

        public const int WHISPER_THROTTLE_TIME = 250;
        public const int WHISPER_REPEAT_TIMES = 6;
        public const int WHISPER_REPEAT_WITHIN = 2000;
        public const int WHISPER_SQUELCH_DURATION = 4000;
        public const int WHISPER_DISCONNECT_TRIGGER = 200;

        public const int USE_THROTTLE_TIME = 250;

        public const int MOVEMENT_THROTTLE_TIME = 300;
        public const int MOVEMENT_REPEAT_TIMES = 0;
        public const int MOVEMENT_REPEAT_WITHIN = 0;
        public const int MOVEMENT_SQUELCH_DURATION = 0;
        public const int MOVEMENT_DISCONNECT_TRIGGER = 200;

        public const int GENERIC_THROTTLE_TIME = 300;
        public const int GENERIC_REPEAT_TIMES = 0;
        public const int GENERIC_REPEAT_WITHIN = 0;
        public const int GENERIC_SQUELCH_DURATION = 0;
        public const int GENERIC_DISCONNECT_TRIGGER = 200;

        public static Dictionary<byte, Throttle> PACKET_THROTTLES = new Dictionary<byte, Throttle> {
            { 0x06, new Throttle(MOVEMENT_THROTTLE_TIME, MOVEMENT_REPEAT_TIMES, MOVEMENT_REPEAT_WITHIN, MOVEMENT_SQUELCH_DURATION, MOVEMENT_DISCONNECT_TRIGGER) }, { 0x0e, new Throttle(SPEAK_THROTTLE_TIME, SPEAK_REPEAT_TIMES, SPEAK_REPEAT_WITHIN, SPEAK_SQUELCH_DURATION, SPEAK_DISCONNECT_TRIGGER) }, { 0x3a, new Throttle(GENERIC_THROTTLE_TIME, GENERIC_REPEAT_TIMES, GENERIC_REPEAT_WITHIN, GENERIC_SQUELCH_DURATION, GENERIC_DISCONNECT_TRIGGER) }, { 0x38, new Throttle(REFRESH_THROTTLE_TIME, REFRESH_REPEAT_TIMES, REFRESH_REPEAT_WITHIN, REFRESH_SQUELCH_DURATION, REFRESH_DISCONNECT_TRIGGER) }, { 0x39, new Throttle(GENERIC_THROTTLE_TIME, GENERIC_REPEAT_TIMES, GENERIC_REPEAT_WITHIN, GENERIC_SQUELCH_DURATION, GENERIC_DISCONNECT_TRIGGER) } };

        public const int IDLE_TIME = 60;
        public const int IDLE_CHECK = 10;

        public const string ShutdownPassword = "batterystaple8!";

        public const int DIALOG_SEQUENCE_SHARED = 5000;
        public const int DIALOG_SEQUENCE_PURSUITS = 5100;
        public const int DIALOG_SEQUENCE_HARDCODED = 65280;

        public const string DEFAULT_CITIZENSHIP = "Mileth";

        public static Dictionary<string, int> CLASSES = new Dictionary<string, int> {
        { "Peasant", 0 },
        { "Warrior", 1 },
        { "Rogue", 2 },
        { "Wizard", 3 },
        { "Priest", 4 },
        { "Monk", 5 }
        };

        public static Dictionary<int, string> REVERSE_CLASSES = new Dictionary<int, string> {
                  { 0, "Peasant" },
                  { 1, "Warrior" },
                  { 2, "Rogue" },
                  { 3, "Wizard" },
                  { 4, "Priest" },
                  { 5, "Monk" }

        };

        public static string[] BONUS_ATTRS = { "hp", "mp", "str", "int", "wis", "con", "dex", "hit",
                                                "dmg", "ac", "mr", "regen" };

        public static string[] SCRIPT_DIRECTORIES = { "npc", "startup", "item", "reactor" };
    }

    internal static class DialogTypes
    {
        public const int FUNCTION_DIALOG = -1;
        public const int SIMPLE_DIALOG = 0;
        public const int OPTIONS_DIALOG = 2;
        public const int INPUT_DIALOG = 4;
    }

    internal static class MessageTypes
    {
        public const int WHISPER = 0;
        public const int SYSTEM = 1;
        public const int SYSTEM_WITH_OVERHEAD = 3;
        public const int SLATE_WITH_SCROLLBAR = 9;
        public const int SLATE = 10;
        public const int GROUP = 11;
        public const int GUILD = 12;
        public const int OVERHEAD = 18;
    }

    internal static class LogLevels
    {
        public const int CRIT = 2;
        public const int ERROR = 3;
        public const int WARNING = 4;
        public const int NOTICE = 5;
        public const int INFO = 6;
        public const int DEBUG = 7;
    }

    namespace Utility
    {

        /// <summary>
        /// A class to allow easy grabbing of assembly info; we use this in various places to
        /// display uniform version / copyright info.
        /// This code is modified slightly from Henning Dieterichs original class @
        /// codeproject.com/Tips/353819/Get-all-Assembly-Information
        /// </summary>
        public class AssemblyInfo
        {
            private readonly Assembly Assembly;

            public AssemblyInfo(Assembly assembly)
            {
                if (assembly == null)
                {
                    throw new ArgumentNullException("assembly");
                }
                Assembly = assembly;
            }


            public string Version
            {
                get
                {
                    var version = Assembly.GetName().Version;
                    if (version != null)
                    {
                        return version.ToString();
                    }
                    return "1.3.3.7";
                }
            }

            public string Copyright
            {
                get
                {
                    return GetAttributeValue<AssemblyCopyrightAttribute>(a => a.Copyright);
                }
            }

            protected string GetAttributeValue<TAttr>(Func<TAttr,
                string> resolveFunc, string defaultResult = null)
                where TAttr: Attribute
            {
                var attributes = Assembly.GetCustomAttributes(typeof(TAttr), false);
                if (attributes.Length > 0)
                {
                    return resolveFunc((TAttr)attributes[0]);
                }
                else
                {
                    return defaultResult;
                }
            }
        }

        /// <summary>
        /// A prettyprinter for objects that don't have a direct string representation.
        /// </summary>
        public static class PrettyPrinter
        {
            public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            /// <summary>
            /// Pretty print an object, which is essentially a dump of its properties, at the moment.
            /// </summary>
            /// <param name="obj">The object to be pretty printed, using Hybrasyl.Utility.Logger.</param>
            public static void PrettyPrint(object obj)
            {
                Logger.DebugFormat("object dump follows");
                try
                {
                    foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
                    {
                        var name = descriptor.Name;
                        var value = descriptor.GetValue(obj);
                        Logger.DebugFormat("{0} = {1}", name, value);
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Couldn't pretty print: {0}", e.ToString());
                }
            }
        }

        /// <summary>
        /// Extension methods for the Type class
        /// </summary>
        public static class TypeExtensions
        {
            /// <summary>
            /// Return true if the type is a System.Nullable wrapper of a value type
            /// </summary>
            /// <param name="type">The type to check</param>
            /// <returns>True if the type is a System.Nullable wrapper</returns>
            public static bool IsNullable(this Type type)
            {
                return type.IsGenericType
                && (type.GetGenericTypeDefinition() == typeof(Nullable<>));
            }

            /// <summary>
            /// Return true if the type is an integer of any size.
            /// </summary>
            /// <param name="value">The value to check</param>
            /// <returns>True if the type is sbyte, byte, short, ushort, int, uint, long, ulong.</returns>
            public static bool IsInteger(this object value)
            {
                return value is sbyte || value is byte || value is short || value is ushort || value is int || value is uint ||
                value is long || value is ulong;
            }
        }
    }
}
