using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hybrasyl.Objects
{
    public class ThreatInfo
    {
        public Creature ThreatTarget => ThreatTable.Count > 0 ? ThreatTable.Aggregate((l, r) => l.Value > r.Value ? l : r).Key : null;

        public Dictionary<Creature, uint> ThreatTable { get; private set; }

        public ThreatInfo()
        {
            ThreatTable = new Dictionary<Creature, uint>();
        }

        public void IncreaseThreat(Creature threat, uint amount)
        {
            ThreatTable[threat] += amount;
        }

        public void DecreaseThreat(Creature threat, uint amount)
        {
            ThreatTable[threat] -= amount;
        }

        public void WipeThreat(Creature threat)
        {
            ThreatTable[threat] = 0;
        }

        public void AddNewThreat(Creature newThreat, uint amount = 0)
        {
            ThreatTable.Add(newThreat, amount);
        }

        public void RemoveThreat(Creature threat)
        {
            ThreatTable.Remove(threat);
        }

        public void RemoveAllThreats()
        {
            ThreatTable = new Dictionary<Creature, uint>();
        }

        public bool ContainsThreat(Creature threat)
        {
            return ThreatTable.ContainsKey(threat);
        }

        public bool ContainsAny(List<User> users)
        {
            foreach (var user in users)
            {
                if (ThreatTable.ContainsKey(user))
                {
                    return true;
                }
            }
            return false;
        }

        public void OnRangeExit(Creature threat)
        {
            if (ContainsThreat(threat))
            {
                ThreatTable.Remove(threat);
            }
        }

        public void OnRangeEnter(Creature threat)
        {
            if (threat is User userThreat)
            {
                if (ThreatTarget != null)
                {
                    if (ThreatTarget is User user)
                    {
                        if (user.Group.Members.Contains(userThreat))
                        {
                            AddNewThreat(userThreat);
                        }
                    }
                }
                else
                {
                    AddNewThreat(userThreat, 1);
                }
            }
        }

        public void ForceThreatChange(Creature threat)
        {
            if (threat is User userThreat)
            {
                if (ThreatTarget is User user)
                {
                    if (user.Grouped && user.Group.Members.Contains(userThreat))
                    {
                        var newTopThreat = (uint)Math.Ceiling(ThreatTable[ThreatTarget] * 1.1);
                        if (ContainsThreat(userThreat))
                        {
                            ThreatTable[threat] = newTopThreat;
                        }
                        else
                        {
                            AddNewThreat(userThreat, newTopThreat);
                        }
                    }
                    else
                    {
                        RemoveAllThreats();
                        AddNewThreat(threat, 1);
                    }
                }
                else
                {
                    AddNewThreat(threat, 1);
                }
            }

        }

        public void OnNearbyHeal(Creature threat, uint amount)
        {
            if (threat is User user)
            {
                if (ContainsThreat(user))
                {
                    IncreaseThreat(threat, amount);
                    return;
                }

                if (user.Grouped && ContainsAny(user.Group.Members))
                {
                    AddNewThreat(threat, amount);
                    return;
                }
            }
        }

        public uint this[Creature threat]
        {
            get { return ThreatTable[threat]; }
            set { ThreatTable[threat] = value; }
        }
    }
}
