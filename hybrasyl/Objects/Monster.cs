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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Hybrasyl.ChatCommands;
using Hybrasyl.Enums;
using Hybrasyl.Scripting;
using Hybrasyl.Xml;
using Sentry;

namespace Hybrasyl.Objects;

public enum MobAction
{
    Attack,
    Move,
    Idle,
    Death
}

public class Monster : Creature, ICloneable
{
    private readonly object _lock = new object();

    private readonly ConcurrentQueue<MobAction> _actionQueue;

    private bool _idle = true;

    private uint _mTarget;

    public Dictionary<string, MonsterBookSlot> Castables { get; set; } = new();

    public Dictionary<CreatureCastingSet, List<CreatureCastable>> Rotations = new();
    public List<CreatureCastable> ThresholdCasts = new();

    public List<CreatureCastingSet> CastingSets = new();

    public BookSlot LastSpellUsed { get; set; }
    public BookSlot LastSkillUsed { get; set; }

    private CreatureBehaviorSet _behaviorSet { get; set; }

    public CreatureBehaviorSet BehaviorSet
    {
        get => _behaviorSet;
        set
        {
            _behaviorSet = value;
            ProcessRotations();
        }
    }

    public SpawnFlags SpawnFlags;

    public (int X, int Y) Destination;

    public Tile CurrentPath;

    public int ActionDelay = 800;

    public DateTime LastAction { get; set; } = DateTime.MinValue;
    public DateTime LastSkill { get; set; } = DateTime.MinValue;
    public DateTime LastSpell { get; set; } = DateTime.MinValue;
    public bool IsHostile { get; set; }
    public bool ShouldWander { get; set; }
    public bool DeathDisabled => SpawnFlags.HasFlag(SpawnFlags.DeathDisabled);
    public bool MovementDisabled => SpawnFlags.HasFlag(SpawnFlags.MovementDisabled);
    public bool AiDisabled => SpawnFlags.HasFlag(SpawnFlags.AiDisabled);
    public bool DeathProcessed { get; set; }

    public bool ScriptExists { get; set; }

    public ThreatInfo ThreatInfo { get; private set; }

    public bool HasCastNearDeath;

    public bool Active;

    public bool HasAssailSkills { get; set; } = false;

    public Monster(Xml.Creature creature, SpawnFlags flags, byte level, int map, Loot loot = null,
        CreatureBehaviorSet behaviorsetOverride = null)
    {
        _actionQueue = new ConcurrentQueue<MobAction>();
        SpawnFlags = flags;
        if (behaviorsetOverride != null)
            BehaviorSet = behaviorsetOverride;
        else if (!string.IsNullOrEmpty(creature.BehaviorSet))
        {
            if (World.WorldData.TryGetValue<CreatureBehaviorSet>(creature.BehaviorSet, out var behaviorSet))
                BehaviorSet = behaviorSet;
        }

        Stats.BaseInt = 3;
        Stats.BaseDex = 3;
        Stats.BaseStr = 3;
        Stats.BaseWis = 3;
        Stats.BaseCon = 3;
        Stats.BaseAc = 100;

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

        Loot = loot;

        IsHostile = !AiDisabled;

        if (flags.HasFlag(SpawnFlags.MovementDisabled))
            ShouldWander = false;
        else
            ShouldWander = IsHostile == false;

        ThreatInfo = new ThreatInfo(Guid);
        DeathProcessed = false;
        Stats.Hp = Stats.MaximumHp;
        Stats.Mp = Stats.MaximumMp;
    }

    /// <summary>
    /// Given a behavior set, process our skill/spell rotations. This is automatically called whenever a behaviorset is changed.
    /// </summary>
    public void ProcessRotations()
    {
        if (BehaviorSet?.Behavior == null) return;
        foreach (var set in BehaviorSet.Behavior.CastableSets)
        {
            CastingSets.Add(set);
            ProcessRotation(set);
        }
    }

