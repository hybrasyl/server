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
        /// <summary>
        /// Get the current Terran hour for the local (timezone of the server) time.
        /// </summary>
        /// <returns></returns>
        public static int GetCurrentHour() => DateTime.Now.Hour;
        /// <summary>
        /// Get the current Terran day for the local (timezone of the server) time.
        /// </summary>
        /// <returns></returns>
        public static int GetCurrentDay() => DateTime.Now.Day;
        /// <summary>
        /// Get current Unix time.
        /// </summary>
        /// <returns></returns>
        public static long GetUnixTime() => new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();
        /// <summary>
        /// Calculate the number of hours (float) between two Unix timestamps t1 and t2.
        /// </summary>
        /// <param name="t1">First timestamp</param>
        /// <param name="t2">Second timestamp</param>
        /// <returns></returns>
        public static long HoursBetweenUnixTimes(long t1, long t2) => ((t2 - t1) / 3600);
        /// <summary>
        /// Calculate the number of hours (float) between two Unix timestamps represented as strings.
        /// </summary>
        /// <param name="t1"></param>
        /// <param name="t2"></param>
        /// <returns></returns>
        public static long HoursBetweenUnixTimes(string t1, string t2)
        {
            if (string.IsNullOrEmpty(t1) || string.IsNullOrEmpty(t2))
            {
                GameLog.ScriptingError("HoursBetweenUnixTimes: t1 (first argument) or t2 (second argument) was null or empty, returning 0");
                return 0;
            }
            try
            {
                return (Convert.ToInt64(t2) - Convert.ToInt64(t1)) / 3600;
            }
            catch (Exception e)
            {
                Game.ReportException(e);
                GameLog.ScriptingError("HoursBetweenUnixTimes: Exception occurred doing time conversion, returning 0 - {exception}", e);
                return 0;
            }
        }
    }
}
