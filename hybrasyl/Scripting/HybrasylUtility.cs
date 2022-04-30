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
using Hybrasyl.Messaging;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using Hybrasyl.Enums;

namespace Hybrasyl.Scripting;

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

    /// <summary>
    /// Calculate the number of minutes (float) between two Unix timestamps t1 and t2.
    /// </summary>
    /// <param name="t1">First timestamp</param>
    /// <param name="t2">Second timestamp</param>
    /// <returns></returns>
    public static long MinutesBetweenUnixTimes(long t1, long t2) => ((t2 - t1) / 60);

    /// <summary>
    /// Calculate the number of hours (float) between two Unix timestamps represented as strings.
    /// </summary>
    /// <param name="t1">First timestamp</param>
    /// <param name="t2">Second timestamp</param>
    /// <returns></returns>
    public static long MinutesBetweenUnixTimes(string t1, string t2)
    {
        if (string.IsNullOrEmpty(t1) || string.IsNullOrEmpty(t2))
        {
            GameLog.ScriptingError("MinutesBetweenUnixTimes: t1 (first argument) or t2 (second argument) was null or empty, returning 0");
            return 0;
        }
        try
        {
            return (Convert.ToInt64(t2) - Convert.ToInt64(t1)) / 60;
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.ScriptingError("MinutesBetweenUnixTimes: Exception occurred doing time conversion, returning 0 - {exception}", e);
            return 0;
        }
    }

    public static bool SendMail(string to, string from, string subject, string body)
    {
        User userObj;
        if (!Game.World.TryGetActiveUser(to, out userObj) &&
            !Game.World.WorldData.TryGetUser(to, out userObj))
            return false;

        var ret = userObj.Mailbox.ReceiveMessage(new Message(to, from, subject, body));
        if (ret)
            userObj.Mailbox.Save();
        if (!userObj.AuthInfo.IsLoggedIn) return ret;
        userObj.UpdateAttributes(StatUpdateFlags.UnreadMail);
        return ret;
    }

    public static bool SendParcel(string to, string from, string itemName, int quantity = 1)
    {
        User userObj;
        if (!Game.World.TryGetActiveUser(to, out userObj) &&
            !Game.World.WorldData.TryGetUser(to, out userObj))
            return false;
        if (!Game.World.WorldData.TryGetValueByIndex(itemName, out Item _))
            return false;
        var mboxString = Game.World.GetLocalString("send_parcel_mailbox_message", ("$SENDER", from),
            ("$ITEM", $"{itemName} (qty {quantity})"));

        userObj.Mailbox.ReceiveMessage(new Message(to, from, Game.World.GetLocalString("send_parcel_mailbox_subject", ("$NAME", from)), mboxString));
        userObj.ParcelStore.AddItem(from, itemName, (uint) quantity);
        userObj.ParcelStore.Save();
        if (userObj.AuthInfo.IsLoggedIn)
        {
            userObj.SendSystemMessage(Game.World.GetLocalString("send_parcel_system_msg", ("$NAME", from)));
            userObj.UpdateAttributes(StatUpdateFlags.UnreadMail);
        }

        userObj.ParcelStore.AddItem(from, itemName, (uint)quantity);
        return true;
    }

}