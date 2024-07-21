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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hybrasyl.Subsystems.Spawning;

//This class is defined to control the mob spawning thread.

internal class MonolithControl
{
    internal MonolithControl()
    {
        _maps = Game.World.WorldState.Values<MapObject>().ToList();
    }

    private IEnumerable<MapObject> _maps { get; set; }

    public void Start()
    {
        var x = 0;
        while (true)
        {
            // Ignore processing if no one is logged in, what's the point

            try
            {
                foreach (var map in _maps)
                {
                    if (map.Users.Count == 0) continue;

                    foreach (var obj in map.Objects.Where(predicate: x => x is Monster).ToList())
                        if (obj is Monster { Active: true } mob)
                            Evaluate(mob, map);
                }
            }
            catch (Exception e)
            {
                GameLog.Fatal("Monolith thread error: {e}", e);
            }

            Thread.Sleep(1000);
            x++;
            // Refresh our list every 15 seconds in case of XML reloading
            if (x != 15) continue;
            _maps = Game.World.WorldState.Values<MapObject>().ToList();
            x = 0;
        }
    }


    private static void Evaluate(Monster monster, MapObject map)
    {
        if (!(monster.LastAction < DateTime.Now.AddMilliseconds(-monster.ActionDelay))) return;

        if (monster.Stats.Hp == 0 || monster.AiDisabled)
            return;

        if (map.Users.Count == 0)
            // Mobs on empty maps don't move, it's a waste of time
            return;
        if (!World.ControlMessageQueue.IsCompleted)
            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.MonolithControl, monster, map));
    }
}