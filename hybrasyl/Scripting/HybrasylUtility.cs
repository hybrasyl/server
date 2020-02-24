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

using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Scripting
{
    /// <summary>
    /// A variety of utility functions for scripts that are statically accessible from a global `utility` object.
    /// </summary>
    
    [MoonSharpUserData]
    public static class HybrasylUtility
    {
        public static int GetCurrentHour() => DateTime.Now.Hour;
        public static int GetCurrentDay() => DateTime.Now.Day;
        public static long GetUnixTime() => new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();
        public static long HoursBetweenUnixTimes(long t1, long t2) => ((t2 - t1) / 3600);
        public static long HoursBetweenUnixTimes(string t1, string t2) => (Convert.ToInt64(t2) - Convert.ToInt64(t1)) / 3600;
    }
}
