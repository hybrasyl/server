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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015-2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

 using System;
 using System.Collections.Generic;
 using System.Drawing;
 using System.Linq;
 using Hybrasyl.Castables;
 using Hybrasyl.Creatures;
using Hybrasyl.Enums;
using Castable = Hybrasyl.Castables.Castable;
 using Class = Hybrasyl.Castables.Class;

namespace Hybrasyl.Objects
{
    public class Monster : Creature, ICloneable
    {
        protected static Random Rng = new Random();

        private bool _idle = true;

        private uint _mTarget;

        private Spawn _spawn;

        private uint _simpleDamage => Convert.ToUInt32(Rng.Next(_spawn.Damage.Min, _spawn.Damage.Max) * _variance);

        private List<Creatures.Castable> _castables;
        private double _variance;

        public int ActionDelay = 800;

        public DateTime LastAction { get; set; }
        public bool IsHostile { get; set; }
        public bool ShouldWander { get; set; }
        public bool CanCast { get; set; }



        public override void OnDeath()
        {
            //Shout("AAAAAAAAAAaaaaa!!!");
            // Now that we're dead, award loot.
            // FIXME: Implement loot tables / full looting.
            var hitter = LastHitter as User;
            if (hitter == null) return; // Don't handle cases of MOB ON MOB COMBAT just yet

            Condition.Alive = false;

            hitter.ShareExperience(LootableXP);
            var golds = new Gold(LootableGold);
            World.Insert(golds);
            Map.Insert(golds, X, Y);
            Map.Remove(this);

            World.Remove(this);
        }

        public override void OnReceiveDamage()
        {
            this.IsHostile = true;
            this.ShouldWander = false;
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


        public uint LootableXP => CalculateVariance((uint)Rng.Next((int)(_spawn.Loot.Xp?.Min ?? 1), (int)(_spawn.Loot.Gold?.Max ?? 1)));
        public uint LootableGold => CalculateVariance((uint)Rng.Next((int)(_spawn.Loot.Gold?.Min ?? 1),(int)(_spawn.Loot.Gold?.Max ?? 1)));


        public Monster(Hybrasyl.Creatures.Creature creature, Spawn spawn, int map)
        {

            var direction = (Rng.Next(0, 100) >= 50);
            _spawn = spawn;
            _variance = (direction == true ? Rng.NextDouble() * -1 : Rng.NextDouble()) * _spawn.Variance;
           

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
            Stats.BaseDefensiveElement = (Enums.Element) spawn.GetDefensiveElement();
            Stats.BaseDefensiveElement = (Enums.Element) spawn.GetOffensiveElement();

            //until intents are fixed, this is how this is going to be done.
            IsHostile = _random.Next(0, 7) < 2;
            ShouldWander = IsHostile == false;
            CanCast = spawn.Castables.Count > 0;
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
                Walk(x > X ? Direction.East : Direction.West);
            }

            return false;
        }

        public bool CheckFacing(Direction direction, Creature target)
        {
            if (Math.Abs(this.X - target.X) <= 1 && Math.Abs(this.Y - target.Y) <= 1)
            {
                if (((this.X - target.X) == 1 && (this.Y - target.Y) == 0))
                {
                    //check if facing west
                    if (this.Direction == Direction.West) return true;
                    else
                    {
                        this.Turn(Direction.West);
                    }
                }
                if (((this.X - target.X) == -1 && (this.Y - target.Y) == 0))
                {
                    //check if facing east
                    if (this.Direction == Direction.East) return true;
                    else
                    {
                        this.Turn(Direction.East);
                    }
                }
                if (((this.X - target.X) == 0 && (this.Y - target.Y) == 1))
                {
                    //check if facing south
                    if (this.Direction == Direction.North) return true;
                    else
                    {
                        this.Turn(Direction.North);
                    }
                }
                if (((this.X - target.X) == 0 && (this.Y - target.Y) == -1))
                {
                    if (this.Direction == Direction.South) return true;
                    else
                    {
                        this.Turn(Direction.South);
                    }
                }
            }
            return false;
        }

        public void Cast(Creature target)
        {
            var nextSpell = _random.Next(0, _castables.Count);
            var creatureCastable = _castables[nextSpell];
            var castable = World.WorldData.Get<Castable>(creatureCastable.Value);
            if (target is Merchant) return;
            UseCastable(castable, target);
        }

        public void AssailAttack(Direction direction, Creature target = null)
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
        public void SimpleAttack(Creature target) => target?.Damage(_simpleDamage, Stats.OffensiveElement, Enums.DamageType.Physical, DamageFlags.None, this);

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
