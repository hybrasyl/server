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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015-2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

 using Hybrasyl.Objects;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
 using System.Runtime.CompilerServices;
 using System.Threading;
 using System.Threading.Tasks;
 using Community.CsharpSqlite;
 using Hybrasyl.Creatures;
 using Hybrasyl.Enums;
 using Creature = Hybrasyl.Creatures.Creature;

namespace Hybrasyl
{
    //This class is defined to control the mob spawning thread.
    internal class Monolith
    {
        private static readonly ManualResetEvent AcceptDone = new ManualResetEvent(false);
        private static Random _random;

        public static readonly ILog Logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private IEnumerable<SpawnGroup> _spawnGroups => Game.World.WorldData.Values<SpawnGroup>();
        private IEnumerable<Map> _maps => Game.World.WorldData.Values<Map>();
        private IEnumerable<Creature> _creatures => Game.World.WorldData.Values<Creature>();


        internal Monolith()
        {
            _random = new Random();
        }

        public void Start()
        {
            foreach (var spawngroup in _spawnGroups)
            {
                foreach (var spawnmap in spawngroup.Maps)
                {
                    var mapObject = Game.World.WorldData.Values<Map>().SingleOrDefault(x => x.Name == spawnmap.Name);
                    if (mapObject is null)
                    {
                        Logger.Error($"Spawngroup {spawngroup.Filename} references non-existent map {spawnmap.Name}, disabling");
                        spawnmap.Disabled = true;
                        continue;
                    }
                    spawnmap.Id = mapObject.Id;
                    spawnmap.LastSpawn = DateTime.Now;
                }
            }

            while (true)
            {
                foreach (var spawnGroup in _spawnGroups)
                {
                    Spawn(spawnGroup);
                    Thread.Sleep(100);
                }
            }
        }
    

        public void Spawn(SpawnGroup spawnGroup)
        {
            foreach (var map in spawnGroup.Maps.Where(x => x.Disabled != true))
            {
                try
                {
                    var spawnMap = Game.World.WorldData.Get<Map>(map.Id);
                    var monsterList = spawnMap.Objects.OfType<Monster>().ToList();
                    var monsterCount = monsterList.Count;

                    if (monsterCount > map.Limit) continue;
                    if (!(map.LastSpawn.AddSeconds(map.Interval) < DateTime.Now)) continue;

                    map.LastSpawn = DateTime.Now;

                    var thisSpawn = _random.Next(map.MinSpawn, map.MaxSpawn);

                    for (var i = 0; i < thisSpawn; i++)
                    {
                        var idx = _random.Next(0, spawnGroup.Spawns.Count - 1);
                        var spawn = spawnGroup.Spawns[idx];
                        var creature = _creatures.Single(x => x.Name == spawn.Base);

                        var baseMob = new Monster(creature, spawn, map.Id);
                        var mob = (Monster)baseMob.Clone();

                        var xcoord = 0;
                        var ycoord = 0;
                        do
                        {
                            xcoord = _random.Next(0, spawnMap.X - 1);
                            ycoord = _random.Next(0, spawnMap.Y - 1);
                        } while (spawnMap.IsWall[xcoord, ycoord]);
                        mob.X = (byte)xcoord;
                        mob.Y = (byte)ycoord;
                        mob.Id = Convert.ToUInt32(_random.Next(0, int.MaxValue - 1));
                        SpawnMonster(mob, spawnMap);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"Spawngroup {spawnGroup.Filename}: disabled map {map.Name} due to error {e.ToString()}");
                    map.Disabled = true;
                    continue;
                }
            }
        }
        private static void SpawnMonster(Monster monster, Map map)
        {
            World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MonolithSpawn, monster, map));
            //Game.World.Maps[mapId].InsertCreature(monster);
            //Logger.DebugFormat("Spawning monster: {0} at {1}, {2}", monster.Name, (int) monster.X, (int) monster.Y);
        }
    }

    internal class MonolithControl
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private IEnumerable<Map> _maps => Game.World.WorldData.Values<Map>();
        private static Random _random;

        internal MonolithControl()
        {
            _random = new Random();
        }
        
        public void Start()
        {
            while (true)
            {
                var mapsWithUsers = _maps.Where(x => x.EntityTree.GetAllObjects().OfType<User>().Any());
                foreach (var map in mapsWithUsers)
                {
                    var mobsToEval = from monsters in map.EntityTree.GetAllObjects().OfType<Monster>()
                        join users in map.EntityTree.GetAllObjects().OfType<User>() on monsters.Map equals users.Map
                        where monsters.GetViewport().IntersectsWith(users.GetViewport())
                        select monsters;

                    foreach (var mob in mobsToEval)
                    {
                        Evaluate(mob, map);
                    }
                    Thread.Sleep(1000);
                }
            }
        }


        private static void Evaluate(Monster monster, Map map)
        {
            if (!(monster.LastAction < DateTime.Now.AddMilliseconds(-monster.ActionDelay))) return;

            var mapTree = map.EntityTree.GetAllObjects();
            var mapPlayers = mapTree.Any(x => x is User);
            if (!mapPlayers) return;

            World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MonolithControl, monster, map));

        }
    }
}
