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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using Creature = Hybrasyl.Xml.Creature;

namespace Hybrasyl;

//This class is defined to control the mob spawning thread.
internal class Monolith
{
    private readonly ConcurrentDictionary<string, SpawnGroup> Spawns;

    internal Monolith()
    {
        Spawns = new ConcurrentDictionary<string, SpawnGroup>();
    }

    // This is to support multiple instance of maps in the future, potentially with instancing
    public static string Instance => "main";

    public static string SpawnMapKey(ushort id) => $"hyb-{Instance}-{id}";

    public void LoadSpawns(Map map)
    {
        if (map.SpawnDirectives?.Spawns == null) return;

        var spawnlist = new List<Spawn>();
        foreach (var spawn in map.SpawnDirectives.Spawns)
            // This references another group
            if (!string.IsNullOrEmpty(spawn.Import))
            {
                if (Game.World.WorldData.TryGetValue(spawn.Import, out SpawnGroup group))
                    // TODO: make recursive; this only supports one level of importing for now
                    foreach (var importedSpawn in group.Spawns.Where(predicate: x => string.IsNullOrEmpty(x.Import))
                                 .ToList())
                    {
                        importedSpawn.Loot = group.Loot;
                        spawnlist.Add(importedSpawn);
                    }
                else
                    GameLog.SpawnWarning($"Map {map.Name}: spawn import {spawn.Import} not found");
            }
            else // Direct reference to a creature spawn
            {
                spawnlist.Add(spawn);
            }

        if (string.IsNullOrEmpty(map.SpawnDirectives.Name))
            map.SpawnDirectives.Name = SpawnMapKey(map.Id);
        map.SpawnDirectives.Spawns = spawnlist;
        map.SpawnDirectives.MapId = map.Id;
        Spawns.TryAdd(map.SpawnDirectives.Name, map.SpawnDirectives);
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
            try
            {
                if (World.ControlMessageQueue.IsCompleted)
                    break;
                foreach (var key in Spawns.Keys)
                    if (Spawns.TryGetValue(key, out var group))
                        Spawn(group);
                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                GameLog.SpawnFatal($"Unhandled exception {ex} in spawn thread");
            }
    }

