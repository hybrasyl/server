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

using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
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
        public const int MonolithSpawn = 8;
        public const int MonolithControl = 9;
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
        public const byte MAXIMUM_BOOK = 90;
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

        // Death pile timeouts (how long someone has to wait until they can loot a death pile)

        public const int DEATHPILE_GROUP_TIMEOUT = 0; // Group/self can pick up deathpile immediately
        public const int DEATHPILE_RANDO_TIMEOUT = 900; // Randos can pick up death piles after 15 minutes

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

        // Idle settings
        // A client counts as idle after IDLE_TIME seconds without any packet receipt (except for heartbeat opcodes)
        // The idle check job will run every IDLE_CHECK seconds

        public const int IDLE_TIME = 60;
        public const int IDLE_CHECK = 10;

        // Shutdown password
        // This is a dirty hack until we have better role / auth support

        public const string ShutdownPassword = "batterystaple8!";
        public const int ControlServicePort = 4949;

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
        // You must wait this long before sending another board/mail message
        public const int SEND_MESSAGE_COOLDOWN = 2000; 
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
