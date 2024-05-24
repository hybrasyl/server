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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Casting;

public class CastableController : IEnumerable<Rotation>
{
    private readonly HashSet<CreatureCastingSet> CastingSets = new();
    private readonly Dictionary<CreatureCastingSet, Rotation> Rotations = new();
    private readonly HashSet<RotationEntry> ThresholdCasts = new();

    public CastableController(Guid id)
    {
        MonsterGuid = id;
    }

    public BookSlot LastCastableUsed { get; set; }
    public Dictionary<string, BookSlot> Castables { get; set; } = new();

    public Guid MonsterGuid { get; set; }
    public Monster MonsterObj => Game.World.WorldState.GetWorldObject<Monster>(MonsterGuid);
    public bool HasAssailSkills { get; set; }

    public bool Enabled { get; set; }

    private string DebugLogHeader =>
        $"{MonsterObj.Name} ({MonsterObj.Map?.Name ?? "Unknown"}@{MonsterObj.X},{MonsterObj.Y})";

    public IEnumerator<Rotation> GetEnumerator() => Rotations.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool ContainsCastable(string castable) => Castables.ContainsKey(castable);

    public bool TryGetCastable(string castable, out BookSlot slot) => Castables.TryGetValue(castable, out slot);

    /// <summary>
    ///     Given an already specified behaviorset for the monster, learn all the castables possible at
    ///     their level; or the castables specifically enumerated in the set.
    /// </summary>
    public void LearnCastables()
    {
        // All monsters get assail. TODO: hardcoded
        if (Game.World.WorldData.TryGetValueByIndex("Assail", out Castable assail))
            Castables.Add("Assail", new BookSlot { Castable = assail });

        if (MonsterObj?.BehaviorSet == null) return;

        // Default to automatic assignation if unset
        if (MonsterObj.BehaviorSet.Castables.Auto)
        {
            // If categories are present, use those. Otherwise, learn everything we can
            foreach (var category in MonsterObj.BehaviorSet.LearnSpellCategories)
            foreach (var castable in Game.World.WorldData.FindSpells(MonsterObj.Stats.BaseStr,
                         MonsterObj.Stats.BaseInt, MonsterObj.Stats.BaseWis,
                         MonsterObj.Stats.BaseCon, MonsterObj.Stats.BaseDex, category))
                if (!Castables.ContainsKey(castable.Name))
                    Castables.Add(castable.Name, new BookSlot { Castable = castable });

            foreach (var category in MonsterObj.BehaviorSet.LearnSkillCategories)
            foreach (var castable in Game.World.WorldData.FindSkills(MonsterObj.Stats.BaseStr, MonsterObj.Stats.BaseInt,
                         MonsterObj.Stats.BaseWis,
                         MonsterObj.Stats.BaseCon, MonsterObj.Stats.BaseDex, category))
                if (!Castables.ContainsKey(castable.Name))
                    Castables.Add(castable.Name, new BookSlot { Castable = castable });

            if (MonsterObj.BehaviorSet.LearnSkillCategories.Count == 0 &&
                MonsterObj.BehaviorSet.LearnSpellCategories.Count == 0)
                // Auto add according to stats
                foreach (var castable in Game.World.WorldData.FindCastables(MonsterObj.Stats.BaseStr,
                             MonsterObj.Stats.BaseInt, MonsterObj.Stats.BaseWis,
                             MonsterObj.Stats.BaseCon, MonsterObj.Stats.BaseDex))
                    if (!Castables.ContainsKey(castable.Name))
                    {
                        if (castable.IsSkill)
                            Castables.Add(castable.Name, new BookSlot { Castable = castable });
                        else
                            Castables.Add(castable.Name, new BookSlot { Castable = castable });
                    }
        }

        // Handle any specific additions. Note that specific additions *ignore stat requirements*, 
        // to allow a variety of complex behaviors.
        foreach (var castable in MonsterObj.BehaviorSet.Castables.Castable)
            if (Game.World.WorldData.TryGetValueByIndex(castable, out Castable xmlCastable))
            {
                if (Castables.ContainsKey(xmlCastable.Name)) continue;
                if (xmlCastable.IsSkill)
                    Castables.Add(xmlCastable.Name, new BookSlot { Castable = xmlCastable });
                else
                    Castables.Add(xmlCastable.Name, new BookSlot { Castable = xmlCastable });
            }
            else
            {
                GameLog.SpawnError($"{MonsterObj.Name}: Castable {castable} defined, but does not exist");
            }

        foreach (var kvp in Castables)
            GameLog.SpawnInfo($"{MonsterObj.Name}: CastableController: learned {kvp.Key}");
    }


