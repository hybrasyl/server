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

using System.Timers;

namespace Hybrasyl.Jobs;

public static class ShutdownJob
{
    public static readonly int Interval = 10;

    public static void Execute(object obj, ElapsedEventArgs args)
    {
        if (Game.ShutdownTimeRemaining == -1) return; // No shutdown yet requested

        if (Game.ShutdownTimeRemaining == 0)
        {
            // Shutdown has arrived
            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.ShutdownServer, "job", 0));
            return;
        }

        if (Game.ShutdownTimeRemaining <= 300)
        {
            // Send message every minute
            if (Game.ShutdownTimeRemaining % 60 == 0)
                foreach (var user in Game.World.ActiveUsers)
                    user.SendSystemMessage($"Chaos will be rising up in {Game.ShutdownTimeRemaining / 60} minute(s).");
        }
        else if (Game.ShutdownTimeRemaining > 600)
        {
            // Send message every five minutes
            if (Game.ShutdownTimeRemaining % 300 == 0)
                foreach (var user in Game.World.ActiveUsers)
                    user.SendSystemMessage($"Chaos will be rising up in {Game.ShutdownTimeRemaining % 60} minutes.");
        }

        Game.ShutdownTimeRemaining -= Interval;
    }
}