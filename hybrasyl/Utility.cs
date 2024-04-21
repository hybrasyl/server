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
using Hybrasyl.ChatCommands;
using Hybrasyl.Dialogs;

namespace Hybrasyl
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RedisType : Attribute { }

    public static class Extensions
    {
        public static IEnumerable<string> Split(this string str, int n)
        {
            if (string.IsNullOrEmpty(str) || n < 1) throw new ArgumentException();

            for (var i = 0; i < str.Length; i += n) yield return str.Substring(i, Math.Min(n, str.Length - i));
        }

        public static bool IsAscii(this string value) => !Regex.Match(value, "[^\x00-\x7F]").Success;
    }

    public static class RandomExtensions
    {
        public static string RandomString(this Random rand, int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(selector: s => s[rand.Next(s.Length)]).ToArray());
        }
    }

    public static class EnumerableExtension
    {
        public static T PickRandom<T>(this IEnumerable<T> source, bool nullifempty = false)
        {
            if (nullifempty && source.Count() == 0) return default;
            return source.PickRandom(1).Single();
        }

        public static IEnumerable<T> PickRandom<T>(this IEnumerable<T> source, int count) =>
            source.Shuffle().Take(count);

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.OrderBy(keySelector: x => Guid.NewGuid());
        }
    }

    internal class DescendingComparer<T> : IComparer<T> where T : IComparable<T>
    {
        public int Compare(T x, T y) => y.CompareTo(x);
    }

    internal static class ServerTypes
    {
        public const int Lobby = 0;
        public const int Login = 1;
        public const int World = 2;
    }

    internal static class DialogTypes
    {
        public const int FUNCTION_DIALOG = -1;
        public const int SIMPLE_DIALOG = 0;
        public const int OPTIONS_DIALOG = 2;
        public const int INPUT_DIALOG = 4;
        public const int JUMP_DIALOG = 8;
    }

    public enum MessageType
    {
        Whisper = 0,
        System = 1,
        SystemOverhead = 3,
        SlateScrollbar = 9,
        Slate = 10,
        Group = 11,
        Guild = 12,
        Overhead = 18
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
        public class MultiIndexDictionary<TKey1, TKey2, TValue>
        {
            private Dictionary<TKey1, KeyValuePair<TKey2, TValue>> _dict1;
            private Dictionary<TKey2, KeyValuePair<TKey1, TValue>> _dict2;

            public MultiIndexDictionary()
            {
                _dict1 = new Dictionary<TKey1, KeyValuePair<TKey2, TValue>>();
                _dict2 = new Dictionary<TKey2, KeyValuePair<TKey1, TValue>>();
            }

            public int Count => _dict1.Count;

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

            public bool ContainsKey(TKey1 k1) => _dict1.ContainsKey(k1);

            public bool ContainsKey(TKey2 k2) => _dict2.ContainsKey(k2);

            public bool Remove(TKey1 k1)
            {
                if (_dict1.ContainsKey(k1))
                {
                    var k2obj = _dict1[k1];
                    return _dict1.Remove(k1) && _dict2.Remove(k2obj.Key);
                }

                return false;
            }

            public bool Remove(TKey2 k2)
            {
                if (_dict2.ContainsKey(k2))
                {
                    var k1obj = _dict2[k2];
                    return _dict2.Remove(k2) && _dict1.Remove(k1obj.Key);
                }

                return false;
            }

            public bool TryGetValue(TKey1 k1, out TValue value)
            {
                value = default;
                if (_dict1.TryGetValue(k1, out var kvp))
                {
                    value = kvp.Value;
                    return true;
                }

                return false;
            }

            public bool TryGetValue(TKey2 k2, out TValue value)
            {
                value = default;
                if (_dict2.TryGetValue(k2, out var kvp))
                {
                    value = kvp.Value;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        ///     A class to allow easy grabbing of assembly info; we use this in various places to
        ///     display uniform version / copyright info.
        ///     This code is modified slightly from Henning Dieterichs original class @
        ///     codeproject.com/Tips/353819/Get-all-Assembly-Information
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
                    var result = string.Empty;
                    var version = Assembly.GetName().Version;
                    if (version != null)
                        return version.ToString();
                    return "1.3.3.7";
                }
            }

            public string Copyright
            {
                get { return GetAttributeValue<AssemblyCopyrightAttribute>(resolveFunc: a => a.Copyright); }
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
                var attributes = Assembly.GetCustomAttributes(typeof(TAttr), false);
                if (attributes.Length > 0)
                    return resolveFunc((TAttr)attributes[0]);
                return defaultResult;
            }
        }

        /// <summary>
        ///     A prettyprinter for objects that don't have a direct string representation.
        /// </summary>
        public static class PrettyPrinter
        {
            /// <summary>
            ///     Pretty print an object, which is essentially a dump of its properties, at the moment.
            /// </summary>
            /// <param name="obj">The object to be pretty printed, using Hybrasyl.Utility.Logger.</param>
            public static void PrettyPrint(object obj)
            {
                GameLog.DebugFormat("object dump follows");
                try
                {
                    foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
                    {
                        var name = descriptor.Name;
                        var value = descriptor.GetValue(obj);
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
        ///     Extension methods for the Type class
        /// </summary>
        public static class TypeExtensions
        {
            /// <summary>
            ///     Return true if the type is a System.Nullable wrapper of a value type
            /// </summary>
            /// <param name="type">The type to check</param>
            /// <returns>True if the type is a System.Nullable wrapper</returns>
            public static bool IsNullable(this Type type) =>
                type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(Nullable<>);

            /// <summary>
            ///     Return true if the type is an integer of any size.
            /// </summary>
            /// <param name="value">The value to check</param>
            /// <returns>True if the type is sbyte, byte, short, ushort, int, uint, long, ulong.</returns>
            public static bool IsInteger(this object value) =>
                value is sbyte || value is byte || value is short || value is ushort || value is int ||
                value is uint ||
                value is long || value is ulong;
        } // end TypeExtensions

        public static class StringExtensions
        {
            public static bool Contains(this string source, string toCheck, StringComparison comparision) =>
                source?.IndexOf(toCheck, comparision) >= 0;

            public static string Capitalize(this string s) =>
                 string.IsNullOrEmpty(s) ? string.Empty : string.Concat(s[0].ToString().ToUpper(), s.AsSpan(1));

            public static string Normalize(string key) => Regex.Replace(key.ToLower(), @"\s+", "");
        }
    } // end Namespace:Utility
} // end Namespace: Hybrasyl