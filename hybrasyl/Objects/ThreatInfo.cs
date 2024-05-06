using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Serilog;

namespace Hybrasyl.Objects;

public class ThreatEntry(Guid id) : IComparable
{
    public Guid Target { get; set; } = id;
    public Creature TargetObject => Game.World.WorldState.GetWorldObject<Creature>(Target);
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

    public int CompareTo(object e)
    {
        if (e == null) return 1;
        if (!(e is ThreatEntry other))
            throw new ArgumentException("Object is not a ThreatEntry");
        if (Threat == other.Threat)
            return 0;
        if (Threat > other.Threat)
            return 1;
        return -1;
    }
}

[MoonSharpUserData]
public class ThreatInfo(Guid id)
{
    public Guid Owner { get; set; } = id;
    public Creature OwnerObject => Game.World.WorldState.GetWorldObject<Creature>(Owner);

    public Creature HighestThreat => ThreatTableByThreat.Count == 0
        ? null
        : Game.World.WorldState.GetWorldObject<Creature>(ThreatTableByThreat.First().Value);

    public ThreatEntry HighestThreatEntry => ThreatTableByThreat.First().Key;

    public int Count => ThreatTableByCreature.Count;
    public Dictionary<Guid, ThreatEntry> ThreatTableByCreature { get; } = new();
    public SortedDictionary<ThreatEntry, Guid> ThreatTableByThreat { get; } = new();
    public Creature LastCaster { get; set; }

    public uint this[Creature threat]
    {
        get => ThreatTableByCreature.TryGetValue(threat.Guid, out var entry) ? entry.Threat : 0;
        set
        {
            if (ThreatTableByCreature.TryGetValue(threat.Guid, out var entry))
                entry.Threat = value;
            else
                AddNewThreat(threat, value);
        }
    }

    public List<Creature> GetTargets(CreatureTargetPriority priority)
    {
        var ret = new List<Creature>();
        if (OwnerObject == null) 
            return ret;
        ThreatEntry entry;
        var monstersInViewport = OwnerObject.Map.EntityTree.GetObjects(OwnerObject.GetViewport()).OfType<Monster>().ToList();
        if (OwnerObject.Condition.Charmed)
        {
            switch (OwnerObject.LastTarget)
            {
                // If our immediate target is grouped, add every monster they've collectively targeted to our target list,
                // otherwise add their last target - but make sure not to add ourselves
                case User u1:
                    if (u1.Group != null)
                        ret.AddRange(u1.Group.Members.Where(x=> x.LastTarget != null && x.LastTarget != OwnerObject));
                    else if (u1.LastTarget != OwnerObject)
                        ret.Add(u1.LastTarget);
                    break;
                // If we are already targeting a monster, continue to target it
                case Monster:
                    ret.Add(OwnerObject.LastTarget);
                    break;
                // Add everything targeting the last player to use a spell on this monster
                default:
                {
                    if (LastCaster is User u2)
                        ret.AddRange(monstersInViewport.Where(x => x.ThreatInfo.ContainsThreat(LastCaster)));
                    break;
                }
            }
            // If we still have no targets, or our (singular) target is dead, add every monster in the viewport.
            if (ret.Count == 0 || (ret.Count == 1 && ret.First().Stats.Hp <= 0))
                ret.AddRange(monstersInViewport);
            // Order by distance, take closest first, make sure to not target ourselves
            return ret.OrderBy(x => x.Distance(OwnerObject)).Where(x => x.Guid != Owner).ToList();
        }

        if (ThreatTableByThreat.Count == 0)
            return ret;

        switch (priority)
        {
            case CreatureTargetPriority.HighThreat:
                ret.Add(Game.World.WorldState.GetWorldObject<Creature>(ThreatTableByThreat.First().Value));
                break;
            case CreatureTargetPriority.LowThreat:
                ret.Add(Game.World.WorldState.GetWorldObject<Creature>(ThreatTableByThreat.First().Value));
                break;
            case CreatureTargetPriority.Attacker:
                entry = ThreatTableByThreat.Keys.MaxBy(keySelector: x => x.SecondsSinceLastMelee);
                if (entry != null)
                    ret.Add(entry.TargetObject);
                break;
            case CreatureTargetPriority.AttackingCaster:
                entry = ThreatTableByThreat.Keys.Where(predicate: x => x.IsCaster).MaxBy(keySelector: x => x.SecondsSinceLastNonHealCast);
                if (entry != null)
                    ret.Add(entry.TargetObject);
                break;
            case CreatureTargetPriority.AttackingGroup:
                entry = ThreatTableByThreat.Keys.MaxBy(keySelector: x => x.SecondsSinceLastMelee);
                if (entry != null)
                    ret.Add(entry.TargetObject);
                break;
            case CreatureTargetPriority.AttackingHealer:
                entry = ThreatTableByThreat.Keys.Where(predicate: x => x.IsHealer).MaxBy(keySelector: x => x.SecondsSinceLastHeal);
                if (entry != null)
                    ret.Add(entry.TargetObject);
                break;
            case CreatureTargetPriority.AllAllies:
                ret.AddRange(OwnerObject.Map.EntityTree.GetObjects(OwnerObject.GetViewport()).OfType<Monster>()
                    .Select(selector: x => x as Creature).ToList());
                break;
            case CreatureTargetPriority.RandomAlly:
                ret.Add(OwnerObject.Map.EntityTree.GetObjects(OwnerObject.GetViewport()).OfType<Monster>()
                    .Select(selector: x => x as Creature).PickRandom());
                break;
            case CreatureTargetPriority.RandomAttacker:
                ret.Add(Game.World.WorldState.GetWorldObject<Creature>(ThreatTableByCreature.PickRandom().Key));
                break;
            case CreatureTargetPriority.Self:
                ret.Add(OwnerObject);
                break;
        }

        return ret;
    }

    public void IncreaseThreat(Creature threat, uint amount)
    {
        if (!ThreatTableByCreature.ContainsKey(threat.Guid))
            AddNewThreat(threat, amount);
        ThreatTableByCreature[threat.Guid].Threat += amount;
    }

    public void DecreaseThreat(Creature threat, uint amount)
    {
        if (ThreatTableByCreature.ContainsKey(threat.Guid))
            ThreatTableByCreature[threat.Guid].Threat -= amount;
    }

    public void ClearThreat(Creature threat)
    {
        if (ThreatTableByCreature.ContainsKey(threat.Guid))
            ThreatTableByCreature[threat.Guid].Threat = 0;
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
        // TODO: review / refactor
        if (threat is not User userThreat) return;
        if (HighestThreat is User user && user.Group.Members.Contains(userThreat))
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
            entry.Threat = (uint)(HighestThreatEntry.Threat * 1.10);
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
        else if (threat is User user && user.Grouped && ContainsAny(user.Group.Members))
            AddNewThreat(threat, amount);
        var entry = ThreatTableByCreature[threat.Guid];
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
        else if (user.Grouped && ContainsAny(user.Group.Members))
        {
            AddNewThreat(threat, amount);
            return;
        }

        var entry = ThreatTableByCreature[threat.Guid];
        entry.TotalHeals++;
        entry.LastHeal = DateTime.Now;
    }
}