    /// <summary>
    /// Given a rotation type and a creature casting set, construct and store a casting rotation
    /// </summary>
    /// <param name="type">The RotationType of the rotation</param>
    /// <param name="set">The CreatureCastingSet that will be evaluated</param>
    private void ProcessRotation(CreatureCastingSet set)
    {
        if (set == null) return;
        if (set.Type == RotationType.Assail) HasAssailSkills = true;
        Rotations[set] = new List<CreatureCastable>();
        foreach (var entry in set.Castable)
            Rotations[set].Add(entry);
        // Now add categories
        foreach (var category in set.CategoryList)
        {
            var castableMatches = Castables.Where(x => x.Value.Castable.CategoryList.Contains(category));
            foreach (var match in castableMatches)
            {
                Rotations[set].Add(new CreatureCastable
                {
                    Value = match.Value.Castable.Name,
                    HealthPercentage = set.HealthPercentage,
                    Interval = set.Interval,
                    TargetPriority = set.TargetPriority,
                });
            }
        }
    }

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
            if (DeathProcessed) return;

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
                if (hitter.Stats.ExtraXp > 0)
                    hitter.GiveExperience((uint) (LootableXP * hitter.Stats.ExtraXp));

                var itemDropTime = DateTime.Now;

                if (LootableGold > 0)
                {
                    uint gold = 0;
                    gold = hitter.Stats.ExtraGold > 0
                        ? (uint) (LootableGold + LootableGold * hitter.Stats.ExtraGold)
                        : LootableGold;
                    var goldObj = new Gold(gold)
                    {
                        ItemDropType = ItemDropType.MonsterLootPile,
                        ItemDropAllowedLooters = ItemDropAllowedLooters,
                        ItemDropTime = itemDropTime
                    };
                    World.Insert(goldObj);
                    Map.Insert(goldObj, X, Y);
                }

                foreach (var itemname in LootableItems)
                {
                    var item = Game.World.CreateItem(itemname);
                    if (item == null)
                    {
                        GameLog.UserActivityError("User {player}: looting {monster}, loot item {item} is missing",
                            hitter.Name, Name, itemname);
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
            // TODO: ondeath castables
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

    public override void OnDamage(DamageEvent damageEvent)
    {
        lock (_lock)
        {
            if (damageEvent.Attacker != null && !damageEvent.Flags.HasFlag(DamageFlags.NoThreat))
            {
                if (!ThreatInfo.ContainsThreat(damageEvent.Attacker))
                {
                    ThreatInfo.AddNewThreat(damageEvent.Attacker, damageEvent.Damage);
                }
                else
                {
                    ThreatInfo.IncreaseThreat(damageEvent.Attacker, damageEvent.Damage);
                }
            }

            Condition.Asleep = false;
            IsHostile = true;
            ShouldWander = false;

            // FIXME: in the glorious future, run asynchronously with locking
            InitScript();

            if (Script == null) return;

            Script.SetGlobalValue("damage", damageEvent.Damage);
            Script.ExecuteFunction("OnDamage", this, damageEvent.Attacker);
        }
    }

    public override void OnHeal(Creature healer, uint heal)
    {
        // FIXME: in the glorious future, run asynchronously with locking
        InitScript();
        if (Script == null) return;

        Script.SetGlobalValue("heal", heal);
        Script.ExecuteFunction("OnHeal", this, healer);
    }

    public Loot Loot;

    public uint LootableXP
    {
        get => Loot?.Xp ?? 0;
        set => Loot.Xp = value;
    }

    public uint LootableGold => Loot?.Gold ?? 0;

    public List<string> LootableItems => Loot?.Items ?? new List<string>();

    public void ApplyModifier(double modifier)
    {
        Stats.BaseHp = (uint) (Stats.BaseHp * (1 + modifier));
        Stats.BaseMp = (uint) (Stats.BaseMp * (1 + modifier));
        LootableXP = (uint) (LootableXP * (1 + modifier));
        if (Loot?.Gold > 0)
            Loot.Gold = (uint) (Loot.Gold * (1 + modifier));
        Stats.BaseOutboundDamageModifier = 1 + modifier;
        Stats.BaseInboundDamageModifier = 1 - modifier;
        Stats.BaseOutboundHealModifier = 1 + modifier;
        Stats.BaseInboundHealModifier = 1 - modifier;
    }

    private void RandomlyAllocateStatPoints(int points)
    {
        // Random allocation
        for (var x = 1; x <= points; x++)
        {
            switch (Random.Shared.Next(1, 6))
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
                        var randomBonus = Random.Shared.NextDouble() * 0.30 + 0.85;
                        int bonusHpGain =
                            (int) Math.Ceiling((double) (Stats.BaseCon / (float) Stats.Level) * 50 * randomBonus);
                        int bonusMpGain =
                            (int) Math.Ceiling((double) (Stats.BaseWis / (float) Stats.Level) * 50 * randomBonus);

                        Stats.BaseHp += bonusHpGain + 25;
                        Stats.BaseMp += bonusMpGain + 25;
                    }
                }
            }
        }

