using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Formulas;
using Hybrasyl.Subsystems.Loot;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Subsystems.Spawning;

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

    public void LoadSpawns(MapObject map)
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
        map.SpawnDirectives.Status = new SpawnStatus();
        if (Spawns.ContainsKey(map.SpawnDirectives.Name))
        {
            GameLog.Error($"Duplicate spawngroup (ignored): map {map.Name}, spawngroup {map.SpawnDirectives.Name}");
            return;
        }

        Spawns.TryAdd(map.SpawnDirectives.Name, map.SpawnDirectives);
        GameLog.Debug($"Active spawn for {map.Name}: {map.SpawnDirectives.Name}");
    }

    public void Start()
    {
        foreach (var spawnmap in Game.World.WorldState.Values<MapObject>())
        {
            if (spawnmap.SpawningDisabled)
                continue;
            LoadSpawns(spawnmap);
        }

        while (true)
        {
            if (World.ControlMessageQueue.IsCompleted)
                break;
            foreach (var (key, spawngroup) in Spawns)
            {
                spawngroup.Status ??= new SpawnStatus();
                try
                {
                    Spawn(spawngroup);
                    spawngroup.Status.LastSpawnTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    GameLog.SpawnFatal($"Unhandled exception {ex} in spawn thread");
                    spawngroup.Status.ErrorCount++;
                    spawngroup.Status.LastErrorTime = DateTime.Now;
                    spawngroup.Status.LastException = ex;
                    if (spawngroup.Status.ErrorCount > 5)
                    {
                        GameLog.SpawnError($"Spawngroup {spawngroup.Name} disabled due to errors");
                        spawngroup.Disabled = true;
                    }
                }
            }

            Thread.Sleep(1000);
        }
    }

    public void Spawn(SpawnGroup spawnGroup)
    {
        if (!Game.World.WorldState.TryGetValue(spawnGroup.MapId, out MapObject spawnmap))
        {
            GameLog.SpawnWarning($"Spawngroup {spawnGroup.Name}: Map {spawnGroup.MapId} not found, disabling group");
            spawnGroup.Status.Disabled = true;
            return;
        }

        foreach (var spawn in spawnGroup.Spawns)
        {
            spawn.Status ??= new SpawnStatus();
            if (spawnmap.SpawnDebug)
                GameLog.SpawnInfo($"Spawngroup {spawnGroup.Name}: {spawn.Name} processing");

            // If the map is disabled, or we don't have a spec for our spawning, or the individual spawn
            // previously had errors and was disabled - continue on
            if (spawnmap.SpawningDisabled || spawn.Status.Disabled)
            {
                GameLog.SpawnWarning(
                    $"Spawngroup {spawnGroup.Name}, map {spawnmap.Name}: spawn disabled or map spawning disabled");
                continue;
            }

            if (spawn.Spec is null)
            {
                GameLog.SpawnWarning(
                    $"Spawngroup {spawnGroup.Name}, map {spawnmap.Name}: no spec defined for spawning");
                spawn.Status.Disabled = true;
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
            // monsters expected from coordinate references.


            var maxcount = spawn.Coordinates.Count > 0
                ? spawn.Coordinates.Count
                : Math.Min(20, spawnmap.X * spawnmap.Y / 30);

            var interval = 30;
            var maxPerInterval = maxcount / 5;
            int baseLevel;

            try
            {
                if (!string.IsNullOrEmpty(spawn.Spec.MaxCount) && spawn.Coordinates.Count == 0)
                    maxcount = (int)FormulaParser.Eval(spawn.Spec.MaxCount, formeval);
                if (!string.IsNullOrEmpty(spawn.Spec.Interval))
                    interval = (int)FormulaParser.Eval(spawn.Spec.Interval, formeval);
                if (!string.IsNullOrEmpty(spawn.Spec.MaxPerInterval))
                    maxPerInterval = (int)FormulaParser.Eval(spawn.Spec.MaxPerInterval, formeval);

                // If the spawn itself has a level defined, evaluate and use it; otherwise,
                // the spawn group (imported, or in the map itself) should define a base level
                if (string.IsNullOrEmpty(spawn.Base?.Level))
                    baseLevel = (int)FormulaParser.Eval(spawnGroup.BaseLevel, formeval);
                else
                    baseLevel = (int)FormulaParser.Eval(spawn.Base.Level, formeval);
            }
            catch (Exception ex)
            {
                spawn.Status.Disabled = true;
                spawn.Status.LastException = ex;
                GameLog.SpawnError("Spawn {spawn} on map {map} disabled due to formula evaluation exception: {ex}",
                    spawn.Name,
                    spawnmap.Name, ex);
                continue;
            }

            var currentCount = spawnmap.Monsters.Where(predicate: x => x.Name == spawn.Name).ToList().Count();

            if (currentCount >= maxcount)
            {
                GameLog.SpawnFatal(
                    $"Spawn: {spawnmap.Name}: not spawning {spawn.Name} - mob count is {currentCount}, maximum is {maxcount}");
                if (spawnmap.SpawnDebug)
                    GameLog.SpawnInfo(
                        $"Spawn: {spawnmap.Name}: not spawning {spawn.Name} - mob count is {currentCount}, maximum is {maxcount}");
                continue;
            }


            if (spawn.Status.LastSpawnSeconds < interval)
            {
                if (spawnmap.SpawnDebug)
                    GameLog.SpawnInfo(
                        $"Spawn: {spawnmap.Name}: not spawning {spawn.Name} - last spawn was {spawn.Status.LastSpawnSeconds} ago, interval {interval}");
                continue;
            }

            // Now spawn stuff

            for (var x = 0; x <= Math.Min(maxcount - currentCount, maxPerInterval); x++)
                if (Game.World.WorldData.TryGetValue(spawn.Name, out Creature creature))
                {
                    var newSpawnLoot = LootBox.CalculateLoot(spawn.Loot);
                    newSpawnLoot += LootBox.CalculateLoot(creature.Loot);
                    newSpawnLoot += LootBox.CalculateLoot(spawnGroup.Loot);

                    var baseMob = new Monster(creature, spawn.Flags, (byte)baseLevel,
                        newSpawnLoot);

                    if (baseMob.LootableXp == 0)
                        // If no XP defined, prepopulate based on defaults.
                        // TODO: another place a hardcoded formula should be elsewhere
                        // This is most simply expressed as "amount between mob level and last level times .7%"
                        baseMob.LootableXp = Convert.ToUInt32((Math.Pow(baseMob.Stats.Level, 3) * 250 -
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

                    var mob = (Monster)baseMob.Clone();
                    int xcoord;
                    int ycoord;

                    if (spawn.Coordinates.Count != 0)
                    {
                        var coordinate =
                            spawn.Coordinates.FirstOrDefault(predicate: coord =>
                                spawnmap.IsCreatureAt(coord.X, coord.Y) == false);
                        if (coordinate == null)
                            return;
                        xcoord = coordinate.X;
                        ycoord = coordinate.Y;
                    }
                    else
                    {
                        var tile = spawnmap.FindEmptyTile();
                        if (tile == (-1, -1))
                        {
                            GameLog.SpawnFatal($"{spawnmap.Name}: {spawn.Name} - no empty tiles, aborting");
                            return;
                        }

                        xcoord = (byte)tile.x;
                        ycoord = (byte)tile.y;
                    }


                    baseMob.X = (byte)xcoord;
                    baseMob.Y = (byte)ycoord;

                    if (spawn.Hostility != null)
                        baseMob.Hostility = spawn.Hostility;

                    if (spawn.Damage != null)
                    {
                        ushort minDmg = 0;
                        ushort maxDmg = 0;

                        try
                        {
                            minDmg = (ushort)FormulaParser.Eval(spawn.Damage.MinDmg, formeval);
                            maxDmg = (ushort)FormulaParser.Eval(spawn.Damage.MaxDmg, formeval);

                            if (minDmg > 0)
                                // They need some kind of weapon
                                if (Game.World.WorldData.TryGetValueByIndex("monsterblade", out Item template))
                                {
                                    var newTemplate = template.Clone<Item>();
                                    template.Properties.Damage.SmallMin = minDmg;
                                    template.Properties.Damage.SmallMax = maxDmg;
                                    template.Properties.Damage.LargeMin = minDmg;
                                    template.Properties.Damage.LargeMax = maxDmg;
                                    template.Properties.Physical.Durability = uint.MaxValue / 10;
                                    baseMob.Stats.OffensiveElementOverride = spawn.OffensiveElement;

                                    var item = new ItemObject(newTemplate);
                                    baseMob.Equipment.Insert((byte)ItemSlots.Weapon, item);
                                }
                        }
                        catch (Exception ex)
                        {
                            spawn.Status.Disabled = true;
                            spawn.Status.LastException = ex;
                            GameLog.SpawnError("Spawn {spawn} on map {map} disabled due to exception: {ex}", spawn.Name,
                                spawnmap.Name, ex);
                            continue;
                        }
                    }

                    if (spawn.Defense != null)
                    {
                        sbyte Ac;
                        sbyte Mr;

                        try
                        {
                            Ac = (sbyte)FormulaParser.Eval(spawn.Defense.Ac, formeval);
                            Mr = (sbyte)FormulaParser.Eval(spawn.Defense.Mr, formeval);
                        }
                        catch (Exception ex)
                        {
                            spawn.Status.Disabled = true;
                            spawn.Status.LastException = ex;
                            GameLog.SpawnError("Spawn {spawn} on map {map} disabled due to exception: {ex}", spawn.Name,
                                spawnmap.Name, ex);
                            continue;
                        }

                        baseMob.Stats.BonusAc = Ac;
                        baseMob.Stats.BonusMr = Mr;
                        baseMob.Stats.DefensiveElementOverride = spawn.DefensiveElement;
                    }

                    foreach (var cookie in spawn.SetCookies) baseMob.SetCookie(cookie.Name, cookie.Value);

                    spawnmap.InsertCreature(baseMob);
                }
                else
                {
                    GameLog.SpawnWarning(
                        $"Spawngroup {spawnGroup.Name}: map {spawnmap.Name} Spawn {spawn.Name} not found");
                }

            spawn.Status.LastSpawnTime = DateTime.Now;
        }
    }
}