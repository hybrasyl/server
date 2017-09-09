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
                foreach (var map in _maps)
                {
                    foreach (var mob in map.EntityTree.GetAllObjects().OfType<Monster>())
                    {
                        Evaluate(mob, map);
                        
                    }
                }
            }
        }


        private static void Evaluate(Monster monster, Map map)
        {
            if (monster.IsHostile)
            {
                var entityTree = map.EntityTree.GetObjects(monster.GetViewport());
                var hasPlayer = entityTree.Any(x => x is User);

                if (hasPlayer)
                {
                    //get players
                    var players = entityTree.OfType<User>();

                    //get closest
                    var closest =
                        players.OrderBy(x => Math.Sqrt((Math.Pow(monster.X - x.X, 2) + Math.Pow(monster.Y - x.Y, 2))))
                            .FirstOrDefault();

                    if (closest != null)
                    {

                        //pathfind or cast if far away
                        var distanceX = (int)Math.Sqrt(Math.Pow(monster.X - closest.X, 2));
                        var distanceY = (int)Math.Sqrt(Math.Pow(monster.Y - closest.Y, 2));
                        if (distanceX > 1 || distanceY > 1)
                        {
                            var nextAction = _random.Next(1, 3);

                            if (nextAction == 1)
                            {
                                //pathfind;
                                if (distanceX > distanceY)
                                {
                                    monster.Walk(monster.X > closest.X ? Direction.West : Direction.East);
                                }
                                else
                                {
                                    //movey
                                    monster.Walk(monster.Y > closest.Y ? Direction.South : Direction.North);
                                }
                            }
                            else
                            {
                                //cast
                                monster.Shout("I SHOULD BE CASTING RIGHT NOW!");
                            }
                        }
                        else
                        {
                            //check facing and attack or cast

                            var nextAction = _random.Next(1, 3);
                            if (nextAction == 1)
                            {
                                var facing = monster.CheckFacing(monster.Direction, closest);
                                if (facing)
                                {
                                    monster.AssailAttack(monster.Direction, closest);
                                }
                            }
                            else
                            {
                                monster.Shout("I WANT TO CAST BUT ITS NOT IMPLEMENTED YET!");
                            }
                        }
                    }
                }
            }
            if (monster.ShouldWander)
            {
                var nextAction = _random.Next(0, 2);

                if (nextAction == 1)
                {
                    var nextMove = _random.Next(0, 4);
                    switch (nextMove)
                    {
                        case 0:
                            monster.Walk(Direction.East);
                            break;
                        case 1:
                            monster.Walk(Direction.West);
                            break;
                        case 2:
                            monster.Walk(Direction.North);
                            break;
                        case 3:
                            monster.Walk(Direction.South);
                            break;
                    }
                }
                else
                {
                    var nextMove = _random.Next(0, 4);
                    switch (nextMove)
                    {
                        case 0:
                            monster.Turn(Direction.East);
                            break;
                        case 1:
                            monster.Turn(Direction.West);
                            break;
                        case 2:
                            monster.Turn(Direction.North);
                            break;
                        case 3:
                            monster.Turn(Direction.South);
                            break;
                    }
                }
            }
        }
    }
}
