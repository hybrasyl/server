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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using System.IO;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Hybrasyl.Enums;

namespace Hybrasyl
{
    public static class IntegerExtensions
    {
        public static string DisplayWithOrdinal(this int num)
        {
            if (num.ToString().EndsWith("11")) return num.ToString() + "th";
            if (num.ToString().EndsWith("12")) return num.ToString() + "th";
            if (num.ToString().EndsWith("13")) return num.ToString() + "th";
            if (num.ToString().EndsWith("1")) return num.ToString() + "st";
            if (num.ToString().EndsWith("2")) return num.ToString() + "nd";
            if (num.ToString().EndsWith("3")) return num.ToString() + "rd";
            return num.ToString() + "th";
        }
    }

    class DescendingComparer<T> : IComparer<T> where T : IComparable<T>
    {
        public int Compare(T x, T y)
        {
            return y.CompareTo(x);
        }
    }

    // Generic container for throttling info
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

        // N.B. SquelchedObjects is in *reverse* order, meaning oldest squelches are first
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

        public long LastSuccess { get; set; }

        public bool IsThrottled
        {
            get
            {                
                var elapsed = new TimeSpan(LastReceived - PreviousReceived);
                var lastSuccess = new TimeSpan(LastReceived - LastSuccess);
                if ((Throttled && elapsed.TotalMilliseconds > Throttle.Time) || (Throttled && lastSuccess.TotalMilliseconds > Throttle.Time) )
                {
                    Logger.DebugFormat("Unthrottled: difference since last throttle is {0}ms", elapsed.TotalMilliseconds);
                    LastSuccess = DateTime.Now.Ticks;
                    Throttled = false;
                }
                else if (elapsed.TotalMilliseconds < Throttle.Time)
                {
                    Logger.DebugFormat("Throttled: difference is {0}ms", elapsed.TotalMilliseconds);
                    TotalThrottled++;
                    Throttled = true;
                }
                else
                {
                    Logger.DebugFormat("Not throttled: difference is {0}ms", elapsed.TotalMilliseconds);
                }
                return Throttled;
            }
        }

        public bool IsSquelched(Object obj = null)
        {
            var now = DateTime.Now.Ticks;
            ClearExpiredSquelches();

            if (obj == null)
                obj = String.Empty;

            if (SquelchedObjects.ContainsValue(obj))
                return true;
                
            Tuple<int, long> seen;
            if (SeenObjects.TryGetValue(obj, out seen))
            {
                // Item1 = count, Item2 = last seen
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
                        // Squelched
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
            
            //if (Throttle != Constants.PACKET_THROTTLES[0x13])
            //{
                PreviousReceived = LastReceived;
                LastReceived = DateTime.Now.Ticks;
                TotalReceived++;
            //}
            //else
            //{
                LastReceived = DateTime.Now.Ticks;
                TotalReceived++;
                return;
            //}
            //PreviousReceived = LastReceived;
        }

    }

    static class ControlOpcodes
    {
        public const int CleanupUser = 0;
        public const int SaveUser = 1;
        public const int ChaosRising = 2;
        public const int ShutdownServer = 3;
        public const int RegenUser = 4;
        public const int LogoffUser = 5;
        public const int MailNotifyUser = 6;
        public const int StatusTick = 7;
    }

    static class ServerTypes
    {
        public const int Lobby = 0;
        public const int Login = 1;
        public const int World = 2;
    }

    static class Constants
    {
        // Eventually most of these should be moved into a config file. For right now they're here.

        public static int MAX_LEVEL = 99;
        public static Regex PercentageRegex = new Regex(@"(\+|\-){0,1}(\d{0,4})%", RegexOptions.Compiled);
        public const int VIEWPORT_SIZE = 24;
        public const byte MAXIMUM_INVENTORY = 59;
        public const int MAXIMUM_DROP_DISTANCE = 2;
        public const int PICKUP_DISTANCE = 2;
        public const int EXCHANGE_DISTANCE = 5;
        public const uint MAXIMUM_GOLD = 1000000000;
        public const int VARIANT_ID_START = 100000;
        public const int DEFAULT_LOG_LEVEL = Hybrasyl.LogLevels.DEBUG;
        // Manhattan distance between the user performing an action (killing a monster, etc) and other
        // users in the group in order to be eligible for sharing.
        public const int GROUP_SHARING_DISTANCE = 20;

        public static string DataDirectory;

        // This should be an ID of a map that will be used to store players in the event of lag / worldmap disconnects
        public const ushort LAG_MAP = 1001;

