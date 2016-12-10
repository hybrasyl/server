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
using System.Threading;
using Hybrasyl.Creatures;
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
        //        private readonly Dictionary<int, SpawnGroup> _spawnGroups;
        //        private readonly Dictionary<string, Map> _maps;
        //        private readonly Dictionary<string, Creature> _creatures;


        internal Monolith()
        {
  //          _spawnGroups = Game.World.WorldData.GetDictionary<SpawnGroup>();
    //        _maps = Game.World.MapCatalog;
      //      _creatures = Game.World.WorldData.Values<Creature>();
            _random = new Random();
        }

        public void Start()
        {
            try
            {
                foreach (var map in _spawnGroups.SelectMany(spawnGroup => spawnGroup.Maps))
                {
                    //set extension properties on startup
                    map.Id = Game.World.WorldData.Values<Map>().Single(x => x.Name == map.Name).Id;
                    map.LastSpawn = DateTime.Now;
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
            catch (Exception)
            {

                throw;
            }
        }
    

        public void Spawn(SpawnGroup spawnGroup)
        {
            foreach (var map in spawnGroup.Maps)
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
                        xcoord = _random.Next(0, spawnMap.X -1);
                        ycoord = _random.Next(0, spawnMap.Y -1);
                    } while (spawnMap.IsWall[xcoord, ycoord]);
                    mob.X = (byte) xcoord;
                    mob.Y = (byte) ycoord;
                    mob.Id = Convert.ToUInt32(_random.Next(0, int.MaxValue - 1));
                    SpawnMonster(mob, spawnMap);
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
}
