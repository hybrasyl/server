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
using System.Timers;
using Hybrasyl.Objects;

namespace Hybrasyl.Jobs;

public static class AutoSnoreJob
{
    public static readonly int Interval = 10;

    public static void Execute(object obj, ElapsedEventArgs args)
    {
        GameLog.Debug("Job starting");
        try
        {
            // FIXME: make this more efficient / don't break our own conventions
            foreach (var connection in GlobalConnectionManifest.WorldClients)
            {
                var client = connection.Value;
                var connectionId = connection.Key;

                if (client.IsIdle())
                {
                    User user;
                    if (Game.World.WorldData.TryGetValueByIndex(connectionId, out user))
                        user.Motion(16, 120); // send snore effect
                    else
                        GameLog.WarningFormat(
                            "Connection id {0} marked as idle but no corresponding user found...?",
                            connectionId);
                }
            }

            GameLog.Debug("Job complete");
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error("Exception occured in job:", e);
        }
    }
}