using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Objects;

public class ThreatEntry : IComparable
{
    public uint Threat { get; set; }
    public bool IsHealer => TotalHeals > 0;
    public bool IsCaster => TotalCasts > 0;
    public int TotalHeals { get; set; }
    public int TotalCasts { get; set; }
        
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

public class ThreatInfo
{
    public Creature HighestThreat
    {
        get
        {
            if (ThreatTableByThreat.Count > 0)
            {
                return ThreatTableByThreat.Max().Value;
            }
            else return null;
        }
    }

    public Creature LowestThreat
    {
        get
        {
            if (ThreatTableByThreat.Count > 0)
            {
                return ThreatTableByThreat.Min().Value;
            }
            return null;
        }
    }

    public Creature HighestThreatCaster
    {
        get
        {
            if (ThreatTableByThreat.Count > 0)
            {
                var topCaster = ThreatTableByThreat.Where(x => x.Key.IsCaster == true).Max(x => x.Key);
            }
            return null;
        }
    }

    public Creature HighestThreatHealer
    {
        get
        {
            if (ThreatTableByThreat.Count > 0)
            {
                var topCaster = ThreatTableByThreat.Where(x => x.Key.IsHealer == true).Max(x => x.Key);
            }
            return null;
        }
    }

    public int Count => ThreatTableByCreature.Count;
    public Dictionary<Creature, ThreatEntry> ThreatTableByCreature { get; private set; }
    public SortedDictionary<ThreatEntry, Creature> ThreatTableByThreat { get; private set; }

    public ThreatInfo()
    {
        ThreatTableByCreature = new Dictionary<Creature, ThreatEntry>();
        ThreatTableByThreat = new SortedDictionary<ThreatEntry, Creature>();
    }

    public void IncreaseThreat(Creature threat, uint amount)
    {
        if (!ThreatTableByCreature.ContainsKey(threat))
            AddNewThreat(threat, amount);
        ThreatTableByCreature[threat].Threat += amount;
    }

    public void DecreaseThreat(Creature threat, uint amount)
    {
        if (ThreatTableByCreature.ContainsKey(threat))
            ThreatTableByCreature[threat].Threat -= amount;
    }

    public void ClearThreat(Creature threat)
    {
        if (ThreatTableByCreature.ContainsKey(threat))
            ThreatTableByCreature[threat].Threat = 0;
    }

    public void AddNewThreat(Creature newThreat, uint amount = 0)
    {
        var entry = new ThreatEntry() { Threat = amount };
        ThreatTableByCreature.Add(newThreat, entry);
        ThreatTableByThreat.Add(entry, newThreat);
    }

    public void RemoveThreat(Creature threat)
    {
        if (ThreatTableByCreature.TryGetValue(threat, out ThreatEntry entry))
        {
            ThreatTableByCreature.Remove(threat);
            ThreatTableByThreat.Remove(entry);
        }
    }

    public void RemoveAllThreats()
    {
        ThreatTableByCreature = new Dictionary<Creature, ThreatEntry>();
        ThreatTableByThreat = new SortedDictionary<ThreatEntry, Creature>();
    }

    public bool ContainsThreat(Creature threat) => ThreatTableByCreature.ContainsKey(threat);


    public bool ContainsAny(List<User> users)
    {
        foreach (var user in users)
        {
            if (ThreatTableByCreature.ContainsKey(user))
            {
                return true;
            }
        }
        return false;
    }

    public void OnRangeExit(Creature threat) => RemoveThreat(threat);

    public void OnRangeEnter(Creature threat)
    {
        // TODO: review / refactor
        if (threat is User userThreat)
        {
            if (HighestThreat is User user && user.Group.Members.Contains(userThreat))
                AddNewThreat(userThreat);
            else
                AddNewThreat(userThreat, 1);
        }
    }

    public void ForceThreatChange(Creature threat)
    {
        if (ThreatTableByCreature.TryGetValue(threat, out ThreatEntry entry))
        {
            if (HighestThreat == threat)
                return;
            entry.Threat = (uint) (ThreatTableByCreature[HighestThreat].Threat * 1.10);
        }
        else
        {
                
        }
    }

    public void OnCast(Creature threat, uint amount = 0)
    {
        if (ContainsThreat(threat))
            IncreaseThreat(threat, amount);
        else if (threat is User user && user.Grouped && ContainsAny(user.Group.Members))
            AddNewThreat(threat, amount);
        var entry = ThreatTableByCreature[threat];
        entry.TotalCasts++;
    }

    public void OnNearbyHeal(Creature threat, uint amount)
    {
        if (threat is User user)
        {
            if (ContainsThreat(user))
            {
                IncreaseThreat(threat, amount);                    
            }
            else if (user.Grouped && ContainsAny(user.Group.Members))
            {
                AddNewThreat(threat, amount);
                return;
            }
            var entry = ThreatTableByCreature[threat];
            entry.TotalHeals++;
        }
    }

    public uint this[Creature threat]
    {
        get {
            if (ThreatTableByCreature.TryGetValue(threat, out ThreatEntry entry))
                return entry.Threat;
            return 0;
        }
        set {
            if (ThreatTableByCreature.TryGetValue(threat, out ThreatEntry entry))
                entry.Threat = value;
            else
                AddNewThreat(threat, value);
        }
    }
}