    public void Spawn(SpawnGroup spawnGroup)
    {
        if (!Game.World.WorldData.TryGetValue(spawnGroup.MapId, out Map spawnmap))
        {
            GameLog.SpawnWarning($"Map id {spawnGroup.MapId}: not found");
            return;
        }

        //    if (spawnGroup.Spawns.Count == 0)
        //GameLog.SpawnWarning($"Spawngroup {spawnGroup.Name}: no spawns?");

        foreach (var spawn in spawnGroup.Spawns)
        {
            if (spawnmap.SpawnDebug)
                GameLog.SpawnInfo($"Spawngroup {spawnGroup.Name}: {spawn.Name} processing");
            var monsters = spawnmap.Monsters;

            // If the map is disabled, or we don't have a spec for our spawning, or the individual spawn
            // previously had errors and was disabled - continue on
            if (spawnmap.SpawningDisabled || spawn.Disabled)
            {
                GameLog.SpawnWarning(
                    $"Spawngroup {spawnGroup.Name}, map {spawnmap.Name}: spawn disabled or map spawning disabled");
                continue;
            }

            if (spawn.Spec is null)
            {
                GameLog.SpawnWarning(
                    $"Spawngroup {spawnGroup.Name}, map {spawnmap.Name}: no spec defined for spawning");
                continue;
            }

            var formeval = new FormulaEvaluation
            {
                Map = spawnmap,
                XmlSpawn = spawn,
                SpawnGroup = spawnGroup
            };

            // Set some reasonable defaults.
            //
            // If there is no maximum specified, we consider an appropriate maximum
            // to be 1/10th of total number of map tiles for any given mob (maximum of 30) spawned
            // at a default interval of every 30 seconds, with (maxcount/5) spawned
            // per tick.
            // We take Coordinates into account here since we always want to have the number of
            // monsters expected.

            var maxcount = Math.Max(Math.Min(20, spawnmap.X * spawnmap.Y / 30), spawn.Coordinates.Count);
            var interval = 30;
            var maxPerInterval = maxcount / 5;
            var baseLevel = 0;

            try
            {
                if (!string.IsNullOrEmpty(spawn.Spec.MaxCount))
                    maxcount = (int) FormulaParser.Eval(spawn.Spec.MaxCount, formeval);
                if (!string.IsNullOrEmpty(spawn.Spec.Interval))
                    interval = (int) FormulaParser.Eval(spawn.Spec.Interval, formeval);
                if (!string.IsNullOrEmpty(spawn.Spec.MaxPerInterval))
                    maxPerInterval = (int) FormulaParser.Eval(spawn.Spec.MaxPerInterval, formeval);

                // If the spawn itself has a level defined, evaluate and use it; otherwise,
                // the spawn group (imported, or in the map itself) should define a base level
                if (string.IsNullOrEmpty(spawn.Base?.Level))
                    baseLevel = (int) FormulaParser.Eval(spawnGroup.BaseLevel, formeval);
                else
                    baseLevel = (int) FormulaParser.Eval(spawn.Base.Level, formeval);
            }
            catch (Exception e)
            {
                spawn.Disabled = true;
                spawn.ErrorMessage = $"Spawn disabled due to formula evaluation exception: {e}";
                GameLog.SpawnError("Spawn {spawn} on map {map} disabled due to exception: {ex}", spawn.Name,
                    spawnmap.Name, e);
                continue;
            }

            var currentCount = monsters.Where(predicate: x => x.Name == spawn.Name).ToList().Count();

            if (currentCount >= maxcount)
            {
                if (spawnmap.SpawnDebug)
                    GameLog.SpawnInfo(
                        $"Spawn: {spawnmap.Name}: not spawning {spawn.Name} - mob count is {currentCount}, maximum is {maxcount}");
                continue;
            }

            var since = (DateTime.Now - spawn.LastSpawn).TotalSeconds;

            if (since < interval)
            {
                if (spawnmap.SpawnDebug)
                    GameLog.SpawnInfo(
                        $"Spawn: {spawnmap.Name}: not spawning {spawn.Name} - last spawn was {since} ago, interval {interval}");
                continue;
            }

            // Now spawn stuff

            for (var x = 0; x <= Math.Min(maxcount - currentCount, maxPerInterval); x++)
                if (Game.World.WorldData.TryGetValue(spawn.Name, out Creature creature))
                {
                    var newSpawnLoot = LootBox.CalculateLoot(spawn.Loot);
                    newSpawnLoot += LootBox.CalculateLoot(creature.Loot);
                    newSpawnLoot += LootBox.CalculateLoot(spawnGroup.Loot);

                    var baseMob = new Monster(creature, spawn.Flags, (byte) baseLevel,
                        newSpawnLoot);

                    if (baseMob.LootableXP == 0)
                        // If no XP defined, prepopulate based on defaults.
                        // TODO: another place a hardcoded formula should be elsewhere
                        // This is most simply expressed as "amount between mob level and last level times .7%"
                        baseMob.LootableXP = Convert.ToUInt32((Math.Pow(baseMob.Stats.Level, 3) * 250 -
                                                               Math.Pow(baseMob.Stats.Level - 1, 3) * 250) * 0.007);
                    // Is this a strong or weak mob?
                    if (spawn.Base.StrongChance > 0 || spawn.Base.WeakChance > 0)
                    {
                        // TODO: potentially refactor with xml control. This defaults to 3-15%
                        // modifications randomly
                        var modifier = Math.Min(.03, Random.Shared.NextDouble() * .15);
                        var mobtype = Random.Shared.NextDouble() * 100;

                        if (mobtype <= spawn.Base.StrongChance + spawn.Base.WeakChance)
                        {
                            if (spawn.Base.StrongChance >= spawn.Base.WeakChance)
                            {
                                if (mobtype <= spawn.Base.WeakChance)
                                {
                                    baseMob.ApplyModifier(modifier * -1);
                                    if (spawnmap.SpawnDebug)
                                        GameLog.SpawnInfo($"Mob is weak: modifier {modifier}");
                                }
                                else
                                {
                                    baseMob.ApplyModifier(modifier);
                                    if (spawnmap.SpawnDebug)
                                        GameLog.SpawnInfo($"Mob is strong: modifier {modifier}");
                                }
                            }
                            else
                            {
                                if (mobtype <= spawn.Base.StrongChance)
                                {
                                    baseMob.ApplyModifier(modifier);
                                    if (spawnmap.SpawnDebug)
                                        GameLog.SpawnInfo($"Mob is strong: modifier {modifier}");
                                }
                                else
                                {
                                    baseMob.ApplyModifier(modifier * -1);
                                    if (spawnmap.SpawnDebug)
                                        GameLog.SpawnInfo($"Mob is weak: modifier {modifier}");
                                }
                            }
                        }
                    }

                    var mob = (Monster) baseMob.Clone();
                    var xcoord = 0;
                    var ycoord = 0;

                    if (spawn.Coordinates.Any())
                        foreach (var coord in spawn.Coordinates)
                        {
                            if (spawnmap.EntityTree.GetObjects(new Rectangle(coord.X, coord.Y, 1, 1))
                                .Any(predicate: e => e is Objects.Creature)) continue;
                            xcoord = coord.X;
                            ycoord = coord.Y;
                        }
                    else
                        do
                        {
                            xcoord = Random.Shared.Next(0, spawnmap.X);
                            ycoord = Random.Shared.Next(0, spawnmap.Y);
                        } while (spawnmap.IsWall[xcoord, ycoord]);

                    baseMob.X = (byte) xcoord;
                    baseMob.Y = (byte) ycoord;

                    if (spawn.Hostility != null)
                        baseMob.Hostility = spawn.Hostility;

                    if (spawn.Damage != null)
                    {
                        ushort minDmg = 0;
                        ushort maxDmg = 0;

                        try
                        {
                            minDmg = (ushort) FormulaParser.Eval(spawn.Damage.MinDmg, formeval);
                            maxDmg = (ushort) FormulaParser.Eval(spawn.Damage.MaxDmg, formeval);

                            if (minDmg > 0)
                                // They need some kind of weapon
                                if (Game.World.WorldData.TryGetValueByIndex("monsterblade", out Item template))
                                {
                                    var newTemplate = template.Clone();
                                    template.Properties.Damage.Small.Min = minDmg;
                                    template.Properties.Damage.Small.Max = maxDmg;
                                    template.Properties.Damage.Large.Min = minDmg;
                                    template.Properties.Damage.Large.Max = maxDmg;
                                    template.Properties.Physical.Durability = uint.MaxValue / 10;
                                    baseMob.Stats.OffensiveElementOverride = spawn.Damage.Element switch
                                    {
                                        ElementType.RandomFour => (ElementType) Random.Shared.Next(1,
                                            5), // earth/fire/wind/water
                                        ElementType.RandomEight => (ElementType) Random.Shared.Next(1,
                                            9), // Above plus light/dark/metal/wood
                                        ElementType.Random => (ElementType) Random.Shared.Next(1,
                                            10), // Above plus undead
                                        _ => spawn.Damage.Element
                                    };

                                    var item = new ItemObject(newTemplate);
                                    baseMob.Equipment.Insert((byte) ItemSlots.Weapon, item);
                                }
                        }
                        catch (Exception e)
                        {
                            spawn.Disabled = true;
                            spawn.ErrorMessage = $"Spawn disabled due to formula evaluation exception: {e}";
                            GameLog.SpawnError("Spawn {spawn} on map {map} disabled due to exception: {ex}", spawn.Name,
                                spawnmap.Name, e);
                            continue;
                        }
                    }

                    if (spawn.Defense != null)
                    {
                        sbyte Ac = 0;
                        sbyte Mr = 0;

                        try
                        {
                            Ac = (sbyte) FormulaParser.Eval(spawn.Defense.Ac, formeval);
                            Mr = (sbyte) FormulaParser.Eval(spawn.Defense.Mr, formeval);
                        }
                        catch (Exception e)
                        {
                            spawn.Disabled = true;
                            spawn.ErrorMessage = $"Spawn disabled due to formula evaluation exception: {e}";
                            GameLog.SpawnError("Spawn {spawn} on map {map} disabled due to exception: {ex}", spawn.Name,
                                spawnmap.Name, e);
                            continue;
                        }

                        baseMob.Stats.BonusAc = Ac;
                        baseMob.Stats.BonusMr = Mr;
                        baseMob.Stats.DefensiveElementOverride = spawn.Defense.Element switch
                        {
                            ElementType.RandomFour => (ElementType) Random.Shared.Next(1, 5), // earth/fire/wind/water
                            ElementType.RandomEight => (ElementType) Random.Shared.Next(1,
                                9), // Above plus light/dark/metal/wood
                            ElementType.Random => (ElementType) Random.Shared.Next(1, 10), // Above plus undead
                            _ => spawn.Damage.Element
                        };
                    }

                    foreach (var cookie in spawn.SetCookies) baseMob.SetCookie(cookie.Name, cookie.Value);
                    SpawnMonster(baseMob, spawnmap);
                }
                else
                {
                    GameLog.SpawnWarning("Map {map}: Spawn {spawn} not found", spawnmap.Name, spawn.Name);
                }

            spawn.LastSpawn = DateTime.Now;
        }
    }

    private static void SpawnMonster(Monster monster, Map map)
    {
        if (!World.ControlMessageQueue.IsCompleted)
            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MonolithSpawn, monster, map));
    }
}

internal class MonolithControl
{
    internal MonolithControl()
    {
        _maps = Game.World.WorldData.Values<Map>().ToList();
    }

    private IEnumerable<Map> _maps { get; set; }

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
                        if (obj is Monster mob)
                            if (mob.Active)
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