        // If a player logs in again before this time, they will be returned to their
        // last known location as opposed to their nation's spawn point. This is in
        // seconds.

        public const int NATION_SPAWN_TIMEOUT = 10800; // 3 hours

        // Heartbeat controls
        // Every BYTE_HEARTBEAT_INTERVAL and TICK_HEARTBEAT_INTERVAL seconds, Hybrasyl sends 0x3B and 0x68 
        // heartbeat packets to clients.
        // The client expects to receive these and responds in kind (otherwise it will disconnect after a 
        // certain period of time).
        // Hybrasyl provides several jobs to handle these; two jobs to transmit the packets to clients, and
        // a "Reaper Job" to disconnect clients that haven't responded to either of the heartbeats within REAP_HEARTBEAT_INTERVAL.
        // The reaper job will run every REAP_HEARTBEAT_INTERVAL seconds to ensure that no connections are missed.

        public const int BYTE_HEARTBEAT_INTERVAL = 60;
        public const int TICK_HEARTBEAT_INTERVAL = 60;
        public const int REAP_HEARTBEAT_INTERVAL = 5;

        // The message a spirit gets when it tries to do things it cannot

        public const string SPIRIT_FORBIDDEN = "Spirits cannot do that.";

        public static Dictionary<PlayerCondition, string> STATUS_RESTRICTION_MESSAGES = new Dictionary
            <PlayerCondition, string>
        {
            {PlayerCondition.InComa, NearDeathStatus.ActionProhibitedMessage},
            {PlayerCondition.Asleep, SleepStatus.ActionProhibitedMessage},
            {PlayerCondition.Frozen, FreezeStatus.ActionProhibitedMessage},
            {PlayerCondition.Paralyzed, ParalyzeStatus.ActionProhibitedMessage},
            {PlayerCondition.Alive, Constants.SPIRIT_FORBIDDEN}

        };
        // These times control various throttling of packet receipts. 
        // The thresholds are in milliseconds.
        // Generally:
        // *_THROTTLE_TIME = Upper limit on intervals between opcode receipt
        // *_REPEAT_TIMES = Number of times an opcode / object combination (e.g. a say message) can
        // be repeated within REPEAT_WITHIN time. 0 disables.
        // *_SQUELCH_DURATION = Once REPEAT_TIME within REPEAT_WITHIN is reached, stop listening to similar
        // opcode or opcode+object combos for SQUELCH_DURATION milliseconds. 0 disables.
        // *_DISCONNECT_TRIGGER = Number of total times a throttle or a squelch must be hit before the client is just 
        // disconnected altogether. 0 disables.

        // Throttling for refresh opcode (F5)
        public const int REFRESH_THROTTLE_TIME = 1000;
        public const int REFRESH_REPEAT_TIMES = 2;
        public const int REFRESH_REPEAT_WITHIN = 1000;
        public const int REFRESH_SQUELCH_DURATION = 1000;
        public const int REFRESH_DISCONNECT_TRIGGER = 500;

        // Number of times you're allowed to say exactly the same thing
        public const int SPEAK_THROTTLE_TIME = 250;
        public const int SPEAK_REPEAT_TIMES = 3;
        public const int SPEAK_REPEAT_WITHIN = 10000;
        public const int SPEAK_SQUELCH_DURATION = 10000;
        public const int SPEAK_DISCONNECT_TRIGGER = 200;

        // Number of times you're allowed to whisper exactly the same thing
        // Not currently enforced.
        public const int WHISPER_THROTTLE_TIME = 250;
        public const int WHISPER_REPEAT_TIMES = 6;
        public const int WHISPER_REPEAT_WITHIN = 2000;
        public const int WHISPER_SQUELCH_DURATION = 4000;
        public const int WHISPER_DISCONNECT_TRIGGER = 200;

        // Throttling for skills/spells. Not currently implemented because, well, skills aren't.
        public const int USE_THROTTLE_TIME = 250;
        public const int SPACEBAR_THROTTLE_TIME = 600; 

        // Throttling for movement opcode
        // USDA seems to allow a total of 7 movements in a one second cycle; we decrease that
        // to ~3.33 in one second, or every 300ms.
        // N.B. this doesn't limit turning, just movement
        public const int MOVEMENT_THROTTLE_TIME = 300;
        public const int MOVEMENT_REPEAT_TIMES = 0;
        public const int MOVEMENT_REPEAT_WITHIN = 0;
        public const int MOVEMENT_SQUELCH_DURATION = 0;
        public const int MOVEMENT_DISCONNECT_TRIGGER = 200;

