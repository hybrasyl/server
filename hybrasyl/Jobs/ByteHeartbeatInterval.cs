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

namespace Hybrasyl.Jobs;

public static class ByteHeartbeatJob
{
    public static readonly int Interval = Constants.BYTE_HEARTBEAT_INTERVAL;

    public static void Execute(object obj, ElapsedEventArgs args)
    {
        try
        {
            GameLog.Debug("Job starting");

            foreach (var client in GlobalConnectionManifest.WorldClients.Values)
                // Send the 0x3B heartbeat to logged in clients
                client.SendByteHeartbeat();
            GameLog.Debug("Job complete");
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error("Exception occured in job:", e);
        }
    }
}