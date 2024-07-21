// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Extensions
{
    public static class StringExtensions
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

    namespace Utility
    {
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
                    return resolveFunc((TAttr) attributes[0]);
                return defaultResult;
            }
        }

        public static class StringExtensions
        {
            public static bool Contains(this string source, string toCheck, StringComparison comparision) =>
                source?.IndexOf(toCheck, comparision) >= 0;

            public static string Capitalize(this string s) =>
                string.IsNullOrEmpty(s) ? string.Empty : string.Concat(s[0].ToString().ToUpper(), s.AsSpan(1));

            public static string Normalize(string key) => Regex.Replace(key.ToLower(), @"\s+", "");
        }

        public static class DirectionExtensions
        {
            public static Direction Opposite(this Direction direction)
            {
                return direction switch
                {
                    Direction.North => Direction.South,
                    Direction.South => Direction.North,
                    Direction.East => Direction.West,
                    Direction.West => Direction.East,
                    _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
                };
            }

            public static Direction LeftOf(this Direction direction) => Opposite(RightOf(direction));

            public static Direction RightOf(this Direction direction)
            {
                return direction switch
                {
                    Direction.North => Direction.East,
                    Direction.South => Direction.West,
                    Direction.East => Direction.South,
                    Direction.West => Direction.North,
                    _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
                };
            }
        }

        public static class IntentDirectionExtensions
        {
            public static Direction Resolve(this IntentDirection intent, Direction direction)
            {
                return intent switch
                {
                    IntentDirection.Front => direction,
                    IntentDirection.Back => direction.Opposite(),
                    IntentDirection.Left => direction.LeftOf(),
                    IntentDirection.Right => direction.RightOf(),
                    IntentDirection.None => direction,
                    _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, null)
                };
            }
        }
    } // end Namespace:Utility
} // end Namespace: Hybrasyl