        // Throttling for generic opcodes
        // This currently covers things like opening an NPC or using a dialog 
        public const int GENERIC_THROTTLE_TIME = 300;
        public const int GENERIC_REPEAT_TIMES = 0;
        public const int GENERIC_REPEAT_WITHIN = 0;
        public const int GENERIC_SQUELCH_DURATION = 0;
        public const int GENERIC_DISCONNECT_TRIGGER = 200;
        

        // This consolidates all the above information into a static dictionary of the following:
        // opcode, throttle time, number of consecutive packets that trigger a squelch, and squelch time.
        // Opcodes not on this list are not throttled, or squelched.
        // A squelch refers to Hybrasyl outright ignoring packets sent over a certain threshold (_REPEAT_TIMES) for the amount specified
        // by the squelch time (_SQUELCH_TIME). A value of 0 for the repeat time disables squelching for the given opcode.
        // e.g. 3, 250 = A player can send 3 packets of the same opcode / object in a row before being squelched for 250ms.
        public static Dictionary<byte, Throttle> PACKET_THROTTLES = new Dictionary<byte, Throttle> {
            {0x06, new Throttle(MOVEMENT_THROTTLE_TIME, MOVEMENT_REPEAT_TIMES, MOVEMENT_REPEAT_WITHIN, MOVEMENT_SQUELCH_DURATION, MOVEMENT_DISCONNECT_TRIGGER)}, // movement
            {0x0e, new Throttle(SPEAK_THROTTLE_TIME, SPEAK_REPEAT_TIMES, SPEAK_REPEAT_WITHIN, SPEAK_SQUELCH_DURATION, SPEAK_DISCONNECT_TRIGGER)},  // say / shout 
            {0x3a, new Throttle(GENERIC_THROTTLE_TIME, GENERIC_REPEAT_TIMES, GENERIC_REPEAT_WITHIN, GENERIC_SQUELCH_DURATION, GENERIC_DISCONNECT_TRIGGER)},  // NPC use dialog
            {0x38, new Throttle(REFRESH_THROTTLE_TIME, REFRESH_REPEAT_TIMES, REFRESH_REPEAT_WITHIN, REFRESH_SQUELCH_DURATION, REFRESH_DISCONNECT_TRIGGER)},  // refresh (F5)
            {0x39, new Throttle(GENERIC_THROTTLE_TIME, GENERIC_REPEAT_TIMES, GENERIC_REPEAT_WITHIN, GENERIC_SQUELCH_DURATION, GENERIC_DISCONNECT_TRIGGER)},  // NPC main menu
            {0x13, new Throttle(300, 1, 600, 600, 90000000)}, //Assail - this doesn't work through normal throttling. Moved to assail usage.
        };

        // Message throttling 

        public const int SEND_MESSAGE_COOLDOWN = 2000; // You must wait two seconds before sending another message

        // Idle settings
        // A client counts as idle after IDLE_TIME seconds without any packet receipt (except for heartbeat opcodes)
        // The idle check job will run every IDLE_CHECK seconds

        public const int IDLE_TIME = 60;
        public const int IDLE_CHECK = 10;

        // Shutdown password
        // This is a dirty hack until we have better role / auth support

        public const string ShutdownPassword = "batterystaple8!";

        // Dialog settings
        // Dialog sequence IDs between 1 and DIALOG_SEQUENCE_SHARED are processed as 
        // "shared" (globally available) sequences; sequence IDs between DIALOG_SEQUENCE_SHARED
        // and DIALOG_SEQUENCE_PURSUITS are pursuits (main menu options); IDs above 
        // DIALOG_SEQUENCE_PURSUITS are local to the object in question.
        public const int DIALOG_SEQUENCE_SHARED = 5000;
        public const int DIALOG_SEQUENCE_PURSUITS = 5100;
        public const int DIALOG_SEQUENCE_HARDCODED = 65280;

        public static Dictionary<string, int> CLASSES = new Dictionary<string, int> {
        {"Peasant", 0},
        {"Warrior", 1},
        {"Rogue", 2},
        {"Wizard", 3},
        {"Priest", 4},
        {"Monk", 5}
        };

        public static Dictionary<int, string> REVERSE_CLASSES = new Dictionary<int, string> {
                  {0, "Peasant"},
                  {1, "Warrior"},
                  {2, "Rogue"},
                  {3, "Wizard"},
                  {4, "Priest"},
                  {5, "Monk"}

        };

        public static string[] BONUS_ATTRS = { "hp", "mp", "str", "int", "wis", "con", "dex", "hit", 
                                                "dmg", "ac", "mr", "regen" };

