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

using Hybrasyl.Internals.Logging;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Objects;

public class ThreatEntry(Guid id) : IComparable
{
    public Guid Target { get; set; } = id;

    // Null when the target has left the world (e.g. logout) before its threat entry is cleaned up.
    public Creature? TargetObject => Game.World.WorldState.GetWorldObject<Creature>(Target);
    public uint Threat { get; set; }
    public bool IsHealer => TotalHeals > 0;
    public bool IsCaster => TotalCasts > 0;
    public int TotalHeals { get; set; }
    public int TotalCasts { get; set; }
    public DateTime LastHeal { get; set; }
    public DateTime LastMelee { get; set; }
    public DateTime LastNonHealCast { get; set; }
    public double SecondsSinceLastHeal => (DateTime.Now - LastHeal).TotalSeconds;
    public double SecondsSinceLastMelee => (DateTime.Now - LastMelee).TotalSeconds;
    public double SecondsSinceLastNonHealCast => (DateTime.Now - LastNonHealCast).TotalSeconds;

    public int CompareTo(object? e)
    {
        if (e == null) return 1;
        if (!(e is ThreatEntry other))
            throw new ArgumentException("Object is not a ThreatEntry");
        if (Threat == other.Threat)
            // Distinct targets must never compare equal: SortedDictionary treats
            // compare-0 as the same key, so two creatures entering aggro range with
            // equal threat would collide (and crash the Add).
            return Target.CompareTo(other.Target);
        if (Threat > other.Threat)
            return 1;
        return -1;
    }
}

[MoonSharpUserData]
public class ThreatInfo(Guid id)
{
    public Guid Owner { get; set; } = id;
    public Creature? OwnerObject => Game.World.WorldState.GetWorldObject<Creature>(Owner);

    // ThreatTableByThreat sorts ascending, so Last() is the highest threat.
    public Creature? HighestThreat => ThreatTableByThreat.Count == 0
        ? null
        : Game.World.WorldState.GetWorldObject<Creature>(ThreatTableByThreat.Last().Value);

    public ThreatEntry HighestThreatEntry => ThreatTableByThreat.Last().Key;

    public int Count => ThreatTableByCreature.Count;
    public Dictionary<Guid, ThreatEntry> ThreatTableByCreature { get; } = new();
    public SortedDictionary<ThreatEntry, Guid> ThreatTableByThreat { get; } = new();
    public Creature? LastCaster { get; set; }

    public uint this[Creature threat]
    {
        get => ThreatTableByCreature.TryGetValue(threat.Guid, out var entry) ? entry.Threat : 0;
        set
        {
            if (ThreatTableByCreature.TryGetValue(threat.Guid, out var entry))
                SetThreat(entry, value);
            else
                AddNewThreat(threat, value);
        }
    }

    // Threat is the SortedDictionary comparison key; a key must not change while
    // resident or the tree silently corrupts (wrong ordering, failed removals).
    // Every Threat mutation must go through here: remove under the old value,
    // mutate, reinsert under the new one.
    private void SetThreat(ThreatEntry entry, uint value)
    {
        ThreatTableByThreat.Remove(entry);
        entry.Threat = value;
        ThreatTableByThreat.Add(entry, entry.Target);
    }

