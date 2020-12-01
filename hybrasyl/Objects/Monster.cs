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
using System.Threading.Tasks;
using System.Transactions;
using Hybrasyl.Enums;
using Hybrasyl.Scripting;
using Hybrasyl.Utility;

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
            foreach(var user in users)
            {
                if(ThreatTable.ContainsKey(user))
                {
                    return true;
                }
            }
            return false;
        }

        public void OnRangeExit(Creature threat)
        {
            if(ContainsThreat(threat))
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
            if(threat is User user)
            {
                if(ContainsThreat(user))
                {
                    IncreaseThreat(threat, amount);
                    return;
                }

                if(user.Grouped && ContainsAny(user.Group.Members))
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


    public enum MobAction
    {
        Attack,
        Cast,
        Move,
        Idle,
        Death
    }

    public class Monster : Creature, ICloneable
    {
        private readonly object _lock = new object();

        private ConcurrentQueue<MobAction> _actionQueue;

        private static Random Rng = new Random();

        private bool _idle = true;

        private uint _mTarget;

        public List<Xml.Castable> Spells { get; set; } = new List<Xml.Castable>();
        public List<Xml.Castable> Skills { get; set; } = new List<Xml.Castable>();

        public Xml.CreatureBehaviorSet BehaviorSet;

        public Xml.SpawnFlags SpawnFlags;

        public (int X, int Y) Destination;

        public Tile CurrentPath;

        private double _variance;

        public int ActionDelay = 800;

        public DateTime LastAction { get; set; }
        public bool IsHostile { get; set; }
        public bool ShouldWander { get; set; }
        public bool DeathDisabled => SpawnFlags.HasFlag(Xml.SpawnFlags.DeathDisabled);
        public bool MovementDisabled => SpawnFlags.HasFlag(Xml.SpawnFlags.MovementDisabled);
        public bool AiDisabled => SpawnFlags.HasFlag(Xml.SpawnFlags.AiDisabled);
        public bool DeathProcessed { get; set; }

        public bool ScriptExists { get; set; }

        //public Dictionary<string, double> AggroTable { get; set; }
        public ThreatInfo ThreatInfo {get; private set; }

        public bool HasCastNearDeath = false;

        public bool Active = false;


        public bool CanCast => BehaviorSet?.CanCast ?? false;

        public override void OnDeath()
        {
            lock (_lock)
            {
                if (DeathDisabled)
                {
                    Stats.Hp = Stats.MaximumHp;
                    return;
                }

                // Don't die twice
                if (DeathProcessed == true) return;

                // Even if we encounter an error, we still count the death as processed to avoid 
                // repeated processing
                DeathProcessed = true;
                _actionQueue.Clear();

                var hitter = LastHitter as User;
                if (hitter == null)
                {
                    Map.Remove(this);
                    World.Remove(this);
                    GameLog.Error("OnDeath: lasthitter was null");
                    return; // Don't handle cases of MOB ON MOB COMBAT just yet
                }

                try
                {
                    var deadTime = DateTime.Now;

                    if (hitter.Grouped)
                    {
                        ItemDropAllowedLooters = hitter.Group.Members.Select(user => user.Name).ToList();
                        hitter.Group.Members.ForEach(x => x.TrackKill(Name, deadTime));
                    }
                    else
                    {
                        ItemDropAllowedLooters.Add(hitter.Name);
                        hitter.TrackKill(Name, deadTime);
                    }

                    hitter.ShareExperience(LootableXP, Stats.Level);
                    var itemDropTime = DateTime.Now;

                    if (LootableGold > 0)
                    {
                        var golds = new Gold(LootableGold);
                        golds.ItemDropType = ItemDropType.MonsterLootPile;
                        golds.ItemDropAllowedLooters = ItemDropAllowedLooters;
                        golds.ItemDropTime = itemDropTime;
                        World.Insert(golds);
                        Map.Insert(golds, X, Y);
                    }

                    foreach (var itemname in LootableItems)
                    {
                        var item = Game.World.CreateItem(itemname);
                        if (item == null)
                        {
                            GameLog.UserActivityError("User {player}: looting {monster}, loot item {item} is missing", hitter.Name, Name, itemname);
                            continue;
                        }
                        item.ItemDropType = ItemDropType.MonsterLootPile;
                        item.ItemDropAllowedLooters = ItemDropAllowedLooters;
                        item.ItemDropTime = itemDropTime;
                        World.Insert(item);
                        Map.Insert(item, X, Y);
                    }


                }
                catch (Exception e)
                {
                    GameLog.Error("OnDeath for {Name}: exception encountered, loot/gold cancelled {e}", Name, e);
                    Game.ReportException(e);
                }
                Game.World.RemoveStatusCheck(this);
                Map?.Remove(this);
                World?.Remove(this);
            }
        }

        // We follow a different pattern here due to the fact that monsters
        // are not intended to be long-lived objects, and we don't want to 
        // spend a lot of overhead and resources creating a full script (eg via
        // OnSpawn) when not needed 99% of the time.
        private void InitScript()
        {
            if (Script != null || ScriptExists || string.IsNullOrEmpty(Name))               
                return;

            if (Game.World.ScriptProcessor.TryGetScript(Name, out Script damageScript))
            {
                Script = damageScript;
                Script.AssociateScriptWithObject(this);
                ScriptExists = true;
            }
            else
                ScriptExists = false;                
        }

        public override void OnHear(VisibleObject speaker, string text, bool shout = false)
        {
            if (speaker == this)
                return;

            // FIXME: in the glorious future, run asynchronously with locking
            InitScript();
            if (Script != null)
            {
                Script.SetGlobalValue("text", text);
                Script.SetGlobalValue("shout", shout);

                if (speaker is User user)
                    Script.ExecuteFunction("OnHear", new HybrasylUser(user));
                else
                    Script.ExecuteFunction("OnHear", new HybrasylWorldObject(speaker));
            }
        }

        public void MakeHostile()
        {
            ShouldWander = false;
            IsHostile = true;
        }

        public override void OnDamage(Creature attacker, uint damage)
        {
            lock (_lock)
            {
                if (attacker != null)
                {
                    if (!ThreatInfo.ContainsThreat(attacker))
                    {
                        ThreatInfo.AddNewThreat(attacker, damage);
                    }
                    else
                    {
                        ThreatInfo.IncreaseThreat(attacker, damage);
                    }
                }

                Condition.Asleep = false;
                IsHostile = true;
                ShouldWander = false;

                // FIXME: in the glorious future, run asynchronously with locking
                InitScript();

                if (Script != null)
                {
                    Script.SetGlobalValue("damage", damage);
                    Script.ExecuteFunction("OnDamage", this, attacker);
                }
            }
        }

        public override void OnHeal(Creature healer, uint heal)
        {
            // FIXME: in the glorious future, run asynchronously with locking
            InitScript();
            if (Script != null)
            {
                Script.SetGlobalValue("heal", heal);
                Script.ExecuteFunction("OnHeal", this, healer);
            }
        }


        /// <summary>
        /// Calculates a sanity-checked stat using a spawn's variance value.
        /// </summary>
        /// <param name="stat">byte stat to be modified</param>
        /// <returns>new byte stat, +/- variance</returns>
        public byte CalculateVariance(byte stat)
        {
            var newStat = (int)Math.Round(stat + (stat * _variance));
            if (newStat > byte.MaxValue)
                return byte.MaxValue;
            else if (newStat < byte.MinValue)
                return byte.MinValue;

            return (byte)newStat;
        }

        /// <summary>
        /// Calculates a sanity-checked stat using a spawn's variance value.
        /// </summary>
        /// <param name="stat">uint stat to be modified</param>
        /// <returns>new uint stat, +/- variance</returns>
        public uint CalculateVariance(uint stat)
        {

            var newStat = (Int64)Math.Round(stat + (stat * _variance));
            if (newStat > uint.MaxValue)
                return uint.MaxValue;
            else if (newStat < uint.MinValue)
                return uint.MinValue;

            return (uint)newStat;
        }

        private Loot _loot;

        public uint LootableXP
        {
            get { return _loot?.Xp ?? 0; }
            set { _loot.Xp = value; }
        }

        public uint LootableGold => _loot?.Gold ?? 0 ;

        public List<string> LootableItems => _loot?.Items ?? new List<string>();

        private void RandomlyAllocateStatPoints(int points)
        {
            // Random allocation
            for (var x = 1; x <= points; x++)
            {
                switch (Rng.Next(1, 5))
                {
                    case 1:
                        Stats.BaseStr += 1;
                        break;
                    case 2:
                        Stats.BaseInt += 1;
                        break;
                    case 3:
                        Stats.BaseDex += 1;
                        break;
                    case 4:
                        Stats.BaseCon += 1;
                        break;
                    case 5:
                        Stats.BaseWis += 1;
                        break;
                }
            }

        }
        public void AllocateStats()
        {
            var totalPoints = Stats.Level * 2;
            if (BehaviorSet is null || string.IsNullOrEmpty(BehaviorSet.StatAlloc))
                RandomlyAllocateStatPoints(totalPoints);
            else
            {
                var allocPattern = BehaviorSet.StatAlloc.Trim().ToLower().Split(" ");
                while (totalPoints != 0)
                {
                    foreach (var alloc in allocPattern)
                    {
                        switch (alloc)
                        {
                            case "str":
                                Stats.BaseStr += 1;
                                break;
                            case "int":
                                Stats.BaseInt += 1;
                                break;
                            case "wis":
                                Stats.BaseWis += 1;
                                break;
                            case "con":
                                Stats.BaseCon += 1;
                                break;
                            case "dex":
                                Stats.BaseDex += 1;
                                break;
                            default:
                                RandomlyAllocateStatPoints(1);
                                break;
                        }
                        totalPoints--;
                    }
                }
            }
        }

        /// <summary>
        /// Given an already specified behaviorset for the monster, learn all the castables possible at 
        /// their level; or the castables specifically enumerated in the set.
        /// </summary>
        private void LearnCastables()
        {
            if (BehaviorSet?.Castables == null)
                // Behavior set either doesn't exist or doesn't specify castables; no action needed
                return;

            // Default to automatic assignation if unset
            if (BehaviorSet.Castables.Auto == true)
            {   
                // If categories are present, use those. Otherwise, learn everything we can
                foreach (var category in BehaviorSet.LearnSpellCategories)
                {
                    Spells.AddRange(Game.World.WorldData.GetSpells(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis, 
                        Stats.BaseCon, Stats.BaseDex, category));
                }

                foreach (var category in BehaviorSet.LearnSkillCategories)
                {
                    Skills.AddRange(Game.World.WorldData.GetSkills(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis,
                        Stats.BaseCon, Stats.BaseDex, category));
                }
                if (BehaviorSet.LearnSkillCategories.Count == 0 && BehaviorSet.LearnSpellCategories.Count == 0)
                {
                    // Auto add according to stats
                    Spells.AddRange(Game.World.WorldData.GetSpells(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis,
                        Stats.BaseCon, Stats.BaseDex));
                    Skills.AddRange(Game.World.WorldData.GetSpells(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis,
                        Stats.BaseCon, Stats.BaseDex));
                }
            }
            // Handle any specific additions. Note that specific additions *ignore stat requirements*, 
            // to allow a variety of complex behaviors.
            foreach (var castable in BehaviorSet.Castables.Castable)
            {
                if (Game.World.WorldData.TryGetValue(castable, out Xml.Castable xmlCastable))
                {
                    if (xmlCastable.IsSkill)
                        Skills.Add(xmlCastable);
                    else
                        Spells.Add(xmlCastable);
                }               
            }          
        }

        public Monster(Xml.Creature creature, Xml.SpawnFlags flags, byte level, int map, Loot loot = null,
            Xml.CreatureBehaviorSet behaviorsetOverride = null)
        {
            _actionQueue = new ConcurrentQueue<MobAction>();
            SpawnFlags = flags;
            if (!Game.World.WorldData.TryGetValue(creature.BehaviorSet,
                out Xml.CreatureBehaviorSet BehaviorSet))
                BehaviorSet = behaviorsetOverride;

            Name = creature.Name;
            Sprite = creature.Sprite;
            World = Game.World;
            Map = Game.World.WorldData.Get<Map>(map);
            Stats.Level = level;
            AllocateStats();
            LearnCastables();
            
            DisplayText = creature.Description;

            //Stats.BaseDefensiveElement = spawn.GetDefensiveElement();
            //Stats.BaseDefensiveElement = spawn.GetOffensiveElement();

            _loot = loot;

            if (AiDisabled)
                IsHostile = false;
            else
                IsHostile = true;

            if (flags.HasFlag(Xml.SpawnFlags.MovementDisabled))
                ShouldWander = false;
            else
                ShouldWander = IsHostile == false;

            ThreatInfo = new ThreatInfo();
            DeathProcessed = false;
        }

        public Creature Target
        {
            get
            {
                return World.Objects.ContainsKey(_mTarget) ? (Creature)World.Objects[_mTarget] : null;
            }
            set
            {
                _mTarget = value?.Id ?? 0;
            }
        }

        public override int GetHashCode()
        {
            return (Name.GetHashCode() * Id.GetHashCode()) - 1;
        }

        public bool CheckFacing(Xml.Direction direction, Creature target)
        {
            if (Math.Abs(this.X - target.X) <= 1 && Math.Abs(this.Y - target.Y) <= 1)
            {
                if (((this.X - target.X) == 1 && (this.Y - target.Y) == 0))
                {
                    //check if facing west
                    if (this.Direction == Xml.Direction.West) return true;
                    else
                    {
                        this.Turn(Xml.Direction.West);
                    }
                }
                if (((this.X - target.X) == -1 && (this.Y - target.Y) == 0))
                {
                    //check if facing east
                    if (this.Direction == Xml.Direction.East) return true;
                    else
                    {
                        this.Turn(Xml.Direction.East);
                    }
                }
                if (((this.X - target.X) == 0 && (this.Y - target.Y) == 1))
                {
                    //check if facing south
                    if (this.Direction == Xml.Direction.North) return true;
                    else
                    {
                        this.Turn(Xml.Direction.North);
                    }
                }
                if (((this.X - target.X) == 0 && (this.Y - target.Y) == -1))
                {
                    if (this.Direction == Xml.Direction.South) return true;
                    else
                    {
                        this.Turn(Xml.Direction.South);
                    }
                }
            }
            return false;
        }

        public void Cast(Creature aggroTarget, UserGroup targetGroup)
        {
            if (CanCast)
            {
                decimal currentHpPercent = ((decimal)Stats.Hp / Stats.MaximumHp) * 100m;
            }
            //if (CanCast)
            //{
            //    //need to determine what it should do, and what is available to it.
            //    var interval = 0;
            //    decimal currentHpPercent = ((decimal)Stats.Hp / Stats.MaximumHp) * 100m;

            //    if (currentHpPercent < 1)
            //    {
            //        //ondeath does not need an interval check
            //        var selectedCastable = SelectSpawnCastable(SpawnCastType.OnDeath);
            //        if (selectedCastable == null) return;
            //        if (selectedCastable.Target == Xml.TargetType.Attacker)
            //        {
            //            Cast(aggroTarget, selectedCastable);
            //        }

            //        if (selectedCastable.Target == Xml.TargetType.Group || selectedCastable.Target == Xml.TargetType.Random)
            //        {
            //            if (targetGroup != null)
            //            {
            //                Cast(targetGroup, selectedCastable, selectedCastable.Target);
            //            }
            //            else
            //            {
            //                Cast(aggroTarget, selectedCastable);                            
            //            }
            //        }
            //    }

            //    if (currentHpPercent <= Castables.NearDeath.HealthPercent)
            //    {
            //        interval = _castables.NearDeath.Interval;

            //        var selectedCastable = SelectSpawnCastable(SpawnCastType.NearDeath);

            //        if (selectedCastable == null) return;

            //        if (selectedCastable.Target == Xml.TargetType.Attacker)
            //        {
            //            if (_castables.NearDeath.LastCast.AddSeconds(interval) < DateTime.Now)
            //            {
            //                Cast(aggroTarget, selectedCastable);
            //                _castables.NearDeath.LastCast = DateTime.Now;
            //            }
            //            else
            //            {
            //                if (Distance(ThreatInfo.ThreatTarget) == 1)
            //                {
            //                    AssailAttack(Direction, aggroTarget);
            //                }
            //            }

            //        }

            //        if (selectedCastable.Target == Xml.TargetType.Group || selectedCastable.Target == Xml.TargetType.Random)
            //        {
            //            if (targetGroup != null)
            //            {
            //                if (_castables.NearDeath.LastCast.AddSeconds(interval) < DateTime.Now)
            //                {
            //                    Cast(targetGroup, selectedCastable, selectedCastable.Target);
            //                    _castables.NearDeath.LastCast = DateTime.Now;
            //                }
            //                else
            //                {
            //                    if (Distance(ThreatInfo.ThreatTarget) == 1)
            //                    {
            //                        AssailAttack(Direction, aggroTarget);
            //                    }
            //                }
            //            }
            //            else
            //            {
            //                if (_castables.NearDeath.LastCast.AddSeconds(interval) < DateTime.Now)
            //                {
            //                    Cast(aggroTarget, selectedCastable);
            //                    _castables.NearDeath.LastCast = DateTime.Now;
            //                }
            //                else
            //                {
            //                    if (Distance(ThreatInfo.ThreatTarget) == 1)
            //                    {
            //                        AssailAttack(Direction, aggroTarget);
            //                    }
            //                }
            //            }
            //        }
            //    }

            //    var nextChoice = _random.Next(0, 2);

            //    if (nextChoice == 0) //offense
            //    {
            //        interval = _castables.Offense.Interval;
            //        var selectedCastable = SelectSpawnCastable(SpawnCastType.Offensive);
            //        if (selectedCastable == null) return;

            //        if (selectedCastable.Target == Xml.TargetType.Attacker)
            //        {
            //            if (_castables.Offense.LastCast.AddSeconds(interval) < DateTime.Now)
            //            {
            //                Cast(aggroTarget, selectedCastable);
            //                _castables.Offense.LastCast = DateTime.Now;
            //            } 
            //            else
            //            {
            //                if(Distance(ThreatInfo.ThreatTarget) == 1)
            //                {
            //                    AssailAttack(Direction, aggroTarget);
            //                }
            //            }
            //        }

            //        if (selectedCastable.Target == Xml.TargetType.Group || selectedCastable.Target == Xml.TargetType.Random)
            //        {
            //            if (targetGroup != null)
            //            {
            //                if (_castables.Offense.LastCast.AddSeconds(interval) < DateTime.Now)
            //                {
            //                    Cast(targetGroup, selectedCastable, selectedCastable.Target);
            //                    _castables.Offense.LastCast = DateTime.Now;
            //                }
            //                else
            //                {
            //                    if (Distance(ThreatInfo.ThreatTarget) == 1)
            //                    {
            //                        AssailAttack(Direction, aggroTarget);
            //                    }
            //                }
            //            }
            //            else
            //            {
            //                if (_castables.Offense.LastCast.AddSeconds(interval) < DateTime.Now)
            //                {
            //                    Cast(aggroTarget, selectedCastable);
            //                    _castables.Offense.LastCast = DateTime.Now;
            //                }
            //                else
            //                {
            //                    if (Distance(ThreatInfo.ThreatTarget) == 1)
            //                    {
            //                        AssailAttack(Direction, aggroTarget);
            //                    }
            //                }
            //            }
            //        }
            //    }

            //    if (nextChoice == 1) //defense
            //    {
            //        //not sure how to handle this one
            //    }
            //}
            //else
            //{
            //    if (Distance(ThreatInfo.ThreatTarget) == 1)
            //    {
            //        AssailAttack(Direction, aggroTarget);
            //    }
            //}
        }

        public void Cast(Creature target, Xml.Castable creatureCastable)
        {
            //var castable = World.WorldData.GetByIndex<Xml.Castable>(creatureCastable.Name);
            //if (target is Merchant) return;
            //UseCastable(castable, target, creatureCastable);
            //Condition.Casting = false;
        }

        public void Cast(UserGroup target, Xml.Castable creatureCastable, Xml.CreatureAttackPriority priority)
        {

            //var inRange = Map.EntityTree.GetObjects(GetViewport()).OfType<User>();

            //var result = inRange.Intersect(target.Members).ToList();

            //var castable = World.WorldData.GetByIndex<Xml.Castable>(creatureCastable.Name);

            //if (priority == Xml.CreatureAttackPriority.Group)
            //{
            //    foreach(var user in result)
            //    {
            //        UseCastable(castable, user, creatureCastable);
            //    }
            //}

            //if(priority == Xml.CreatureAttackPriority.Random)
            //{
            //    var rngSelection = _random.Next(0, result.Count);

            //    var user = result[rngSelection];

            //    UseCastable(castable, user, creatureCastable);
            //}

            //Condition.Casting = false;
        }

        public Xml.Castable SelectSpawnCastable(SpawnCastType castType)
        {
            return null;
            //Xml.Castable creatureCastable = null;
            //string castableName = string.Empty;

            //switch (castType)
            //{
            //    case SpawnCastType.Offensive:
            //        castableName = BehaviorSet.OffensiveCastables.PickRandom(true);
            //        break;
            //    case SpawnCastType.Defensive:
            //        castableName = BehaviorSet.DefensiveCastables.PickRandom(true);
            //        break;
            //    case SpawnCastType.NearDeath:
            //        castableName = BehaviorSet.NearDeathCastables.PickRandom(true);
            //        break;
            //    case SpawnCastType.OnDeath:
            //        castableName = BehaviorSet.OnDeathCastables.PickRandom(true);
            //        break;
            //}

            //if (!string.IsNullOrEmpty(castableName))
            //    Game.World.WorldData.TryGetValue(castableName, out creatureCastable);

            //return creatureCastable;
        }


        public void AssailAttack(Xml.Direction direction, Creature target = null)
        {
            if (target == null)
            {
                var obj = GetDirectionalTarget(direction);
                var monster = obj as Monster;
                if (monster != null) target = monster;
                var user = obj as User;
                if (user != null)
                {
                    target = user;
                }
                var npc = obj as Merchant;
                if (npc != null)
                {
                    target = npc;
                }
                //try to get the creature we're facing and set it as the target.
            }
            
            // A monster's assail is just a straight attack, no skills involved.
            SimpleAttack(target);
                            
            //animation handled here as to not repeatedly send assails.
            var assail = new ServerPacketStructures.PlayerAnimation() { Animation = 1, Speed = 20, UserId = this.Id };
            //Enqueue(assail.Packet());
            //Enqueue(sound.Packet());
            SendAnimation(assail.Packet());
            PlaySound(1);
        }

        /// <summary>
        /// A simple directional attack by a monster (equivalent of straight assail).
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="target"></param>
        public void SimpleAttack(Creature target)
        {
            // Redo as castable assail
            //target?.Damage(_simpleDamage, Stats.BaseOffensiveElement, Xml.DamageType.Physical, Xml.DamageFlags.None, this);
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (!(obj is User)) return;
            var user = obj as User;
            user.SendVisibleCreature(this);
        }

        public bool IsIdle()
        {
            return _idle;
        }

        public void Awaken()
        {
            _idle = false;
            //add to alive monsters?
        }

        public void Sleep()
        {
            _idle = true;
            //return to idle state
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public List<Tile> GetWalkableTiles(int x, int y)
        {
            var proposedLocations = new List<Tile>()
            {
                new Tile { X = x, Y = y - 1 },
                new Tile { X = x, Y = y + 1 },
                new Tile { X = x - 1, Y = y },
                new Tile { X = x + 1, Y = y }
            };

            // Don't return tiles that are walls, or tiles that contain creatures, but always
            // return our end tile

            return proposedLocations.Where(tile => (!Map.IsWall[tile.X, tile.Y] &&
            (Map.GetTileContents(tile.X, tile.Y).Where(c => c is Creature).Count() == 0)) ||
            (tile.X == Destination.X && tile.Y == Destination.Y)).ToList();
        }

        private static int AStarCalculateH(int x1, int y1, int x2, int y2)
        {
            return Math.Abs(x2 - x1) + Math.Abs(y2 - y1);
        }

        public Xml.Direction AStarGetDirection()
        {
            if (Location.X - CurrentPath.X < 1)
                return Xml.Direction.East;
            if (Location.X - CurrentPath.X > 1)
                return Xml.Direction.West;
            if (Location.Y - CurrentPath.Y < 1)
                return Xml.Direction.North;
            return Xml.Direction.South;
        }

        public bool AStarPathClear()
        {
            // TODO: optimize
            Tile pathStart = CurrentPath;
            while (pathStart != null)
            {
                if (Map.GetTileContents(pathStart.X, pathStart.Y).Where(obj => obj is Creature).Count() > 0)
                    return false;
                pathStart = pathStart.Parent;
            }
            return true;
        }

        public void AStarPathFind(int x1, int y1, int x2, int y2)
        {
            Tile current = null;
            var start = new Tile { X = x1, Y = y1 };
            var end = new Tile { X = x2, Y = y2 };

            var openList = new List<Tile>();
            var closedList = new List<Tile>();
            int g = 0;

            openList.Add(start);

            while (openList.Count > 0)
            {
                var lowest = openList.Min(l => l.F);
                current = openList.First(l => l.F == lowest);

                closedList.Add(current);
                openList.Remove(current);
                if (closedList.FirstOrDefault(l => l.X == end.X && l.Y == end.Y) != null)
                    // We have arrived
                    break;

                var adjacent = GetWalkableTiles(current.X, current.Y);
                g++;

                foreach (var tile in adjacent)
                {
                    // Ignore tiles in closed list
                    if (closedList.FirstOrDefault(l => l.X == tile.X && l.Y == tile.Y) != null)
                        continue;

                    if (openList.FirstOrDefault(l => l.X == tile.X && l.Y == tile.Y) == null)
                    {
                        tile.G = g;
                        tile.H = AStarCalculateH(tile.X, tile.Y, end.X, end.Y);
                        tile.F = tile.G + tile.H;
                        tile.Parent = current;
                        openList.Insert(0, tile);
                    }
                    else
                    {
                        if (g + tile.H < tile.F)
                        {
                            tile.G = g;
                            tile.F = tile.G + tile.H;
                            tile.Parent = current;
                        }
                    }
                }                               
            }
            // If null here, no path was found
            CurrentPath = current;
        }

        public Xml.Direction Relation(int x1, int y1)
        {
            if (Y > y1)
                return Xml.Direction.North;
            if (X < x1)
                return Xml.Direction.East;
            if (Y < y1)
                return Xml.Direction.South;
            if (X > x1)
                return Xml.Direction.West;
            return Xml.Direction.North;
        }

        public void NextAction()
        {
            var next = 0;
            if(Stats.Hp == 0)
            {
                _actionQueue.Enqueue(MobAction.Death);
            }
            if(!IsHostile)
            {
                next = _random.Next(2, 4); //move or idle
                _actionQueue.Enqueue((MobAction)next);
            }
            else
            {
                if(ThreatInfo.ThreatTarget != null)
                {
                    if (Distance(ThreatInfo.ThreatTarget) == 1)
                    {
                        next = _random.Next(0, 2); //attack or cast
                        _actionQueue.Enqueue((MobAction)next);
                    }
                    else
                    {
                        next = _random.Next(1, 3); //cast or move
                        _actionQueue.Enqueue((MobAction)next);
                    }
                }
                else
                {
                    next = 2; //move
                    _actionQueue.Enqueue((MobAction)next);
                }
            }

            ProcessActions();
        }

        private void ProcessActions()
        {
            while (_actionQueue.Count > 0)
            {
                _actionQueue.TryDequeue(out var action);
                if (action == MobAction.Attack)
                {
                    if (ThreatInfo.ThreatTarget == null) return;
                    if (CheckFacing(Direction, ThreatInfo.ThreatTarget))
                    {
                        AssailAttack(Direction, ThreatInfo.ThreatTarget);
                    }
                    else
                    {
                        Turn(Relation(ThreatInfo.ThreatTarget.X, ThreatInfo.ThreatTarget.Y));
                    }
                }
                if (action == MobAction.Cast)
                {

                    if (!Condition.Blinded)
                    {
                        Cast(ThreatInfo.ThreatTarget, ((User)ThreatInfo.ThreatTarget).Group);
                    }
                }
                if (action == MobAction.Move)
                {
                    if (!IsHostile && ShouldWander)
                    {
                        var which = _random.Next(0, 2); //turn or move
                        if (which == 0)
                        {
                            var next = _random.Next(0, 4);
                            if (Direction == (Xml.Direction)next)
                            {
                                Walk((Xml.Direction)next);
                            }
                            else
                            {
                                Turn((Xml.Direction)next);
                            }
                        }
                        else
                        {
                            var next = _random.Next(0, 4);
                            Turn((Xml.Direction)next);
                        }
                    }
                    else
                    {
                        if (ThreatInfo.ThreatTarget == null) return;
                        if (!Condition.Paralyzed && !Condition.Blinded)
                        {
                            if (CurrentPath == null || !AStarPathClear())
                                // If we don't have a current path to our threat target, OR if there is something in the way of
                                // our existing path, calculate a new one
                                AStarPathFind(Location.X, Location.Y, ThreatInfo.ThreatTarget.Location.X, ThreatInfo.ThreatTarget.Location.Y);
                            if (CurrentPath != null)
                            {
                                // Path was found, use it
                                if (Walk(AStarGetDirection()))
                                    // We've moved; update our path
                                    CurrentPath = CurrentPath.Parent;
                            }
                            else
                                // If we can't find a path, return to wandering
                                ShouldWander = true;
                        }
                    }
                }
                if (action == MobAction.Idle)
                {
                    //do nothing
                }
                if (action == MobAction.Death)
                {
                    _actionQueue.Clear();

                }
            }
        }

        public override void AoiDeparture(VisibleObject obj)
        {
            lock (_lock)
            {
                if (obj is User user)
                {
                    ThreatInfo.OnRangeExit(user);

                    if (ThreatInfo.ThreatTarget == null && ThreatInfo.ThreatTable.Count == 0)
                    {
                        ShouldWander = true;
                        FirstHitter = null;
                        Target = null;
                        Stats.Hp = Stats.MaximumHp;
                    }
                }
                if (Map.EntityTree.GetObjects(GetViewport()).OfType<User>().ToList().Count == 0)
                {
                    Active = false;
                }
                base.AoiDeparture(obj);
            }
        }

        public override void AoiEntry(VisibleObject obj)
        {
            lock (_lock)
            {
                if (obj is User user)
                {
                    if (Map.EntityTree.GetObjects(GetViewport()).OfType<User>().ToList().Count > 0)
                    {
                        Active = true;
                    }
                    if (IsHostile && ThreatInfo.ThreatTarget == null)
                    {
                        ThreatInfo.OnRangeEnter(user);
                        ShouldWander = false;
                    }
                }
                base.AoiEntry(obj);
            }
        }
    }

}
