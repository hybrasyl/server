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

using System;
using System.Collections.ObjectModel;
using System.Text;

namespace hybrasyl.Util
{
    public static class Generator
    {
        public static Random Random { get; private set; }
        public static Collection<int> GeneratedNumbers { get; private set; }
        public static Collection<string> GeneratedStrings { get; private set; }

        static Generator()
        {
            Generator.Random = new Random();
            Generator.GeneratedNumbers = new Collection<int>();
            Generator.GeneratedStrings = new Collection<string>();
        }

        public static int GenerateNumber()
        {
            int id;

            do
            {
                id = Random.Next();
            }
            while (Generator.GeneratedNumbers.Contains(id));

            return id;
        }
        public static object Generate<T>(int min, int max)
        {
            string name = typeof(T).Name.ToLower();

            if (name == "byte")
                return (byte)Generator.Random.Next(min, max);
            if (name == "int")
                return GenerateNumber();
            if (name == "uint")
                return (uint)Generator.Random.Next(min, max);

            return null;
        }
        public static string CreateString(int size)
        {
            var value = new StringBuilder();

            for (var i = 0; i < size; i++)
            {
                var binary = Generator.Random.Next(0, 2);

                switch (binary)
                {
                    case 0:
                        value.Append(Convert.ToChar(Generator.Random.Next(65, 91)));
                        break;

                    case 1:
                        value.Append(Generator.Random.Next(1, 10));
                        break;
                }
            }

            return value.ToString();
        }
        public static string GenerateString(int size)
        {
            string s;

            do
            {
                s = Generator.CreateString(size);
            }
            while (Generator.GeneratedStrings.Contains(s));

            Generator.GeneratedStrings.Add(s);

            return s;
        }
    }
}