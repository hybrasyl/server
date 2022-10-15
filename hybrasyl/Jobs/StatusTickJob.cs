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
using Hybrasyl.Objects;

namespace Hybrasyl.Jobs;

public static class StatusTickJob
{
    public static readonly int Interval = 1;

    public static void Execute(object obj, ElapsedEventArgs args)
    {
        GameLog.Debug("Status tick job starting");
        foreach (var connectionId in GlobalConnectionManifest.WorldClients.Keys)
        {
            User user;
            if (Game.World.WorldData.TryGetValueByIndex(connectionId, out user))
                if (user.ActiveStatusCount > 0 && user.Condition.Alive)
                    World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.StatusTick, user.Id));
        }

        foreach (var wobj in Game.World.ActiveStatuses)
            if (wobj is Creature creature)
                if (creature.Condition.Alive)
                    World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.StatusTick, wobj.Id));
                else
                    Game.World.ActiveStatuses.Remove(wobj);
        GameLog.Debug("Status tick job ending");
    }
}