    /// <summary>
    ///     Given a list of creature casting sets, process each one into a rotation.
    /// </summary>
    /// <param name="sets">A list of CreatureCastingSets which will be evaluated and stored as rotations</param>
    public void ProcessCastingSets(List<CreatureCastingSet> sets)
    {
        if (sets.Count == 0)
        {
            Enabled = false;
            return;
        }

        foreach (var set in sets)
        {
            CastingSets.Add(set);
            if (set.Type == RotationType.Assail) HasAssailSkills = true;
            var newRotation = new Rotation(set);
            foreach (var entry in set.Castable)
                if (Castables.TryGetValue(entry.Value, out var slot))
                {
                    if (set.HealthPercentage > 0)
                        entry.HealthPercentage = set.HealthPercentage;
                    var newEntry = new RotationEntry(slot, entry);

                    newRotation.Add(newEntry);

                    if (entry.UseOnce || entry.HealthPercentage > 0)
                        ThresholdCasts.Add(newEntry);
                }
                else
                {
                    GameLog.SpawnError($"{MonsterObj.Name}: processing rotation: missing castable {entry.Value}");
                }

            // Now add categories
            foreach (var category in set.CategoryList)
            {
                var castableMatches = Castables.Where(predicate: x => x.Value.Castable.CategoryList.Contains(category));
                foreach (var match in castableMatches)
                {
                    var directive = new CreatureCastable
                    {
                        Value = match.Value.Castable.Name,
                        HealthPercentage = set.HealthPercentage,
                        Interval = set.Interval,
                        TargetPriority = set.TargetPriority
                    };
                    newRotation.Add(new RotationEntry(match.Value, directive));
                }
            }

            Rotations[set] = newRotation;
            GameLog.SpawnInfo($"{MonsterObj.Name} Rotation resolved to: {Rotations[set]}");
            newRotation.Active = true;
        }
    }

    /// <summary>
    ///     Calculate the next castable to be used.
    /// </summary>
    /// <returns>
    ///     A RotationEntry structure indicating the rotation and castable to be used next (along with targeting), or
    ///     null, if no castable is to be used.
    /// </returns>
    public RotationEntry GetNextCastable(RotationType? type = null)
    {
        // Evaluate all UseOnce castables to see if one has triggered. If it has, return that immediately
        // If no UseOnce triggered, order rotations by priority(SecondsSinceLastUse - Interval) and select top rotation. 
        // For winning rotation, find RotationEntry with GetNextCastable()

        // Rotations have been pre-calculated to include categories / etc, so if there's nothing here we can't do anything

        if (Rotations.Count == 0 || Castables.Count == 0)
        {
            GameLog.SpawnDebug($"{DebugLogHeader}: no rotations exist, or no castables known");
            return null;
        }

        // Find the "most expired" rotation
        var rotations = Rotations.Values.Where(predicate: x => x.Active && x.Priority >= 0);

        if (type != null)
            rotations = rotations.Where(predicate: x => x.Type == type);

        var rotation = rotations.OrderByDescending(keySelector: y => y.Priority).FirstOrDefault();

        if (rotation == null)
        {
            GameLog.SpawnDebug($"{DebugLogHeader}: no active rotations or not enough time elapsed since last use");
            return null;
        }

        // Always handle UseOnce trigger thresholds (Rule #1)
        foreach (var threshold in ThresholdCasts.Where(predicate: c =>
                     c.Directive.HealthPercentage > 0 && c.Directive.HealthPercentage >= MonsterObj.Stats.HpPercentage))
        {
            if (!Castables.ContainsKey(threshold.Name))
                // Threshold references a skill or spell that the mob doesn't know; ignore
                continue;
            // Is this a use once trigger with a percentage defined? If so, it hits and returns immediately IF the
            // corresponding slot hasn't seen a trigger.              
            if (threshold.UseOnce && !threshold.ThresholdTriggered) continue;

            GameLog.SpawnDebug(
                $"{DebugLogHeader}: one-time threshold triggered: {threshold.Name}, {threshold.Threshold}%, priority {threshold.CurrentPriority}");
            threshold.ThresholdTriggered = true;
            return threshold;
        }

        // Monsters have to be active longer than the casting time of a castable in order to use it, exception is assail rotations
        if (type == RotationType.Assail) return rotation.CurrentCastable;
        return MonsterObj.ActiveSeconds > rotation.CurrentCastable.CastingTime ? rotation.CurrentCastable : null;
    }

    public bool CanCast(string castable) => Castables.ContainsKey(castable);

    public Rotation GetNextRotation()
    {
        return Rotations.Count == 0
            ? null
            : Rotations.Values.Where(predicate: x => x.Active && x.SecondsSinceLastUse >= x.Interval)
                .OrderByDescending(keySelector: x => x.Priority).FirstOrDefault();
    }

    public Rotation GetAssailRotation()
    {
        return Rotations.Values.FirstOrDefault(predicate: x => x.Type == RotationType.Assail);
    }

    public RotationEntry GetNextAssail() => GetNextCastable(RotationType.Assail);
}