    public List<Creature> GetTargets(CreatureTargetPriority priority)
    {
        var ret = new List<Creature>();
        if (OwnerObject is not { } owner || owner.Location.Map is null)
            return ret;
        var monstersInViewport =
            owner.Location.Map.EntityTree.GetObjects(owner.GetViewport()).OfType<Monster>().ToList();
        if (owner.Condition.Charmed)
        {
            switch (owner.LastTarget)
            {
                // If our immediate target is grouped, add every monster they've collectively targeted to our target list,
                // otherwise add their last target - but make sure not to add ourselves
                case User u1:
                    if (u1.Group != null)
                        ret.AddRange(u1.Group.Members.Where(predicate: x =>
                            x.LastTarget != null && x.LastTarget != owner));
                    else if (u1.LastTarget != null && u1.LastTarget != owner)
                        ret.Add(u1.LastTarget);
                    break;
                // If we are already targeting a monster, continue to target it
                case Monster m:
                    ret.Add(m);
                    break;
                // Add everything targeting the last player to use a spell on this monster
                default:
                    {
                        if (LastCaster is User u2)
                            ret.AddRange(monstersInViewport.Where(predicate: x => x.ThreatInfo.ContainsThreat(u2)));
                        break;
                    }
            }

            // If we still have no targets, or our (singular) target is dead, add every monster in the viewport.
            if (ret.Count == 0 || (ret.Count == 1 && ret.First().Stats.Hp <= 0))
                ret.AddRange(monstersInViewport);
            // Order by distance, take closest first, make sure to not target ourselves
            return ret.OrderBy(keySelector: x => x.Distance(owner)).Where(predicate: x => x.Guid != Owner && x.Stats.Hp > 0)
                .ToList();
        }

        if (ThreatTableByThreat.Count == 0)
            return ret;

        ThreatEntry? entry;
        switch (priority)
        {
            case CreatureTargetPriority.HighThreat:
                if (Game.World.WorldState.GetWorldObject<Creature>(ThreatTableByThreat.Last().Value) is { } high)
                    ret.Add(high);
                break;
            case CreatureTargetPriority.LowThreat:
                if (Game.World.WorldState.GetWorldObject<Creature>(ThreatTableByThreat.First().Value) is { } low)
                    ret.Add(low);
                break;
            case CreatureTargetPriority.Attacker:
                entry = ThreatTableByThreat.Keys.MaxBy(keySelector: x => x.SecondsSinceLastMelee);
                if (entry?.TargetObject is { } attackerTarget)
                    ret.Add(attackerTarget);
                break;
            case CreatureTargetPriority.AttackingCaster:
                entry = ThreatTableByThreat.Keys.Where(predicate: x => x.IsCaster)
                    .MaxBy(keySelector: x => x.SecondsSinceLastNonHealCast);
                if (entry?.TargetObject is { } casterTarget)
                    ret.Add(casterTarget);
                break;
            case CreatureTargetPriority.AttackingGroup:
                entry = ThreatTableByThreat.Keys.MaxBy(keySelector: x => x.SecondsSinceLastMelee);
                if (entry?.TargetObject is { } groupTarget)
                    ret.Add(groupTarget);
                break;
            case CreatureTargetPriority.AttackingHealer:
                entry = ThreatTableByThreat.Keys.Where(predicate: x => x.IsHealer)
                    .MaxBy(keySelector: x => x.SecondsSinceLastHeal);
                if (entry?.TargetObject is { } healerTarget)
                    ret.Add(healerTarget);
                break;
            case CreatureTargetPriority.AllAllies:
                ret.AddRange(owner.Location.Map.EntityTree.GetObjects(owner.GetViewport()).OfType<Monster>()
                    .Select(selector: x => x as Creature).ToList());
                break;
            case CreatureTargetPriority.RandomAlly:
                if (Extensions.EnumerableExtension.PickRandom(owner.Location.Map.EntityTree
                        .GetObjects(owner.GetViewport()).OfType<Monster>()) is { } randomAlly)
                    ret.Add(randomAlly);
                break;
            case CreatureTargetPriority.RandomAttacker:
                if (Game.World.WorldState.GetWorldObject<Creature>(Extensions.EnumerableExtension
                        .PickRandom(ThreatTableByCreature).Key) is { } randomAttacker)
                    ret.Add(randomAttacker);
                break;
            case CreatureTargetPriority.AllyWithLowestHp:

                if (owner.Location.Map.EntityTree
                        .GetObjects(owner.GetViewport()).OfType<Monster>().OrderBy(x => x.Stats.Hp)
                        .FirstOrDefault() is { } lowestHpAlly)
                    ret.Add(lowestHpAlly);
                break;
            case CreatureTargetPriority.AllyWithLowestMp:

                if (owner.Location.Map.EntityTree
                        .GetObjects(owner.GetViewport()).OfType<Monster>().OrderBy(x => x.Stats.Mp)
                        .FirstOrDefault() is { } lowestMpAlly)
                    ret.Add(lowestMpAlly);
                break;
            case CreatureTargetPriority.AllyWithHighestHp:

                if (owner.Location.Map.EntityTree
                        .GetObjects(owner.GetViewport()).OfType<Monster>().OrderByDescending(x => x.Stats.Hp)
                        .FirstOrDefault() is { } highestHpAlly)
                    ret.Add(highestHpAlly);
                break;
            case CreatureTargetPriority.AllyWithHighestMp:

                if (owner.Location.Map.EntityTree
                        .GetObjects(owner.GetViewport()).OfType<Monster>().OrderByDescending(x => x.Stats.Mp)
                        .FirstOrDefault() is { } highestMpAlly)
                    ret.Add(highestMpAlly);
                break;
            case CreatureTargetPriority.AllyWithLessThanMaxHp:

                ret.AddRange(owner.Location.Map.EntityTree
                    .GetObjects(owner.GetViewport()).OfType<Monster>().Where(x => x.Stats.Hp < x.Stats.MaximumHp)
                    .OrderBy(x => x.Stats.Hp).Select(x => x as Creature).ToList());
                break;
            case CreatureTargetPriority.AllyWithLessThanMaxMp:

                ret.AddRange(owner.Location.Map.EntityTree
                    .GetObjects(owner.GetViewport()).OfType<Monster>().Where(x => x.Stats.Mp < x.Stats.MaximumMp)
                    .OrderBy(x => x.Stats.Mp).Select(x => x as Creature).ToList());
                break;
            case CreatureTargetPriority.AllyWithStatusConditions:
                ret.AddRange(owner.Location.Map.EntityTree
                    .GetObjects(owner.GetViewport()).OfType<Monster>().Where(x => x.CurrentStatuses.Count > 0)
                    .OrderBy(x => x.Stats.Mp).Select(x => x as Creature).ToList());
                break;
            case CreatureTargetPriority.AllyWithNoStatusConditions:
                ret.AddRange(owner.Location.Map.EntityTree
                    .GetObjects(owner.GetViewport()).OfType<Monster>().Where(x => x.CurrentStatuses.Count == 0)
                    .OrderBy(x => x.Stats.Mp).Select(x => x as Creature).ToList());
                break;
            case CreatureTargetPriority.Self:
                ret.Add(owner);
                break;
        }

        GameLog.Debug("{Name}: priority {Priority}, picked {Target} as target", owner.Name, priority, ret.FirstOrDefault()?.Name ?? "null");
        return ret;
    }

