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

using Hybrasyl.Objects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hybrasyl
{

    //This class is defined to control the mob spawning thread.
    internal class Monolith
    {
        private static readonly ManualResetEvent AcceptDone = new ManualResetEvent(false);
        private static Random _random;
        private ConcurrentDictionary<string, Xml.SpawnGroup> Spawns;

        private Map GetMap(ushort id)
        {
            if (Game.World.WorldData.TryGetValue(id, out Map map))
                return map;
            return null;
        }

        internal Monolith()
        {
            _random = new Random();
            Spawns = new ConcurrentDictionary<string, Xml.SpawnGroup>();
        }

        // This is to support multiple instance of maps in the future, potentially with instancing
        public static string Instance => "main";
        public static string SpawnMapKey(ushort id) => $"hyb-{Instance}-{id}";

        public void LoadSpawns(Map map)
        {
            var spawnlist = new List<Xml.Spawn>();

            foreach (var spawn in map.SpawnDirectives.Spawn)
            {
                // This references another group
                if (!string.IsNullOrEmpty(spawn.Import))
                {
                    if (Game.World.WorldData.TryGetValue(spawn.Import, out Xml.SpawnGroup group))
                    {
                        // TODO: make recursive; this only supports one level of importing for now
                        spawnlist.AddRange(group.Spawn.Where(x => string.IsNullOrEmpty(x.Import)).ToList());
                        GameLog.SpawnInfo($"Map {map.Name}: imported {spawn.Import} successfully");
                    }
                    else
                        GameLog.SpawnWarning($"Map {map.Name}: spawn import {spawn.Import} not found");                
                }
                spawnlist.Add(spawn);               
            }
            Spawns.TryAdd(SpawnMapKey(map.Id), new Xml.SpawnGroup() { Spawn = spawnlist });
        }

        public void Start()
        {
            // Resolve active spawns
            foreach (var spawnmap in Game.World.WorldData.Values<Map>())
            {
                if (spawnmap.SpawningDisabled) continue;
                LoadSpawns(spawnmap);             
            }

            while (true)
            {
                if (World.ControlMessageQueue.IsCompleted)
                    break;
                foreach (var key in Spawns.Keys)
                    if (Spawns.TryGetValue(key, out Xml.SpawnGroup group))
                        Spawn(group);
                Thread.Sleep(5000);
            }
        }
    

        public void Spawn(Xml.SpawnGroup spawnGroup)
        {
            if (!Game.World.WorldData.TryGetValue(spawnGroup.MapId, out Map spawnmap))
            {
                GameLog.SpawnWarning($"Map id {spawnGroup.MapId}: not found");
                return;
            }

            foreach (var spawn in spawnGroup.Spawn)
            {
                var monsters = spawnmap.Objects.OfType<Monster>().ToList();

                if (!Game.World.WorldData.TryGetValue(spawn.Name, out Xml.Creature spawnTemplate))
                {
                    GameLog.SpawnWarning($"Map id {spawnGroup.MapId}: spawn {spawn.Name} not found");
                    continue;
                }

                // If the map is disabled, or we don't have a spec for our spawning, or the individual spawn
                // previously had errors and was disabled - continue on
                if (spawnmap.SpawningDisabled || spawn.Spec == null || spawn.Disabled)
                    continue;

                var formeval = new FormulaEvaluation() { Map = spawnmap };

                int limit = 0;
                int interval = 0;
                int maxPerInterval = 0;

                try
                {
                    limit = (int)FormulaParser.Eval(spawn.Spec.Limit, formeval);
                    interval = (int)FormulaParser.Eval(spawn.Spec.Interval, formeval);
                    maxPerInterval = (int)FormulaParser.Eval(spawn.Spec.MaxPerInterval, formeval);
                }
                catch (Exception e)
                {
                    spawn.Disabled = true;
                    spawn.ErrorMessage = $"Spawn disabled due to formula evaluation exception: {e}";
                    GameLog.SpawnError("Spawn {spawn} on map {map} disabled due to exception: {ex}", spawn.Name, spawnmap.Name, e);
                    continue;
                }

                // If there is no limit specified, we want a reasonable default,
                // which we consider to be 1/100th of total number of map tiles for any given mob

                if (limit == 0)
                    limit = spawnmap.X * spawnmap.Y / 100;

                var currentCount = monsters.Where(x => x.Name == spawn.Name).Count();

                if (currentCount >= limit)
                {
                    if (spawnmap.SpawnDebug)
                        GameLog.SpawnInfo($"Spawn: {spawnmap.Name}: not spawning, mob count is {currentCount}, limit is {limit}");
                    continue;
                }

                var since = (DateTime.Now - spawn.LastSpawn).TotalSeconds;

                if (since < interval)
                {
                    if (spawnmap.SpawnDebug) GameLog.SpawnInfo($"Spawn: {spawnmap.Name}: not spawning, last spawn was {since} ago, interval {interval}");
                    continue;
                }

                // Now spawn stuff

                for (var x = 0; x <= (limit - currentCount); x++)
                {
                    if (Game.World.WorldData.TryGetValue(spawn.Name, out Creature creature))
                    {
                        var newSpawnLoot = LootBox.CalculateLoot(spawn);

                        if (spawnmap.SpawnDebug)
                            GameLog.SpawnInfo("Spawn {name}, map {map}: {Xp} xp, {Gold} gold, items {Items}", spawn.Base, 
                                spawnmap.Name, newSpawnLoot.Xp, newSpawnLoot.Gold,
                                string.Join(',', newSpawnLoot.Items));

                        var baseMob = new Monster(creature,  map.Id, newSpawnLoot);
                        var mob = (Monster)baseMob.Clone();
                        var xcoord = 0;
                        var ycoord = 0;

                    }
                    else
                        GameLog.SpawnWarning("Map {map}: Spawn {spawn} not found", spawnmap.Name, spawn.Name);
                }


            }
            

            //        for (var i = 0; i < thisSpawn; i++)
            //        {
            //            var spawn = spawnGroup.Spawns.PickRandom(true);

            //            if (spawn == null)
            //            {
            //                GameLog.SpawnError("Spawngroup empty, skipping");
            //                break;
            //            }

            //            var creature = Creatures.FirstOrDefault(x => x.Name == spawn.Base);

            //            if (creature is default(Xml.Creature))
            //            {
            //                GameLog.SpawnError($"Base monster {spawn.Base} not found");
            //                break;
            //            }
                        
            //            var newSpawnLoot = LootBox.CalculateLoot(spawn);

            //            if (spawnMap.SpawnDebug)
            //                GameLog.SpawnInfo("Spawn {name}, map {map}: {Xp} xp, {Gold} gold, items {Items}", spawn.Base, map.Name, newSpawnLoot.Xp, newSpawnLoot.Gold,
            //                    string.Join(',', newSpawnLoot.Items));

            //            var baseMob = new Monster(creature, spawn, map.Id, newSpawnLoot);
            //            var mob = (Monster)baseMob.Clone();
            //            var xcoord = 0;
            //            var ycoord = 0;

            //            if (map.Coordinates.Count > 0)
            //            {
            //                // TODO: optimize / improve
            //                foreach (var coord in map.Coordinates)
            //                {
            //                    if (spawnMap.EntityTree.GetObjects(new System.Drawing.Rectangle(coord.X, coord.Y, 1, 1)).Where(e => e is Creature).Count() == 0)
            //                    {
            //                        xcoord = coord.X;
            //                        ycoord = coord.Y;
            //                        break;
            //                    }
            //                }                         
            //            }
            //            else
            //            {
            //                do
            //                {
            //                    xcoord = _random.Next(0, spawnMap.X);
            //                    ycoord = _random.Next(0, spawnMap.Y);
            //                } while (spawnMap.IsWall[xcoord, ycoord]);
            //            }
            //            mob.X = (byte)xcoord;
            //            mob.Y = (byte)ycoord;
            //            if (spawnMap.SpawnDebug) GameLog.SpawnInfo($"Spawn: spawning {mob.Name} on {spawnMap.Name}");
            //            SpawnMonster(mob, spawnMap);
            //        }
                   
            //    }
            //    catch (Exception e)
            //    {
            //        Game.ReportException(e);
            //        GameLog.SpawnError(e, "Spawngroup {Filename}: disabled map {Name} due to error", spawnGroup.Filename, map.Name);
            //        map.Disabled = true;
            //        continue;
            //    }
            //}
        }
        private static void SpawnMonster(Monster monster, Map map)
        {
            if (!World.ControlMessageQueue.IsCompleted)
            {
                World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MonolithSpawn, monster, map));
                //Game.World.Maps[mapId].InsertCreature(monster);
                if (map.SpawnDebug)
                    GameLog.SpawnInfo("Spawning monster: {0} {1} at {2}, {3}", map.Name, monster.Name, (int)monster.X, (int)monster.Y);
            }
        }
    }

    internal class MonolithControl
    {
        private IEnumerable<Map> _maps { get; set; }
        private static Random _random;

        internal MonolithControl()
        {
            _random = new Random();
            _maps = Game.World.WorldData.Values<Map>().ToList();
        }
        
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

                        foreach (var obj in map.Objects.Where(x => x is Monster).ToList())
                        {
                            if(obj is Monster mob)
                            {
                                if(mob.Active)
                                {
                                    Evaluate(mob, map);
                                }
                            }
                            
                        }
                    }
                }
                catch (Exception e)
                {
                    GameLog.Fatal("Monolith thread error: {e}", e);
                }
                Thread.Sleep(1000);
                x++;
                // Refresh our list every 15 seconds in case of XML reloading
                if (x == 30)
                {
                    _maps = Game.World.WorldData.Values<Map>().ToList();
                    x = 0;
                }
            }
        }


        private static void Evaluate(Monster monster, Map map)
        {
            if (!(monster.LastAction < DateTime.Now.AddMilliseconds(-monster.ActionDelay))) return;

            if (monster.Stats.Hp == 0 || monster.AiDisabled)
                return;

            if (map.Users.Count == 0)
                // Mobs on empty maps don't move, it's a waste of time
                return;
            if (!World.ControlMessageQueue.IsCompleted)
                World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MonolithControl, monster, map));
        }
    }
}
