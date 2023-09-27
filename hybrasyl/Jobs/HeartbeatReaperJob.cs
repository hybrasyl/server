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

using Hybrasyl.Enums;
using Hybrasyl.Objects;
using System;
using System.Timers;

namespace Hybrasyl.Jobs;

/// <summary>
///     This job reaps connections that haven't responded to either tick or byte based heartbeats
///     in REAP_HEARTBEAT_TIME.
/// </summary>
public static class HeartbeatReaperJob
{
    public static readonly int Interval = Game.ActiveConfiguration.Constants.ReapHeartbeatInterval;

    public static void Execute(object obj, ElapsedEventArgs args)
    {
        GameLog.Debug("Job starting");
        try
        {
            foreach (var connection in GlobalConnectionManifest.WorldClients)
            {
                var client = connection.Value;
                var connectionId = connection.Key;
                User user;
                if (Game.World.WorldState.TryGetValueByIndex(connectionId, out user))
                    if (client.IsHeartbeatExpired())
                    {
                        GameLog.InfoFormat("{0} (connection id {1}: heartbeat expired, disconnecting",
                            user.Name, connectionId);
                        GlobalConnectionManifest.DeregisterClient(client);
                        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.CleanupUser,
                            CleanupType.ByConnectionId, connectionId));
                    }
            }
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.ErrorFormat("Exception occurred in job:", e);
        }
    }
}