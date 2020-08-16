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
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Enums;
using Hybrasyl.Scripting;

namespace Hybrasyl.Objects
{
    public class Monster : Creature, ICloneable
    {
        protected static Random Rng = new Random();

        private bool _idle = true;

        private uint _mTarget;

        private Xml.Spawn _spawn;

        private uint _simpleDamage => Convert.ToUInt32(Rng.Next(_spawn.Damage.Min, _spawn.Damage.Max) * _variance);

        private List<Xml.SpawnCastable> _castables;
        private double _variance;

        public int ActionDelay = 800;

        public DateTime LastAction { get; set; }
        public bool IsHostile { get; set; }
        public bool ShouldWander { get; set; }
        public bool CanCast => _spawn.Castables.Count > 0;
        public bool DeathDisabled => _spawn.Flags.HasFlag(Xml.SpawnFlags.DeathDisabled);
        public bool MovementDisabled => _spawn.Flags.HasFlag(Xml.SpawnFlags.MovementDisabled);
        public bool AiDisabled => _spawn.Flags.HasFlag(Xml.SpawnFlags.AiDisabled);

        public bool ScriptDisabled { get; set; }

        public override void OnDeath()
        {
            if (DeathDisabled)
            {
                Stats.Hp = Stats.MaximumHp;               
                return;
            }

            Condition.Alive = false;
            var hitter = LastHitter as User;
            if (hitter == null)
            {
                Map.Remove(this);
                World.Remove(this);
                return; // Don't handle cases of MOB ON MOB COMBAT just yet
            }

            if (hitter.Grouped) ItemDropAllowedLooters = hitter.Group.Members.Select(user => user.Name).ToList();
            else ItemDropAllowedLooters.Add(hitter.Name);

            hitter.ShareExperience(LootableXP);
            var itemDropTime = DateTime.Now;

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

            if (LootableGold > 0)
            {
                var golds = new Gold(LootableGold);
                golds.ItemDropType = ItemDropType.MonsterLootPile;
                golds.ItemDropAllowedLooters = ItemDropAllowedLooters;
                golds.ItemDropTime = itemDropTime;
                World.Insert(golds);
                Map.Insert(golds, X, Y);
            }
            Map.Remove(this);
            World.Remove(this);

        }

        // We follow a different pattern here due to the fact that monsters
        // are not intended to be long-lived objects, and we don't want to 
        // spend a lot of overhead and resources creating a full script (eg via
        // OnSpawn) when not needed 99% of the time.
        private void InitScript()
        {
            if (Script != null || ScriptDisabled == true)
                return;

            if (World.ScriptProcessor.TryGetScript(Name, out Script damageScript))
            {
                Script = damageScript;
                Script.AssociateScriptWithObject(this);
                ScriptDisabled = false;
            }
            else
                ScriptDisabled = true;                
        }

        public override void OnHear(VisibleObject speaker, string text, bool shout = false)
        {
            if (speaker == this)
                return;

            // FIXME: in the glorious future, run asynchronously with locking
            InitScript();
            if (Script != null && !ScriptDisabled)
            {
                Script.SetGlobalValue("text", text);
                Script.SetGlobalValue("shout", shout);

                if (speaker is User user)
                    Script.ExecuteFunction("OnHear", new HybrasylUser(user));
                else
                    Script.ExecuteFunction("OnHear", new HybrasylWorldObject(speaker));
            }
        }

        public override void OnDamage(Creature attacker, uint damage)
        {
            // FIXME: in the glorious future, run asynchronously with locking
            InitScript();
            if (Script != null && !ScriptDisabled)
            {
                Script.SetGlobalValue("damage", damage);
                Script.ExecuteFunction("OnDamage", this, attacker);
            }
        }

        public override void OnHeal(Creature healer, uint heal)
        {
            // FIXME: in the glorious future, run asynchronously with locking
            InitScript();
            if (Script != null && !ScriptDisabled)
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

        // Convenience methods to avoid calling CalculateVariance directly
        public byte VariantStr => CalculateVariance(_spawn.Stats.Str);
        public byte VariantInt => CalculateVariance(_spawn.Stats.Int);
        public byte VariantDex => CalculateVariance(_spawn.Stats.Dex);
        public byte VariantCon => CalculateVariance(_spawn.Stats.Con);
        public byte VariantWis => CalculateVariance(_spawn.Stats.Wis);
        public uint VariantHp => CalculateVariance(_spawn.Stats.Hp);
        public uint VariantMp => CalculateVariance(_spawn.Stats.Mp);

        private Loot _loot;

        public uint LootableXP => _loot?.Xp ?? 0 ;

        public uint LootableGold => _loot?.Gold ?? 0 ;

        public List<string> LootableItems => _loot?.Items ?? new List<string>();

        public Monster(Xml.Creature creature, Xml.Spawn spawn, int map, Loot loot = null)
        {

            _spawn = spawn;
            var buffed = Rng.Next() > 50;
            if (buffed)
                _variance = (Rng.NextDouble() * _spawn.Variance) + 1;
            else
                _variance = 1 - (Rng.NextDouble() * _spawn.Variance);


            Name = creature.Name;
            Sprite = creature.Sprite;
            World = Game.World;
            Map = Game.World.WorldData.Get<Map>(map);
            Stats.Level = spawn.Stats.Level;
            Stats.BaseHp = VariantHp;
            Stats.Hp = VariantHp;
            Stats.BaseMp = VariantMp;
            Stats.Mp = VariantMp;
            DisplayText = creature.Description;
            Stats.BaseStr = VariantStr;
            Stats.BaseInt = VariantInt;
            Stats.BaseWis = VariantWis;
            Stats.BaseCon = VariantCon;
            Stats.BaseDex = VariantDex;
            _castables = spawn.Castables;

            Stats.BaseDefensiveElement = spawn.GetDefensiveElement();
            Stats.BaseDefensiveElement = spawn.GetOffensiveElement();

            _loot = loot;

            if (spawn.Flags.HasFlag(Xml.SpawnFlags.AiDisabled))
                IsHostile = false;
            else
                IsHostile = _random.Next(0, 7) < 2;

            if (spawn.Flags.HasFlag(Xml.SpawnFlags.MovementDisabled))
                ShouldWander = false;
            else
                ShouldWander = IsHostile == false;
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

        public virtual bool Pathfind(byte x, byte y)
        {
            var xDelta = Math.Abs(x - X);
            var yDelta = Math.Abs(y - Y);

            if (xDelta > yDelta)
            {
                Walk(x > X ? Xml.Direction.East : Xml.Direction.West);
            }

            return false;
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

        public void Cast(Creature target)
        {
            var nextSpell = _random.Next(0, _castables.Count);
            var creatureCastable = _castables[nextSpell];
            var castable = World.WorldData.Get<Xml.Castable>(creatureCastable.Value);
            if (target is Merchant) return;
            UseCastable(castable, target);
            Condition.Casting = false;
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
        public void SimpleAttack(Creature target) => target?.Damage(_simpleDamage, Stats.BaseOffensiveElement, Xml.DamageType.Physical, Xml.DamageFlags.None, this);

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
    }

}