    public void IncreaseThreat(Creature threat, uint amount)
    {
        if (ThreatTableByCreature.TryGetValue(threat.Guid, out var entry))
            SetThreat(entry, entry.Threat + amount);
        else
            AddNewThreat(threat, amount);
    }

    public void DecreaseThreat(Creature threat, uint amount)
    {
        // Saturate at zero; unsigned subtraction would wrap to a huge threat value.
        if (ThreatTableByCreature.TryGetValue(threat.Guid, out var entry))
            SetThreat(entry, amount > entry.Threat ? 0 : entry.Threat - amount);
    }

    public void ClearThreat(Creature threat)
    {
        if (ThreatTableByCreature.TryGetValue(threat.Guid, out var entry))
            SetThreat(entry, 0);
    }

    public void AddNewThreat(Creature newThreat, uint amount = 0)
    {
        var entry = new ThreatEntry(newThreat.Guid) { Threat = amount };
        ThreatTableByCreature.Add(newThreat.Guid, entry);
        ThreatTableByThreat.Add(entry, newThreat.Guid);
    }

    public void RemoveThreat(Creature threat)
    {
        if (!ThreatTableByCreature.TryGetValue(threat.Guid, out var entry)) return;
        ThreatTableByCreature.Remove(threat.Guid);
        ThreatTableByThreat.Remove(entry);
    }

    public void RemoveAllThreats()
    {
        ThreatTableByCreature.Clear();
        ThreatTableByThreat.Clear();
    }

    public bool ContainsThreat(Creature threat) => ThreatTableByCreature.ContainsKey(threat.Guid);


    public bool ContainsAny(List<User> users)
    {
        return users.Any(predicate: user => ThreatTableByCreature.ContainsKey(user.Guid));
    }

    public void OnRangeExit(Creature threat)
    {
        RemoveThreat(threat);
    }

    public void OnRangeEnter(Creature threat)
    {
        if (ThreatTableByCreature.ContainsKey(threat.Guid)) return;
        if (threat is not User userThreat) return;
        if (HighestThreat is User { Group: not null } user && user.Group.Members.Contains(userThreat))
            AddNewThreat(userThreat);
        else
            AddNewThreat(userThreat, 1);
    }

    public void ForceThreatChange(Creature threat)
    {
        if (ThreatTableByCreature.TryGetValue(threat.Guid, out var entry))
        {
            if (HighestThreat == threat)
                return;
            SetThreat(entry, (uint)(HighestThreatEntry.Threat * 1.10));
        }
        else
        {
            AddNewThreat(threat, (uint)(HighestThreatEntry.Threat * 1.10));
        }
    }

    public void OnCast(Creature threat, uint amount = 0)
    {
        if (ContainsThreat(threat))
            IncreaseThreat(threat, amount);
        else if (threat is User user && user.Group is { } group && ContainsAny(group.Members))
            AddNewThreat(threat, amount);
        // Neither branch may have added the caster (ungrouped, not yet a threat).
        if (!ThreatTableByCreature.TryGetValue(threat.Guid, out var entry)) return;
        entry.TotalCasts++;
        entry.LastNonHealCast = DateTime.Now;
    }

    public void OnNearbyHeal(Creature threat, uint amount)
    {
        if (threat is not User user) return;
        if (ContainsThreat(user))
        {
            IncreaseThreat(threat, amount);
        }
        else if (user.Group is { } group && ContainsAny(group.Members))
        {
            AddNewThreat(threat, amount);
            return;
        }

        // The healer may not be a threat at all (ungrouped, never attacked).
        if (!ThreatTableByCreature.TryGetValue(user.Guid, out var entry)) return;
        entry.TotalHeals++;
        entry.LastHeal = DateTime.Now;
    }
}