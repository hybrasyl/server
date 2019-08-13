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
using Hybrasyl.Utility;
using Hybrasyl.Loot;

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
                        //Logger.Error($"Spawngroup {spawngroup.Filename} references non-existent map {spawnmap.Name}, disabling");
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

                        var newSpawnLoot = new SpawnLoot(spawn);
                        mob.LootableGold = newSpawnLoot.LootableGold();
                        mob.LootableItems = newSpawnLoot.LootableItems();

                        SpawnMonster(mob, spawnMap);
                    }
                }
                catch (Exception e)
                {
                    //Logger.Error($"Spawngroup {spawnGroup.Filename}: disabled map {map.Name} due to error {e.ToString()}");
                    map.Disabled = true;
                    continue;
                }
            }
        }
        private static void SpawnMonster(Monster monster, Map map)
        {
            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MonolithSpawn, monster, map));
            //Game.World.Maps[mapId].InsertCreature(monster);
            //Logger.DebugFormat("Spawning monster: {0} at {1}, {2}", monster.Name, (int) monster.X, (int) monster.Y);
        }
    }

    internal class SpawnLoot
    {
        private static Random _rng = new Random();

        private Spawn _spawn;        
        private List<Creatures.LootTable> _spawnLootTable;

        public SpawnLoot(Spawn spawn)
        {
            _spawn = spawn;
            _spawnLootTable = CreateSpawnLootTable();
        }

        private List<Creatures.LootTable> CreateSpawnLootTable()
        {
            var spawnLootTable = new List<Creatures.LootTable>();
            if (_spawn.Loot.Table != null)
            {
                foreach (var lootTbl in _spawn.Loot.Table)
                {
                    for (int i = 0; i < lootTbl.Rolls; i++)
                    {
                        if (lootTbl.Chance >= _rng.NextDouble())
                        {
                            spawnLootTable.Add(lootTbl);
                            continue;
                        }
                    }
                }
            }
            if (_spawn.Loot.Set != null)
            {
                var lootSets = Game.World.WorldData.Values<LootSet>();
                foreach (var set in _spawn.Loot.Set)
                {
                    var lootImportName = set.Name;
                    var lootImportSet = lootSets.Where(lootSet => lootSet.Name == lootImportName);

                    foreach (var importSet in lootImportSet)
                    {
                        foreach (var importTable in importSet.Table)
                        {
                            for (int i = 0; i < importTable.Rolls; i++)
                            {
                                if (importTable.Chance >= _rng.NextDouble())
                                {
                                    spawnLootTable.Add((Creatures.LootTable) importTable);
                                    continue;
                                }
                            }
                        }
                    }
                }

            }
            return spawnLootTable;
        }

        /// <summary>
        /// Calculate the total amount of lootable gold based on the base amount plus any additional from the loot table(s).
        /// </summary>
        /// <returns>Gold uint</returns>
        public uint LootableGold()
        {
            uint lootableGold = 0;
            if(((_spawn.Loot.Gold?.Min ?? 0) < (_spawn.Loot.Gold?.Max ?? 0))) lootableGold += ((uint)_rng.Next((int)(_spawn.Loot.Gold?.Min ?? 0), (int)(_spawn.Loot.Gold?.Max ?? 0)));
            _spawnLootTable.ForEach(lootTable => { if (lootTable.Gold.Min < lootTable.Gold.Max) { lootableGold += ((uint)_rng.Next((int)(lootTable.Gold.Min), (int)(lootTable.Gold.Max))); } });
            
            return lootableGold;
        }

        /// <summary>
        /// Creates a list of LootItems from the LootTables
        /// </summary>
        /// <returns>List of LootItems</returns>
        public List<ItemObject> LootableItems()
        {
            List<ItemObject> lootableItems = new List<ItemObject>();

            if(_spawnLootTable.Count > 0)
            {
                foreach(var table in _spawnLootTable)
                {
                    var numberOfItemsToGet = 0;
                    for(int i = 0; i < table.Items.Rolls; i++)
                    {
                        if(table.Items.Chance >= _rng.NextDouble())
                        {
                            numberOfItemsToGet++;
                        }
                    }

                    if(numberOfItemsToGet > 0)
                    {
                        //now get the number of random items

                        var creaturesLootItem = new List<Creatures.LootItem>();
                        var alwaysItems = new List<Creatures.LootItem>();
                        var possibleItems = new List<Creatures.LootItem>(table.Items.Items);
                        
                        //first determine all "Always" items
                        foreach(var item in possibleItems)
                        {
                            if (item.Always)
                            {
                                alwaysItems.Add(item);
                            }
                        }
                        possibleItems.RemoveAll(item => alwaysItems.Contains(item));

                        //then get all other items, taking into account unique items only drop once
                        while (numberOfItemsToGet > 0)
                        {
                            if(possibleItems.Count > 0)
                            {
                                var randomIndex = _rng.Next(0, possibleItems.Count);
                                var item = possibleItems.ElementAt(randomIndex);

                                if (item.Unique)
                                {
                                    possibleItems.Remove(item);
                                    creaturesLootItem.Add(item);
                                    numberOfItemsToGet--;
                                }
                                else
                                {
                                    creaturesLootItem.Add(item);
                                    numberOfItemsToGet--;
                                }
                            }
                            else
                            {
                                numberOfItemsToGet--;
                            }
                            creaturesLootItem.AddRange(alwaysItems);
                        }

                        var worldItemTemplates = Game.World.WorldData.Values<Items.Item>();


                        foreach (var item in creaturesLootItem)
                        {
                            var possibleItemTemplate = worldItemTemplates.Where(x => (x.IsVariant == true && x.ParentItem.Name == item.Value) || x.Name.Equals(item.Value));
                            var itemVariants = new List<string>(item.Variants);
                            itemVariants.Add("normal");
                            var randomVariant = _rng.Next(0, itemVariants.Count);
                            var randomItemVariant = itemVariants.ElementAt(randomVariant);

                            //Check that Min is less than Max, otherwise only generate one
                            int randomQty;
                            if (item.Min < item.Max && item.Unique == false) randomQty = _rng.Next(item.Min, item.Max);
                            else randomQty = 1;

                            if (randomItemVariant.Equals("normal"))
                            {
                                foreach (var itemTemplate in possibleItemTemplate)
                                {
                                    if (itemTemplate.Name.Equals(item.Value, StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        lootableItems.Add(Game.World.CreateItem(itemTemplate.Id, randomQty));
                                    }
                                }
                            }
                            else
                            {
                                foreach (var itemTemplate in possibleItemTemplate)
                                {
                                    if (itemTemplate.Name.Contains(randomItemVariant, StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        lootableItems.Add(Game.World.CreateItem(itemTemplate.Id, randomQty));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return lootableItems;
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
                // Ignore processing if no one is logged in, what's the point
                var mapsWithUsers = _maps.Where(x => x.Users.Count() > 0);
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

            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MonolithControl, monster, map));

        }
    }
}
