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
using System.Linq;
using Hybrasyl.Enums;
using Hybrasyl.Scripting;

namespace Hybrasyl.Objects;

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

    private readonly ConcurrentQueue<MobAction> _actionQueue;

    private static readonly Random Rng = new Random();

    private bool _idle = true;

    private uint _mTarget;

    public Dictionary<string, BookSlot> Spells { get; set; } = new Dictionary<string, BookSlot>();
    public Dictionary<string, BookSlot> Skills { get; set; } = new Dictionary<string, BookSlot>();

    public BookSlot LastSpellUsed { get; set; }
    public BookSlot LastSkillUsed { get; set; }

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

    public ThreatInfo ThreatInfo { get; private set; }

    public bool HasCastNearDeath;

    public bool Active;


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

            if (!(LastHitter is User hitter))
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

    public uint LootableGold => _loot?.Gold ?? 0;

    public List<string> LootableItems => _loot?.Items ?? new List<string>();

    private void RandomlyAllocateStatPoints(int points)
    {
        // Random allocation
        for (var x = 1; x <= points; x++)
        {
            switch (Rng.Next(1, 6))
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
            while (totalPoints > 0)
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
                    if (totalPoints % 2 == 0)
                    {
                        var randomBonus = (Rng.NextDouble() * 0.30) + 0.85;
                        int bonusHpGain = (int)Math.Ceiling((double)(Stats.BaseCon / (float)Stats.Level) * 50 * randomBonus);
                        int bonusMpGain = (int)Math.Ceiling((double)(Stats.BaseWis / (float)Stats.Level) * 50 * randomBonus);

                        Stats.BaseHp += bonusHpGain + 25;
                        Stats.BaseMp += bonusMpGain + 25;
                    }
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
        // All monsters get assail. TODO: hardcoded
        if (Game.World.WorldData.TryGetValue("Assail", out Xml.Castable assail))
            Skills.Add("Assail", new BookSlot() { Castable = assail });

        if (BehaviorSet?.Castables == null)
            // Behavior set either doesn't exist or doesn't specify castables; no action needed
            return;

        // Default to automatic assignation if unset
        if (BehaviorSet.Castables.Auto == true)
        {
            // If categories are present, use those. Otherwise, learn everything we can
            foreach (var category in BehaviorSet.LearnSpellCategories)
            {
                foreach (var castable in Game.World.WorldData.GetSpells(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis,
                             Stats.BaseCon, Stats.BaseDex, category))
                {
                    Spells.Add(castable.Name, new BookSlot() { Castable = castable });
                }
            }

            foreach (var category in BehaviorSet.LearnSkillCategories)
            {
                foreach (var castable in Game.World.WorldData.GetSkills(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis,
                             Stats.BaseCon, Stats.BaseDex, category))
                {
                    Skills.Add(castable.Name, new BookSlot() { Castable = castable });
                }
            }

            if (BehaviorSet.LearnSkillCategories.Count == 0 && BehaviorSet.LearnSpellCategories.Count == 0)
            {
                // Auto add according to stats
                foreach (var castable in Game.World.WorldData.GetCastables(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis,
                             Stats.BaseCon, Stats.BaseDex))
                {
                    if (castable.IsSkill)
                        Skills.Add(castable.Name, new BookSlot() { Castable = castable });
                    else
                        Spells.Add(castable.Name, new BookSlot() { Castable = castable });
                }
            }
        }
        // Handle any specific additions. Note that specific additions *ignore stat requirements*, 
        // to allow a variety of complex behaviors.
        foreach (var castable in BehaviorSet.Castables.Castable)
        {
            if (Game.World.WorldData.TryGetValue(castable, out Xml.Castable xmlCastable))
            {
                if (xmlCastable.IsSkill)
                    Skills.Add(xmlCastable.Name, new BookSlot() { Castable = xmlCastable });
                else
                    Spells.Add(xmlCastable.Name, new BookSlot() { Castable = xmlCastable });
            }
        }
    }

    public Monster(Xml.Creature creature, Xml.SpawnFlags flags, byte level, int map, Loot loot = null,
        Xml.CreatureBehaviorSet behaviorsetOverride = null)
    {
        _actionQueue = new ConcurrentQueue<MobAction>();
        SpawnFlags = flags;
        if (behaviorsetOverride != null)
            BehaviorSet = behaviorsetOverride;
        else if (!string.IsNullOrEmpty(creature.BehaviorSet))
            Game.World.WorldData.TryGetValue(creature.BehaviorSet, out Xml.CreatureBehaviorSet BehaviorSet);

        Name = creature.Name;
        Sprite = creature.Sprite;
        Map = Game.World.WorldData.Get<Map>(map);
        Stats.Level = level;

        AllocateStats();
        LearnCastables();

        if (BehaviorSet?.Behavior != null)
        {
            foreach (var cookie in BehaviorSet.Behavior.SetCookies)
            {
                // Don't override cookies set from spawn; spawn takes precedence
                if (!HasCookie(cookie.Name))
                    SetCookie(cookie.Name, cookie.Value);
            }
        }

        DisplayText = creature.Description;

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
        Stats.Hp = Stats.MaximumHp;
        Stats.Mp = Stats.MaximumMp;
    }

    public Creature Target
    {
        get
        {
            if (World.Objects.TryGetValue(_mTarget, out WorldObject o))
                return o as Creature;
            return null;
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

    /// <summary>
    /// Get the next offensive castable to be used, if it exists.
    /// </summary>
    /// <returns>NextCastingAction indicating what castable category or castable to be used. </returns>
    public NextCastingAction GetNextOffenseCastable() => GetNextCastable(BehaviorSet?.Behavior?.Casting?.Offense);

    /// <summary>
    /// Get the next defensive castable to be used, if it exists.
    /// </summary>
    /// <returns>NextCastingAction indicating what castable category or castable to be used. </returns>
    public NextCastingAction GetNextDefenseCastable() => GetNextCastable(BehaviorSet?.Behavior?.Casting?.Defense);

    /// <summary>
    /// Get the next near death castable to be used, if it exists.
    /// </summary>
    /// <returns>NextCastingAction indicating what castable category or castable to be used. </returns>
    public NextCastingAction GetNextNearDeathCastable() => GetNextCastable(BehaviorSet?.Behavior?.Casting?.NearDeath);

    /// <summary>
    /// Get the next offensive castable to be used, if it exists.
    /// </summary>
    /// <returns>NextCastingAction indicating what castable category or castable to be used. </returns>
    public NextCastingAction GetNextOnDeathCastable() => GetNextCastable(BehaviorSet?.Behavior?.Casting?.OnDeath);

    /// <summary>
    /// Get the next assail skill to be used, if it exists.
    /// </summary>
    /// <returns>NextCastingAction indicating what castable GLIROcategory or castable to be used. </returns>
    public NextCastingAction GetNextSkill() => GetNextCastable(BehaviorSet?.Behavior?.Assail);

    /// <summary>
    /// Calculate the next castable to be used for a given casting set.
    /// </summary>
    /// <returns>A NextCastingAction structure indicating the castable or category to be used along with a CreatureAttackPriority indicating the target</returns>
    private NextCastingAction GetNextCastable(Xml.CreatureCastingSet set)
    {
        // Resolution rules:
        //
        // 1: If a castable is defined in our casting set with a specific HP percentage that matches (<=), *always* return that first, unless
        //    lastcast is that same castable
        //    Ex: Cast ard sausage at 20% health or lower
        // 2: if Random is set, return a random category or castable if possible)
        //    Ex: Random is set, return a random category or castable based on defined settings
        // 3: if a category is defined, cycle through based on lastCast
        //    Ex: Ham, Sausage, Bacon -> lastcast category is Sausage -> return Bacon)
        // 4: If no HP percentages with UseOnce trigger, if lastCast is in the list of castables defined, return the next one in sequence
        //    Ex: I just cast ard sausage, and I see from our cycle mor ham is next, cast mor ham
        // 5: nothing matches - punt and let the monster AI figure it out (category and castable name will be null)

        // If we have no castables defined, or no behavior set, we can't cast
        if (set == null || set.Castable.Count == 0)
            return NextCastingAction.DoNothing;

        List<Xml.CreatureCastable> selection = new List<Xml.CreatureCastable>();
        BookSlot slot;

        // Find threshold castables, if defined (Rule #1)
        var thresholdCasts = set.Castable.Where(c => c.HealthPercentage > 0 && c.HealthPercentage <= Stats.HpPercentage).ToList();

        foreach (var threshold in thresholdCasts)
        {
            if (!Spells.TryGetValue(threshold.Value, out slot) && !Skills.TryGetValue(threshold.Value, out slot))
                // Threshold references a skill or spell that the mob doesn't know; ignore
                continue;

            // Is this a use once trigger with a percentage defined? If so, it hits and returns immediately IF the
            // corresponding slot hasn't seen a trigger.              
            if (threshold.UseOnce && !slot.ThresholdTriggered)
                return new NextCastingAction() { Slot = slot, Target = threshold.Priority };

            else if (!threshold.UseOnce)
                // Add to our list
                selection.Add(threshold);
        }

        // Now we've handled triggers - look at the rest of the set with no thresholds, and proceed to Rule #2
        selection.AddRange(set.Castable.Where(c => c.HealthPercentage == -1));

        if (set.Random)
        {
            var selectedCast = selection.PickRandom();
            if (!Spells.TryGetValue(selectedCast.Value, out slot) && !Skills.TryGetValue(selectedCast.Value, out slot))
                // Not found, do nothing
                return NextCastingAction.DoNothing;
            return new NextCastingAction() { Slot = slot, Target = selectedCast.Priority };
        }

        // Random handled, proceed to Rule #3 (cast from categories)

        if (set.CategoryList.Count > 0)
        {
            // Pick a random category then pick a random castable from that category
            var selectedCategory = set.CategoryList.PickRandom();
            var selectedCast = Spells.Values.Where(s => s.Castable.CategoryList.Contains(selectedCategory)).PickRandom(true);
            if (selectedCast != null)
                return new NextCastingAction() { Slot = selectedCast, Target = set.Priority };
            return NextCastingAction.DoNothing;
        }

        // Categories not defined - use castable rotation (Rule #4)
        // This is gross
        var idx = selection.IndexOf(selection.Where(c => c.Value == LastSpellUsed.Castable.Name || c.Value == LastSkillUsed.Castable.Name).FirstOrDefault());
        Xml.CreatureCastable selected = null;
        if (idx == -1)
            // Last cast was not found, do nothing
            return NextCastingAction.DoNothing;
        // A,B,C
        // idx == 2 (C), return A
        else if (selection.Count - 1 == idx)
            selected = selection.First();
        // A,B,C
        // idx == 1 (B), return C
        else
            selected = selection[idx + 1];

        if (selected != null)
        {
            if (!Spells.TryGetValue(selected.Value, out slot) && !Skills.TryGetValue(selected.Value, out slot))
                // Not found, do nothing
                return NextCastingAction.DoNothing;
            return new NextCastingAction() { Slot = slot, Target = selected.Priority };
        }

        // Rule #5 (return to monster ai)
        return NextCastingAction.DoNothing;
    }
     
    public void AssailAttack(Xml.Direction direction, Creature target = null)
    {
        if (target == null)
        {
            var obj = GetDirectionalTarget(direction);
            if (obj is Merchant)
                return;
            else if (obj is Creature || obj is User)
                target = obj;
        }

        if (target == null)
            return;

        // Get our skills if needed
        var nextSkill = GetNextSkill();

        if (nextSkill.DoNotCast)
        {
            // In the absence of a skill, just do a straight assail
            SimpleAttack(target);
            //animation handled here as to not repeatedly send assails.
            var assail = new ServerPacketStructures.PlayerAnimation() { Animation = 1, Speed = 20, UserId = this.Id };
            SendAnimation(assail.Packet());
            PlaySound(1);
        }
        else
            UseCastable(nextSkill.Slot.Castable, target, true);
    }


    /// <summary>
    /// A simple attack by a monster (equivalent of straight assail).
    /// </summary>        
    /// <param name="target"></param>
    public void SimpleAttack(Creature target)
    {
        if (Skills.TryGetValue("Assail", out BookSlot slot))
            UseCastable(slot.Castable, target, true);
    }

    public override void ShowTo(VisibleObject obj)
    {
        if (!(obj is User user)) return;
        if (!Condition.IsInvisible || user.Condition.SeeInvisible)
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

        var ret = new List<Tile>();

        foreach (var adj in proposedLocations)
        {
            if (adj.X >= Map.X || adj.Y >= Map.Y || adj.X < 0 || adj.Y < 0) continue;
            if (Map.IsWall[adj.X, adj.Y]) continue;
            var creatureContents = Map.GetCreatures(adj.X, adj.Y);
            if (creatureContents.Count == 0 || creatureContents.Contains(Target) || creatureContents.Contains(this))
                ret.Add(adj);
        }

        return ret;
    }

    private static int AStarCalculateH(int x1, int y1, int x2, int y2)
    {
        return Math.Abs(x2 - x1) + Math.Abs(y2 - y1);
    }

    public Xml.Direction AStarGetDirection()
    {
        if (CurrentPath.Parent == null) return Xml.Direction.North;
        Xml.Direction dir = Xml.Direction.North;

        if (X == CurrentPath.Parent.X)
        {
            if (CurrentPath.Parent.Y == Y + 1) dir = Xml.Direction.South;
            if (CurrentPath.Parent.Y == Y - 1) dir = Xml.Direction.North;
        }
        else if (Y == CurrentPath.Parent.Y)
        {
            if (CurrentPath.Parent.X == X + 1) dir = Xml.Direction.East;
            if (CurrentPath.Parent.X == X - 1) dir = Xml.Direction.West;
        }
        else GameLog.Warning("AStar: path divergence, moving randomly");
        return dir;
    }

    /// <summary>
    /// Verify that the next two steps of our path can be used.
    /// </summary>
    /// <returns>Boolean indicating whether the immediate path is clear or not.</returns>
    public bool AStarPathClear()
    {
        if (CurrentPath == null) return true;
        // TODO: optimize
        if (Map.IsCreatureAt(CurrentPath.X, CurrentPath.Y) && CurrentPath.Parent != null && Map.IsCreatureAt(CurrentPath.Parent.X, CurrentPath.Parent.Y))
        {
            if (!(X == CurrentPath.X && Y == CurrentPath.Y) || (X == CurrentPath.Parent.X || Y == CurrentPath.Parent.Y))
            {
                GameLog.Info($"AStar: path not clear at either {CurrentPath.X}, {CurrentPath.Y} or {CurrentPath.Parent.X}, {CurrentPath.Parent.Y}");
                return false;
            }
        }

        return true;
    }

    public Tile AStarPathFind(int x1, int y1, int x2, int y2)
    {          
        GameLog.Info($"AStarPath: from {x1},{y1} to {x2},{y2}");
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
            {
                // We have arrived
                GameLog.Info($"Closed list contains end tile {end.X}, {end.Y}");
                break;
            }
            var adj = GetWalkableTiles(current.X, current.Y);
            if (adj.Count == 0)
                GameLog.Warning("Adjacent tiles: 0");
            g++;

            foreach (var tile in adj)
            {

                // Ignore tiles in closed list
                if (closedList.FirstOrDefault(l => l.X == tile.X && l.Y == tile.Y) != null)
                    continue;

                //GameLog.Debug($"Adjacencies: {tile.X}, {tile.Y}");

                if (openList.FirstOrDefault(l => l.X == tile.X && l.Y == tile.Y) == null)
                {
                    tile.G = g;
                    tile.H = AStarCalculateH(tile.X, tile.Y, end.X, end.Y);
                    tile.F = tile.G + tile.H;
                    tile.Parent = current;
                    openList.Insert(0, tile);
                    //GameLog.Debug($"Adding {tile.X}, {tile.Y} to the open list");
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
        if (current != null)
            // Save our coordinate target for future reference
            current.Target = (x1, y1);
        else
            GameLog.Debug("AStar path find: no path found");
        return current;
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

    public void Cast(BookSlot slot, Creature target)
    {
        if (!Condition.CastingAllowed) return;
        UseCastable(slot.Castable, target);
        slot.LastCast = DateTime.Now;
        slot.UseCount++;
        Condition.Casting = false;
    }

    public void Attack()
    {
        if (CheckFacing(Direction, ThreatInfo.HighestThreat))
        {
            var assailSkill = GetNextSkill();
            if (assailSkill.DoNotCast)
            {
                if (ThreatInfo.HighestThreat == null) return;
                AssailAttack(Direction, ThreatInfo.HighestThreat);
            }
            else
            {
                var target = GetTarget(assailSkill.Target);
                if (target == null)
                    Cast(assailSkill.Slot, ThreatInfo.HighestThreat);
            }
        }
        else
        {
            Turn(Relation(ThreatInfo.HighestThreat.X, ThreatInfo.HighestThreat.Y));
        }
    }

    public void NextAction()
    {
        var next = 0;
        if (Stats.Hp == 0)
        {
            _actionQueue.Enqueue(MobAction.Death);
        }
        if (!IsHostile)
        {
            next = _random.Next(2, 4); //move or idle
            _actionQueue.Enqueue((MobAction)next);
        }
        else
        {
            if (ThreatInfo.HighestThreat != null)
            {
                if (Distance(ThreatInfo.HighestThreat) == 1)
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
        // TODO: what is the point of this being separate if it's always called from here?
        ProcessActions();
    }

    private void ProcessActions()
    {
        while (_actionQueue.Count > 0)
        {
            _actionQueue.TryDequeue(out var action);
            if (action == MobAction.Attack)
                if (Condition.Frozen || Condition.Asleep) return;
            Attack();
            if (action == MobAction.Cast)
            {
                if (!Condition.CastingAllowed) return;
                var offensiveCast = GetNextOffenseCastable();
                if (!offensiveCast.DoNotCast)
                {
                    // Handle group targeting here
                    if (offensiveCast.Target == Xml.CreatureAttackPriority.Group)
                    {
                        if (ThreatInfo.HighestThreat is User user)
                        {
                            if (user.Group != null)
                            {
                                foreach (var member in user.Group.Members)
                                    Cast(offensiveCast.Slot, member);
                            }
                        }
                    }
                    else
                    {
                        var target = GetTarget(offensiveCast.Target);
                        Cast(offensiveCast.Slot, target);
                    }
                }
                else Attack(); // Fall back to assail if we don't have a spell
            }
            if (action == MobAction.Move)
            {
                if (!Condition.MovementAllowed) return;
                if ((!IsHostile && ShouldWander) || Condition.Blinded)
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
                    if (ThreatInfo.HighestThreat == null) return;
                    if (Condition.MovementAllowed)
                    {
                        if (CurrentPath == null || !AStarPathClear())
                            // If we don't have a current path to our threat target, OR if there is something in the way of
                            // our existing path, calculate a new one
                        {
                            if (CurrentPath == null) GameLog.Info($"Path is null. Recalculating");
                            if (!AStarPathClear()) GameLog.Info($"Path wasn't clear. Recalculating");
                            Target = ThreatInfo.HighestThreat;
                            CurrentPath = AStarPathFind(ThreatInfo.HighestThreat.Location.X, ThreatInfo.HighestThreat.Location.Y, X,Y);
                        }

                        if (CurrentPath != null)
                        {
                            // We have a path, check its validity
                            // We recalculate our path if we're within five spaces of the target and they have moved
                            if (Distance(ThreatInfo.HighestThreat) < 5 && CurrentPath.Target.X != ThreatInfo.HighestThreat.Location.X &&
                                CurrentPath.Target.Y != ThreatInfo.HighestThreat.Location.Y)
                            {
                                GameLog.Info("Distance less than five and target moved, recalculating path");
                                CurrentPath = AStarPathFind(ThreatInfo.HighestThreat.Location.X, ThreatInfo.HighestThreat.Location.Y, X, Y);
                            }
                            if (Walk(AStarGetDirection()))
                            {
                                if (X != CurrentPath.X || Y != CurrentPath.Y)
                                    GameLog.SpawnError($"Walk: followed astar path but not on path (at {X},{Y} path is {CurrentPath.X}, {CurrentPath.Y}");
                                // We've moved; update our path
                                CurrentPath = CurrentPath.Parent;
                            }
                            else
                                // Couldn't move, attempt to recalculate path
                                CurrentPath = AStarPathFind(ThreatInfo.HighestThreat.Location.X, ThreatInfo.HighestThreat.Location.Y, X, Y);
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

                if (ThreatInfo.HighestThreat == null && ThreatInfo.Count == 0)
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
            if (obj is User user && (!user.Condition.IsInvisible || Condition.SeeInvisible))
            {
                if (Map.EntityTree.GetObjects(GetViewport()).OfType<User>().ToList().Count > 0)
                {
                    Active = true;
                }
                if (IsHostile && ThreatInfo.HighestThreat == null)
                {
                    ThreatInfo.OnRangeEnter(user);
                    ShouldWander = false;
                }
            }
            base.AoiEntry(obj);
        }
    }

    public Creature GetTarget(Xml.CreatureAttackPriority priority)
    {
        return priority switch
        {
            Xml.CreatureAttackPriority.Attacker => LastHitter,
            Xml.CreatureAttackPriority.AttackingCaster => ThreatInfo.HighestThreatCaster,
            Xml.CreatureAttackPriority.AttackingHealer => ThreatInfo.HighestThreatHealer,
            var x when x == Xml.CreatureAttackPriority.Random || x == Xml.CreatureAttackPriority.Group => ThreatInfo.ThreatTableByCreature.PickRandom().Key,
            Xml.CreatureAttackPriority.HighThreat => ThreatInfo.HighestThreat,
            Xml.CreatureAttackPriority.LowThreat => ThreatInfo.LowestThreat,
            _ => null,
        };
    }
}