using System;
using System.Collections.Generic;
using System.Linq;
using App.Metrics.ReservoirSampling.Uniform;
using Hybrasyl.Xml;
using MoonSharp.Interpreter;

namespace Hybrasyl.Objects;

public class ThreatEntry : IComparable
{
    public Guid Target { get; set; }
    public Creature TargetObject => Game.World.WorldData.GetWorldObject<Creature>(Target);
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

    public ThreatEntry(Guid id)
    {
        Target = id;
    }

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
public class ThreatInfo
{
    public Guid Owner { get; set; }
    public Creature OwnerObject => Game.World.WorldData.GetWorldObject<Creature>(Owner);

    public Creature HighestThreat => ThreatTableByThreat.Count == 0 ? null : Game.World.WorldData.GetWorldObject<Creature>(ThreatTableByThreat.First().Value);
 
    public ThreatEntry HighestThreatEntry => ThreatTableByThreat.First().Key;

    public List<Creature> GetTargets(CreatureTargetPriority priority)
    {
        var ret = new List<Creature>();
        ThreatEntry entry;
        switch (priority)
        {
            case CreatureTargetPriority.HighThreat:
                ret.Add(Game.World.WorldData.GetWorldObject<Creature>(ThreatTableByThreat.First().Value));
                break;
            case CreatureTargetPriority.LowThreat:
                ret.Add(Game.World.WorldData.GetWorldObject<Creature>(ThreatTableByThreat.First().Value));
                break;
            case CreatureTargetPriority.Attacker:
                 entry = ThreatTableByThreat.Keys.OrderByDescending(x => x.SecondsSinceLastMelee)
                    .FirstOrDefault();
                if (entry != null)
                    ret.Add(entry.TargetObject);
                break;
            case CreatureTargetPriority.AttackingCaster:
                entry = ThreatTableByThreat.Keys.Where(x => x.IsCaster).OrderByDescending(x => x.SecondsSinceLastNonHealCast)
                    .FirstOrDefault();
                if (entry != null)
                    ret.Add(entry.TargetObject);
                break;
            case CreatureTargetPriority.AttackingGroup:
                entry = ThreatTableByThreat.Keys.OrderByDescending(x => x.SecondsSinceLastMelee)
                    .FirstOrDefault();
                if (entry != null)
                    ret.Add(entry.TargetObject);
                break;
            case CreatureTargetPriority.AttackingHealer:
                entry = ThreatTableByThreat.Keys.Where(x => x.IsHealer).OrderByDescending(x => x.SecondsSinceLastHeal)
                    .FirstOrDefault();
                if (entry != null)
                    ret.Add(entry.TargetObject);
                break;
            case CreatureTargetPriority.AllAllies:
                ret.AddRange(OwnerObject.Map.EntityTree.GetObjects(OwnerObject.GetViewport()).OfType<Monster>().Select(x => x as Creature).ToList());
                break;
            case CreatureTargetPriority.RandomAlly:
                ret.Add(OwnerObject.Map.EntityTree.GetObjects(OwnerObject.GetViewport()).OfType<Monster>().Select(x => x as Creature).PickRandom());
                break;
            case CreatureTargetPriority.RandomAttacker:
                ret.Add(Game.World.WorldData.GetWorldObject<Creature>(ThreatTableByCreature.PickRandom().Key));
                break;
            case CreatureTargetPriority.Self:
                ret.Add(OwnerObject);
                break;
            default:
                break;

        }

        return ret;
    }

    public int Count => ThreatTableByCreature.Count;
    public Dictionary<Guid, ThreatEntry> ThreatTableByCreature { get; private set; }
    public SortedDictionary<ThreatEntry, Guid> ThreatTableByThreat { get; private set; }

    public ThreatInfo(Guid id)
    {
        ThreatTableByCreature = new Dictionary<Guid, ThreatEntry>();
        ThreatTableByThreat = new SortedDictionary<ThreatEntry, Guid>();
        Owner = id;
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
        var entry = new ThreatEntry(newThreat.Guid) {Threat = amount};
        ThreatTableByCreature.Add(newThreat.Guid, entry);
        ThreatTableByThreat.Add(entry, newThreat.Guid);
    }

    public void RemoveThreat(Creature threat)
    {
        if (!ThreatTableByCreature.TryGetValue(threat.Guid, out ThreatEntry entry)) return;
        ThreatTableByCreature.Remove(threat.Guid);
        ThreatTableByThreat.Remove(entry);
        GameLog.Error($"{OwnerObject.Id}: Removed threat {threat.Id}");
    }

    public void RemoveAllThreats()
    {
        ThreatTableByCreature.Clear();
        ThreatTableByThreat.Clear();
    }

    public bool ContainsThreat(Creature threat) => ThreatTableByCreature.ContainsKey(threat.Guid);


    public bool ContainsAny(List<User> users) => users.Any(user => ThreatTableByCreature.ContainsKey(user.Guid));
    
    public void OnRangeExit(Creature threat) => RemoveThreat(threat);

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
        if (ThreatTableByCreature.TryGetValue(threat.Guid, out ThreatEntry entry))
        {
            if (HighestThreat == threat)
                return;
            entry.Threat = (uint) (HighestThreatEntry.Threat * 1.10);
        }
        else
            AddNewThreat(threat, (uint) (HighestThreatEntry.Threat * 1.10));
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

    public uint this[Creature threat]
    {
        get => ThreatTableByCreature.TryGetValue(threat.Guid, out ThreatEntry entry) ? entry.Threat : 0;
        set {
            if (ThreatTableByCreature.TryGetValue(threat.Guid, out ThreatEntry entry))
                entry.Threat = value;
            else
                AddNewThreat(threat, value);
        }
    }
}