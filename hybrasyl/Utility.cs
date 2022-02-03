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
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Hybrasyl
{

    [AttributeUsage(AttributeTargets.Class)]
    public class RedisType : Attribute { }

    public static class Extensions
    {
        public static IEnumerable<string> Split(this string str, int n)
        {
            if (String.IsNullOrEmpty(str) || n < 1)
            {
                throw new ArgumentException();
            }

            for (int i = 0; i < str.Length; i += n)
            {
                yield return str.Substring(i, Math.Min(n, str.Length - i));
            }
        }

	public static bool IsAscii(this string value) => !Regex.Match(value, "[^\x00-\x7F]").Success;

    }

    public static class RandomExtensions
    {
        //private static readonly Random random = new Random();
        public static string RandomString(this Random rand, int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[rand.Next(s.Length)]).ToArray());
        }
    }

    public static class EnumerableExtension
    {
        public static T PickRandom<T>(this IEnumerable<T> source, bool nullifempty = false)
        {
            if (nullifempty && source.Count() == 0) return default;
            return source.PickRandom(1).Single();
        }

        public static IEnumerable<T> PickRandom<T>(this IEnumerable<T> source, int count)
        {
            return source.Shuffle().Take(count);
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.OrderBy(x => Guid.NewGuid());
        }
    }

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

    // If you add a new control opcode here, also add it to metricsincludes.tt
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
        public const int TriggerRefresh = 10;
        public const int HandleDeath = 11;
        public const int DialogRequest = 12;
        public const int GlobalMessage = 13;
        public const int RemoveReactor = 14;
        public const int ModifyStats = 15;
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

        // Manhattan distance between two players required for an asynchronous dialog request
        public const int ASYNC_DIALOG_DISTANCE = 10;

        // This should be an ID of a map that will be used to store players in the event of lag / worldmap disconnects
        public const ushort LAG_MAP = 1001;

        // If a player logs in again before this time, they will be returned to their
        // last known location as opposed to their nation's spawn point. This is in
        // seconds.

        public const int NATION_SPAWN_TIMEOUT = 10800; // 3 hours

        // Death pile timeouts (how long someone has to wait until they can loot a death pile)

        public const int DEATHPILE_GROUP_TIMEOUT = 0; // Group/self can pick up deathpile immediately
        public const int DEATHPILE_RANDO_TIMEOUT = 900; // Randos can pick up death piles after 15 minutes

        // Monster loot drop timeouts (how long someone has to wait until they can loot someone elses monster loot)
        public const int MONSTER_LOOT_DROP_RANDO_TIMEOUT = 60; //Randos can pick up loot after 1 minute

        // Monster tagging timeout
        public const int MONSTER_TAGGING_TIMEOUT = 300;
        
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

        // Dialog settings
        // Dialog sequence IDs between 1 and DIALOG_SEQUENCE_SHARED are processed as 
        // "shared" (globally available) sequences; sequence IDs between DIALOG_SEQUENCE_SHARED
        // and DIALOG_SEQUENCE_PURSUITS are pursuits (main menu options); IDs above 
        // DIALOG_SEQUENCE_PURSUITS are local to the object in question. 
        // DIALOG_SEQUENCE_ASYNC -> Special ID - e.g. a *singular* ID - reserved for asynchronous dialogs 
        // (these are effectively individual dialog  sessions that are managed by the server between two participants)
        // DIALOG_SEQUENCE_HARDCODED -> dialogs reserved for internal (e.g. C#) implementations, such as merchant stores, etc
        public const int DIALOG_SEQUENCE_SHARED = 5000;
        public const int DIALOG_SEQUENCE_PURSUITS = 5100;
        public const int DIALOG_SEQUENCE_ASYNC = 65000;
        public const int DIALOG_SEQUENCE_HARDCODED = 65280;

        public static Dictionary<string, int> CLASSES = new Dictionary<string, int> {
        {"peasant", 0},
        {"warrior", 1},
        {"rogue", 2},
        {"wizard", 3},
        {"priest", 4},
        {"monk", 5}
        };

        public static Dictionary<int, string> REVERSE_CLASSES = new Dictionary<int, string> {
                  {0, "peasant"},
                  {1, "warrior"},
                  {2, "rogue"},
                  {3, "wizard"},
                  {4, "priest"},
                  {5, "monk"}

        };

        public static string[] BONUS_ATTRS = { "hp", "mp", "str", "int", "wis", "con", "dex", "hit", 
                                                "dmg", "ac", "mr", "regen" };

        public const int MESSAGE_RETURN_SIZE = 64;
        // You must wait this long in seconds before sending another board message
        public const int BOARD_SEND_MESSAGE_COOLDOWN = 60;
        // You must wait this long in seconds before sending another mail message to the same recipient
        public const int MAIL_MESSAGE_COOLDOWN = 60;

    }

    public static class LevelCircles
    {
        public const int CIRCLE_1 = 11;
        public const int CIRCLE_2 = 41;
        public const int CIRCLE_3 = 71;
        public const int CIRCLE_4 = 90;
    }

    // TODO: move to xml
    static class StatLimitConstants
    {
        public static long MIN_STAT = 1; // str, int, wis, con, dex
        public static long MAX_STAT = 255;
        public static long MIN_BASE_HPMP = 1;
        public static long MAX_BASE_HPMP = uint.MaxValue;

        public static double MIN_DMG = -16.0;
        public static double MAX_DMG = 16.0;
        public static double MIN_HIT = -16.0;
        public static double MAX_HIT = 16.0;
        public static long MIN_AC = -90;
        public static long MAX_AC = 100;
        public static double MIN_MR = -16.0;
        public static double MAX_MR = 16.0;
    }

    static class DialogTypes
    {
        public const int FUNCTION_DIALOG = -1;
        public const int SIMPLE_DIALOG = 0;
        public const int OPTIONS_DIALOG = 2;
        public const int INPUT_DIALOG = 4;
        public const int JUMP_DIALOG = 8;
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


        public class MultiIndexDictionary<TKey1, TKey2, TValue>
        {
            private Dictionary<TKey1, KeyValuePair<TKey2, TValue>> _dict1;
            private Dictionary<TKey2, KeyValuePair<TKey1, TValue>> _dict2;

            public MultiIndexDictionary()
            {
                _dict1 = new Dictionary<TKey1, KeyValuePair<TKey2, TValue>>();
                _dict2 = new Dictionary<TKey2, KeyValuePair<TKey1, TValue>>();
            }

            public void Add(TKey1 k1, TKey2 k2, TValue value)
            {
                _dict1.Add(k1, new KeyValuePair<TKey2, TValue>(k2, value));
                _dict2.Add(k2, new KeyValuePair<TKey1, TValue>(k1, value));
            }

            public void Clear()
            {
                _dict1 = new Dictionary<TKey1, KeyValuePair<TKey2, TValue>>();
                _dict2 = new Dictionary<TKey2, KeyValuePair<TKey1, TValue>>();
            }

            public int Count => _dict1.Count;

            public bool ContainsKey(TKey1 k1) => _dict1.ContainsKey(k1);

            public bool ContainsKey(TKey2 k2) => _dict2.ContainsKey(k2);
         
            public bool Remove(TKey1 k1)
            {
                if (_dict1.ContainsKey(k1))
                {
                    var k2obj = _dict1[k1];
                    return _dict1.Remove(k1) && _dict2.Remove(k2obj.Key);
                }
                else
                    return false;
            }

            public bool Remove(TKey2 k2)
            {
                if (_dict2.ContainsKey(k2))
                {
                    var k1obj = _dict2[k2];
                    return _dict2.Remove(k2) && _dict1.Remove(k1obj.Key);
                }
                else
                    return false;
            }

            public bool TryGetValue(TKey1 k1, out TValue value)
            {
                value = default;
                if (_dict1.TryGetValue(k1,out KeyValuePair<TKey2, TValue> kvp))
                {
                    value = kvp.Value;
                    return true;
                }
                return false;
            }

            public bool TryGetValue(TKey2 k2, out TValue value)
            {
                value = default;
                if (_dict2.TryGetValue(k2, out KeyValuePair<TKey1,TValue> kvp))
                {
                    value = kvp.Value;
                    return true;
                }
                return false;
            }

        }

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

            public string GitHash
            {
                get
                {
                    var attr = Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (attr is not null)
                        return attr.InformationalVersion.Split('+').Last();
                    return "unknown";
                }
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
            /// <summary>
            /// Pretty print an object, which is essentially a dump of its properties, at the moment.
            /// </summary>
            /// <param name="obj">The object to be pretty printed, using Hybrasyl.Utility.Logger.</param>
            public static void PrettyPrint(object obj)
            {
                GameLog.DebugFormat("object dump follows");
                try
                {
                    foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
                    {
                        string name = descriptor.Name;
                        object value = descriptor.GetValue(obj);
                        GameLog.DebugFormat("{0} = {1}", name, value);
                    }
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Couldn't pretty print: {0}", e.ToString());
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

        public static class StringExtensions
        {
            public static bool Contains(this string source, string toCheck, StringComparison comparision)
            {
                return source?.IndexOf(toCheck, comparision) >= 0;
            }
            public static string Capitalize(this string s)
            {
                if (string.IsNullOrEmpty(s))
                    return string.Empty;

                char[] a = s.ToCharArray();
                a[0] = char.ToUpper(a[0]);
                return new string(a);
            }
            public static string Normalize(string key) => Regex.Replace(key.ToLower(), @"\s+", "");

        }

    } // end Namespace:Utility

}// end Namespace: Hybrasyl