        public static string[] SCRIPT_DIRECTORIES = { "npc", "startup", "item", "reactor", "common"};
        public const int MESSAGE_RETURN_SIZE = 64;
    }

    public static class LevelCircles
    {
        public const int CIRCLE_1 = 11;
        public const int CIRCLE_2 = 41;
        public const int CIRCLE_3 = 71;
        public const int CIRCLE_4 = 90;
    }

    static class StatLimitConstants
    {
        public static int MIN_STAT = 1; // str, int, wis, con, dex
        public static int MAX_STAT = 255;
        public static int MIN_BASE_HPMP = 1;
        public static int? MAX_BASE_HPMP = null;

        public static int MIN_DMG = 0;
        public static int? MAX_DMG = null;
        public static int MIN_HIT = 0;
        public static int? MAX_HIT = null;
        public static int MIN_AC = -90;
        public static int MAX_AC = 100;
        public static int MIN_MR = 0;
        public static int MAX_MR = 8;
    }

    static class StatGainConstants
    {
        public const int PEASANT_BASE_HP_GAIN = 8;
        public const int PEASANT_BASE_MP_GAIN = 8;
        public const int PEASANT_BONUS_HP_GAIN = 4;
        public const int PEASANT_BONUS_MP_GAIN = 4;

        public const int WARRIOR_BASE_HP_GAIN = 71;
        public const int WARRIOR_BASE_MP_GAIN = 8;
        public const int WARRIOR_BONUS_HP_GAIN = 9;
        public const int WARRIOR_BONUS_MP_GAIN = 4;

        public const int ROGUE_BASE_HP_GAIN = 48;
        public const int ROGUE_BASE_MP_GAIN = 22;
        public const int ROGUE_BONUS_HP_GAIN = 10;
        public const int ROGUE_BONUS_MP_GAIN = 8;

        public const int MONK_BASE_HP_GAIN = 39;
        public const int MONK_BASE_MP_GAIN = 31;
        public const int MONK_BONUS_HP_GAIN = 9;
        public const int MONK_BONUS_MP_GAIN = 12;

        public const int PRIEST_BASE_HP_GAIN = 28;
        public const int PRIEST_BASE_MP_GAIN = 55;
        public const int PRIEST_BONUS_HP_GAIN = 4;
        public const int PRIEST_BONUS_MP_GAIN = 10;

        public const int WIZARD_BASE_HP_GAIN = 18;
        public const int WIZARD_BASE_MP_GAIN = 68;
        public const int WIZARD_BONUS_HP_GAIN = 4;
        public const int WIZARD_BONUS_MP_GAIN = 4;


        // Modifiers for HP/MP gain upon leveling up, based on the user's Level Circle
        public const double LEVEL_CIRCLE_GAIN_MODIFIER_0 = 0.0;
        public const double LEVEL_CIRCLE_GAIN_MODIFIER_1 = 0.25;
        public const double LEVEL_CIRCLE_GAIN_MODIFIER_2 = 0.5;
        public const double LEVEL_CIRCLE_GAIN_MODIFIER_3 = 0.75;
        public const double LEVEL_CIRCLE_GAIN_MODIFIER_4 = 1.0;
    }

    static class DialogTypes
    {
        public const int FUNCTION_DIALOG = -1;
        public const int SIMPLE_DIALOG = 0;
        public const int OPTIONS_DIALOG = 2;
        public const int INPUT_DIALOG = 4;
    }

    static class MessageTypes
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

    static class LogLevels
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
                    throw new ArgumentNullException("assembly");
                Assembly = assembly;
            }


            public string Version
            {
                get
                {
                    string result = string.Empty;
                    Version version = Assembly.GetName().Version;
                    if (version != null)
                        return version.ToString();
                    return "1.3.3.7";
                }
            }

            public string Copyright
            {
                get { return GetAttributeValue<AssemblyCopyrightAttribute>(a => a.Copyright); }
            }

            protected string GetAttributeValue<TAttr>(Func<TAttr,
                string> resolveFunc, string defaultResult = null) where TAttr : Attribute
            {
                object[] attributes = Assembly.GetCustomAttributes(typeof(TAttr), false);
                if (attributes.Length > 0)
                    return resolveFunc((TAttr)attributes[0]);
                else
                    return defaultResult;
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
                        string name = descriptor.Name;
                        object value = descriptor.GetValue(obj);
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
        } // end TypeExtensions

    } // end Namespace:Utility

}// end Namespace: Hybrasyl