        Stats.Hp = Stats.MaximumHp;
        Stats.Mp = Stats.MaximumMp;
    }

    /// <summary>
    /// Given an already specified behaviorset for the monster, learn all the castables possible at 
    /// their level; or the castables specifically enumerated in the set.
    /// </summary>
    private void LearnCastables()
    {
        // All monsters get assail. TODO: hardcoded
        if (Game.World.WorldData.TryGetValueByIndex("Assail", out Castable assail))
            Castables.Add("Assail", new MonsterBookSlot(castable: assail));

        if (BehaviorSet?.Castables == null)
            // Behavior set either doesn't exist or doesn't specify castables; no action needed
            return;

        // Default to automatic assignation if unsetF
        if (BehaviorSet.Castables.Auto == true)
        {
            // If categories are present, use those. Otherwise, learn everything we can
            foreach (var category in BehaviorSet.LearnSpellCategories)
            {
                foreach (var castable in Game.World.WorldData.GetSpells(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis,
                             Stats.BaseCon, Stats.BaseDex, category))
                {
                    Castables.Add(castable.Name, new MonsterBookSlot(castable: castable));
                }
            }

            foreach (var category in BehaviorSet.LearnSkillCategories)
            {
                foreach (var castable in Game.World.WorldData.GetSkills(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis,
                             Stats.BaseCon, Stats.BaseDex, category))
                {
                    Castables.Add(castable.Name, new MonsterBookSlot(castable: castable));
                }
            }

            if (BehaviorSet.LearnSkillCategories.Count == 0 && BehaviorSet.LearnSpellCategories.Count == 0)
            {
                // Auto add according to stats
                foreach (var castable in Game.World.WorldData.GetCastables(Stats.BaseStr, Stats.BaseInt, Stats.BaseWis,
                             Stats.BaseCon, Stats.BaseDex))
                {
                    if (castable.IsSkill)
                        Castables.Add(castable.Name, new MonsterBookSlot(castable: castable));
                    else
                        Castables.Add(castable.Name, new MonsterBookSlot(castable: castable));
                }
            }
        }

        // Handle any specific additions. Note that specific additions *ignore stat requirements*, 
        // to allow a variety of complex behaviors.
        foreach (var castable in BehaviorSet.Castables.Castable)
        {
            if (Game.World.WorldData.TryGetValue(castable, out Castable xmlCastable))
            {
                if (xmlCastable.IsSkill)
                    Castables.Add(xmlCastable.Name, new MonsterBookSlot(castable: xmlCastable));
                else
                    Castables.Add(xmlCastable.Name, new MonsterBookSlot(castable: xmlCastable));
            }
        }

        foreach (var kvp in Castables)
            GameLog.SpawnInfo($"I learned {kvp.Key}");

    }

    public Creature Target
    {
        get
        {
            if (World.Objects.TryGetValue(_mTarget, out WorldObject o))
                return o as Creature;
            return null;
        }
        set { _mTarget = value?.Id ?? 0; }
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode() * Id.GetHashCode() - 1;
    }

    public bool CheckFacing(Direction direction, Creature target)
    {
        if (target == null) return false;
        if (Math.Abs(X - target.X) <= 1 && Math.Abs(Y - target.Y) <= 1)
        {
            if (X - target.X == 1 && Y - target.Y == 0)
            {
                //check if facing west
                if (Direction == Direction.West) return true;
                else
                {
                    Turn(Direction.West);
                }
            }

            if (X - target.X == -1 && Y - target.Y == 0)
            {
                //check if facing east
                if (Direction == Direction.East) return true;
                else
                {
                    Turn(Direction.East);
                }
            }

            if (X - target.X == 0 && Y - target.Y == 1)
            {
                //check if facing south
                if (Direction == Direction.North) return true;
                else
                {
                    Turn(Direction.North);
                }
            }

            if (X - target.X == 0 && Y - target.Y == -1)
            {
                if (Direction == Direction.South) return true;
                else
                {
                    Turn(Direction.South);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Calculate the next castable to be used for a given casting set.
    /// </summary>
    /// <returns>A NextCastingAction structure indicating the castable or category to be used along with a CreatureAttackPriority indicating the target</returns>
    private NextCastingAction GetNextCastable()
    {
        // Resolution rules:
        //
        // 0: (Pre-step) Rotations are calculated automatically when a behaviorset is assigned to a creature, which includes resolution of categories.
        //    These are stored in Rotations according to the set type (OnDeath, Assail, etc).
        // 1: If a castable is defined in any casting set with a specific HP percentage that matches (<=) and is set to use once,
        //    *always* return that first, unless it has already triggered. If it is just active (eg threshold triggered)
        //    set it to active and add it to our rotation. Ex: Cast ard sausage at 10% health; Cast mor sausage once at 20% health or lower
        // 2: if Random is set, return a random castable (skill only, for assail).
        // 3: if Random is not set, return the next castable from our rotation

        // Rotations have been pre-calculated to include categories / etc, so if there's nothing here we can't do anything
        if (Rotations.Count == 0)
        {
            GameLog.SpawnDebug($"{Name} ({Map.Name}@{X},{Y}): rotation is empty");
            return NextCastingAction.DoNothing;
        }

        MonsterBookSlot slot;
        // Find the "most expired" set
        var set = BehaviorSet.Behavior.CastableSets.Where(x => x.Active && x.SecondsSinceLastUse >= x.Interval)
            .OrderByDescending(y => y.Interval).FirstOrDefault();
        if (set == null)
        {
            GameLog.SpawnDebug($"{Name} ({Map.Name}@{X},{Y}): rotation is empty");
            return NextCastingAction.DoNothing;
        }


        // Always handle UseOnce trigger thresholds (Rule #1)
        foreach (var threshold in ThresholdCasts.Where(c =>
                     c.HealthPercentage > 0 && c.HealthPercentage <= Stats.HpPercentage))
        {
            if (!Castables.TryGetValue(threshold.Value, out slot))
                // Threshold references a skill or spell that the mob doesn't know; ignore
                continue;
            // Is this a use once trigger with a percentage defined? If so, it hits and returns immediately IF the
            // corresponding slot hasn't seen a trigger.              
            if (!threshold.UseOnce || threshold.ThresholdTriggered) continue;

            GameLog.SpawnDebug(
                $"{Name} ({Map.Name}@{X},{Y}): one-time threshold triggered: {threshold.Value}, {threshold.HealthPercentage}%, priority {threshold.TargetPriority}");
            threshold.ThresholdTriggered = true;
            return new NextCastingAction {Slot = slot, TargetPriority = threshold.TargetPriority};
        }

        // Now we've handled triggers and updated our rotation as needed with no thresholds, and proceed to Rule #2
        CreatureCastable toCast = null;
        switch (set?.Random ?? false)
        {
            case true:
                toCast = Rotations[set].PickRandom();
                break;
            case false:
                if (LastSkillUsed == null && LastSpellUsed == null)
                    toCast = Rotations[set].PickRandom();
                if (toCast != null) break;
                // Find next skill in rotation
                var creatureCast = Rotations[set].FirstOrDefault(x =>
                    x.Value == (LastSkillUsed.Castable?.Name ?? string.Empty) ||
                    x.Value == (LastSpellUsed.Castable?.Name ?? string.Empty));
                if (creatureCast == null) return NextCastingAction.DoNothing;
                var idx = Rotations[set].IndexOf(creatureCast);
                if (idx == -1) return NextCastingAction.DoNothing;
                toCast = idx == Rotations[set].Count - 1 ? Rotations[set].First() : Rotations[set][idx + 1];
                break;

        }

        if (toCast == null) return NextCastingAction.DoNothing;
        if (Castables.TryGetValue(toCast.Value, out slot))
        {
            GameLog.SpawnDebug($"{Name} ({Map.Name}@{X},{Y}): casting {slot.Castable.Name}");
            return new NextCastingAction {Slot = slot, TargetPriority = toCast.TargetPriority};
        }

        // Not found, do nothing
        GameLog.SpawnWarning($"{Name} ({Map.Name}@{X},{Y}): trying to cast {toCast.Value} but not in rotation");
        return NextCastingAction.DoNothing;
    }

    /// <summary>
    /// A simple attack by a monster (equivalent of straight assail).
    /// </summary>        
    /// <param name="target"></param>
    public void AssailAttack(Direction direction, Creature target = null)
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
        if (!Castables.TryGetValue("Assail", out MonsterBookSlot slot)) return;
        UseCastable(slot.Castable, target, true);
        //animation handled here as to not repeatedly send assails.
        var assail = new ServerPacketStructures.PlayerAnimation { Animation = 1, Speed = 20, UserId = Id };
        SendAnimation(assail.Packet());
        PlaySound(1);
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
        return MemberwiseClone();
    }

    public List<Tile> GetWalkableTiles(int x, int y)
    {
        var proposedLocations = new List<Tile>
        {
            new() {X = x, Y = y - 1},
            new() {X = x, Y = y + 1},
            new() {X = x - 1, Y = y},
            new() {X = x + 1, Y = y}
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

    public Direction AStarGetDirection()
    {
        if (CurrentPath.Parent == null) return Direction.North;
        Direction dir = Direction.North;

        if (X == CurrentPath.Parent.X)
        {
            if (CurrentPath.Parent.Y == Y + 1) dir = Direction.South;
            if (CurrentPath.Parent.Y == Y - 1) dir = Direction.North;
        }
        else if (Y == CurrentPath.Parent.Y)
        {
            if (CurrentPath.Parent.X == X + 1) dir = Direction.East;
            if (CurrentPath.Parent.X == X - 1) dir = Direction.West;
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
        if (Map.IsCreatureAt(CurrentPath.X, CurrentPath.Y) && CurrentPath.Parent != null &&
            Map.IsCreatureAt(CurrentPath.Parent.X, CurrentPath.Parent.Y))
        {
            if (!(X == CurrentPath.X && Y == CurrentPath.Y) || X == CurrentPath.Parent.X || Y == CurrentPath.Parent.Y)
            {
                GameLog.Info(
                    $"AStar: path not clear at either {CurrentPath.X}, {CurrentPath.Y} or {CurrentPath.Parent.X}, {CurrentPath.Parent.Y}");
                return false;
            }
        }

        return true;
    }

    public Tile AStarPathFind(int x1, int y1, int x2, int y2)
    {
        GameLog.Info($"AStarPath: from {x1},{y1} to {x2},{y2}");
        Tile current = null;
        var start = new Tile {X = x1, Y = y1};
        var end = new Tile {X = x2, Y = y2};

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

    public Direction Relation(int x1, int y1)
    {
        if (Y > y1)
            return Direction.North;
        if (X < x1)
            return Direction.East;
        if (Y < y1)
            return Direction.South;
        if (X > x1)
            return Direction.West;
        return Direction.North;
    }

    public void Cast(MonsterBookSlot slot, Creature target)
    {
        if (!Condition.CastingAllowed) return;
        Condition.Casting = true;
        UseCastable(slot.Castable, target);
        Condition.Casting = false;
        slot.LastCast = DateTime.Now;
        slot.UseCount++;
    }

    public void Attack()
    {
        if (ThreatInfo.HighestThreat == null) return;
        if (CheckFacing(Direction, ThreatInfo.HighestThreat))
        {
            AssailAttack(Direction, ThreatInfo.HighestThreat);
        }
        else
            Turn(Relation(ThreatInfo.HighestThreat.X, ThreatInfo.HighestThreat.Y));
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
            _actionQueue.Enqueue((MobAction) next);
        }
        else
        {
            if (ThreatInfo.HighestThreat != null)
            {
                if (Distance(ThreatInfo.HighestThreat) == 1)
                    _actionQueue.Enqueue(MobAction.Attack);
                else
                {
                    next = _random.Next(1, 3); //cast or move
                    _actionQueue.Enqueue((MobAction) next);
                }
            }
            else
            {
                next = 2; //move
                _actionQueue.Enqueue((MobAction) next);
            }
        }
    }

    public void ProcessActions()
    {
        while (_actionQueue.Count > 0)
        {
            _actionQueue.TryDequeue(out var action);
            switch (action)
            {
                case MobAction.Attack:
                    var next = GetNextCastable();
                    if (next.DoNotCast) Attack();
                    var targets = ThreatInfo.GetTargets(next.TargetPriority);
                    if (targets.Count == 0)
                    {
                        GameLog.SpawnDebug($"{Name}: ({Map.Name}@{X},{Y}): no targets returned from priority {next.TargetPriority}");
                        return;
                    }
                    foreach (var target in targets)
                        Cast(next.Slot, target);
                    
                    return;
                case MobAction.Move when !Condition.MovementAllowed:
                    return;
                case MobAction.Move when !IsHostile && ShouldWander || Condition.Blinded:
                {
                    var which = _random.Next(0, 2); //turn or move
                    if (which == 0)
                    {
                        var dir = _random.Next(0, 4);
                        if (Direction == (Direction) dir)
                        {
                            Walk((Direction) dir);
                        }
                        else
                        {
                            Turn((Direction) dir);
                        }
                    }
                    else
                    {
                        var dir = _random.Next(0, 4);
                        Turn((Direction) dir);
                    }

                    break;
                }
                case MobAction.Move when ThreatInfo.HighestThreat == null:
                    return;
                case MobAction.Move:
                {
                    if (Condition.MovementAllowed)
                    {
                        if (CurrentPath == null || !AStarPathClear())
                            // If we don't have a current path to our threat target, OR if there is something in the way of
                            // our existing path, calculate a new one
                        {
                            if (CurrentPath == null) GameLog.Info($"Path is null. Recalculating");
                            if (!AStarPathClear()) GameLog.Info($"Path wasn't clear. Recalculating");
                            Target = ThreatInfo.HighestThreat;
                            CurrentPath = AStarPathFind(ThreatInfo.HighestThreat.Location.X,
                                ThreatInfo.HighestThreat.Location.Y, X, Y);
                        }

                        if (CurrentPath != null)
                        {
                            // We have a path, check its validity
                            // We recalculate our path if we're within five spaces of the target and they have moved
                            if (Distance(ThreatInfo.HighestThreat) < 5 &&
                                CurrentPath.Target.X != ThreatInfo.HighestThreat.Location.X &&
                                CurrentPath.Target.Y != ThreatInfo.HighestThreat.Location.Y)
                            {
                                GameLog.Info("Distance less than five and target moved, recalculating path");
                                CurrentPath = AStarPathFind(ThreatInfo.HighestThreat.Location.X,
                                    ThreatInfo.HighestThreat.Location.Y, X, Y);
                            }

                            if (Walk(AStarGetDirection()))
                            {
                                if (X != CurrentPath.X || Y != CurrentPath.Y)
                                    GameLog.SpawnError(
                                        $"Walk: followed astar path but not on path (at {X},{Y} path is {CurrentPath.X}, {CurrentPath.Y}");
                                // We've moved; update our path
                                CurrentPath = CurrentPath.Parent;
                            }
                            else
                                // Couldn't move, attempt to recalculate path
                                CurrentPath = AStarPathFind(ThreatInfo.HighestThreat.Location.X,
                                    ThreatInfo.HighestThreat.Location.Y, X, Y);
                        }
                        else
                            // If we can't find a path, return to wandering
                            ShouldWander = true;
                    }

                    break;
                }
                case MobAction.Idle:
                    //do nothing
                    break;
                case MobAction.Death:
                    _actionQueue.Clear();
                    break;
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

    public List<Creature> GetThreatTarget(CreatureTargetPriority priority) => ThreatInfo.GetTargets(priority);
}