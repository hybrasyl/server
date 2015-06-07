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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 *            
 */

using Hybrasyl.Enums;
using log4net;
using System;

namespace Hybrasyl.Objects
{
    public class Creature : VisibleObject
    {
        public new static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public byte Level { get; set; }
        public uint Experience { get; set; }

        public byte Ability { get; set; }
        public uint AbilityExp { get; set; }

        public uint Hp { get; set; }
        public uint Mp { get; set; }

        public long BaseHp { get; set; }
        public long BaseMp { get; set; }
        public long BaseStr { get; set; }
        public long BaseInt { get; set; }
        public long BaseWis { get; set; }
        public long BaseCon { get; set; }
        public long BaseDex { get; set; }

        public long BonusHp { get; set; }
        public long BonusMp { get; set; }
        public long BonusStr { get; set; }
        public long BonusInt { get; set; }
        public long BonusWis { get; set; }
        public long BonusCon { get; set; }
        public long BonusDex { get; set; }
        public long BonusDmg { get; set; }
        public long BonusHit { get; set; }
        public long BonusAc { get; set; }
        public long BonusMr { get; set; }
        public long BonusRegen { get; set; }

        public Element OffensiveElement { get; set; }
        public Element DefensiveElement { get; set; }

        public ushort MapId { get; protected set; }
        public byte MapX { get; protected set; }
        public byte MapY { get; protected set; }

        public uint Gold { get; set; }
        public Inventory Inventory { get; protected set; }
        public Inventory Equipment { get; protected set; }

        public Creature()
            : base()
        {
            Gold = 0;
            Inventory = new Inventory(59);
            Equipment = new Inventory(18);
        }

        public override void OnClick(User invoker)
        {
        }

        public uint MaximumHp
        {
            get
            {
                var value = BaseHp + BonusHp;

                if (value > uint.MaxValue)
                {
                    return uint.MaxValue;
                }
                if (value < uint.MinValue)
                {
                    return uint.MinValue;
                }
                return (uint)value;
            }
        }
        public uint MaximumMp
        {
            get
            {
                var value = BaseMp + BonusMp;

                if (value > uint.MaxValue)
                {
                    return uint.MaxValue;
                }
                if (value < uint.MinValue)
                {
                    return uint.MinValue;
                }
                return (uint)value;
            }
        }
        public byte Str
        {
            get
            {
                var value = BaseStr + BonusStr;

                if (value > byte.MaxValue)
                {
                    return byte.MaxValue;
                }
                if (value < byte.MinValue)
                {
                    return byte.MinValue;
                }
                return (byte)value;
            }
        }
        public byte Int
        {
            get
            {
                var value = BaseInt + BonusInt;

                if (value > byte.MaxValue)
                {
                    return byte.MaxValue;
                }
                if (value < byte.MinValue)
                {
                    return byte.MinValue;
                }
                return (byte)value;
            }
        }
        public byte Wis
        {
            get
            {
                var value = BaseWis + BonusWis;

                if (value > byte.MaxValue)
                {
                    return byte.MaxValue;
                }
                if (value < byte.MinValue)
                {
                    return byte.MinValue;
                }
                return (byte)value;
            }
        }
        public byte Con
        {
            get
            {
                var value = BaseCon + BonusCon;

                if (value > byte.MaxValue)
                {
                    return byte.MaxValue;
                }
                if (value < byte.MinValue)
                {
                    return byte.MinValue;
                }
                return (byte)value;
            }
        }
        public byte Dex
        {
            get
            {
                var value = BaseDex + BonusDex;

                if (value > byte.MaxValue)
                {
                    return byte.MaxValue;
                }
                if (value < byte.MinValue)
                {
                    return byte.MinValue;
                }
                return (byte)value;
            }
        }
        public byte Dmg
        {
            get
            {
                if (BonusDmg > byte.MaxValue)
                {
                    return byte.MaxValue;
                }
                if (BonusDmg < byte.MinValue)
                {
                    return byte.MinValue;
                }
                return (byte)BonusDmg;
            }
        }
        public byte Hit
        {
            get
            {
                if (BonusHit > byte.MaxValue)
                {
                    return byte.MaxValue;
                }
                if (BonusHit < byte.MinValue)
                {
                    return byte.MinValue;
                }
                return (byte)BonusHit;
            }
        }
        public sbyte Ac
        {
            get
            {
                Logger.DebugFormat("BonusAc is {0}", BonusAc);
                var value = 100 - Level / 3 + BonusAc;

                if (value > sbyte.MaxValue)
                {
                    return sbyte.MaxValue;
                }
                if (value < sbyte.MinValue)
                {
                    return sbyte.MinValue;
                }
                return (sbyte)value;
            }
        }
        public sbyte Mr
        {
            get
            {
                if (BonusMr > sbyte.MaxValue)
                {
                    return sbyte.MaxValue;
                }
                if (BonusMr < sbyte.MinValue)
                {
                    return sbyte.MinValue;
                }
                return (sbyte)BonusMr;
            }
        }

        public sbyte Regen
        {
            get
            {
                if (BonusRegen > sbyte.MaxValue)
                {
                    return sbyte.MaxValue;
                }
                if (BonusRegen < sbyte.MinValue)
                {
                    return sbyte.MinValue;
                }
                return (sbyte)BonusRegen;
            }
        }

        public int HealthPercent
        {
            get
            {

                if (Hp <= 0)
                    return 0;

                var percent = (int)(Hp * 100 / MaximumHp);
                if (percent > 100)
                {
                    percent = 100;
                }
                if (percent < 0)
                {
                    percent = 0;
                }
                return percent;
            }
        }


        private uint _mLastHitter;
        public Creature LastHitter
        {
            get
            {
                return World.Objects.ContainsKey(_mLastHitter) ? (Creature)World.Objects[_mLastHitter] : null;
            }
            set
            {
                _mLastHitter = value == null ? 0 : value.Id;
            }
        }

        public bool AbsoluteImmortal { get; set; }
        public bool PhysicalImmortal { get; set; }
        public bool MagicalImmortal { get; set; }

        public virtual void Attack()
        {
        }

        public virtual void UpdateAttributes(StatUpdateFlags flags)
        {
        }

        public virtual void ShowHealthBar()
        {
            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (obj != null)
                {
                    if (obj is User)
                    {
                        var user = obj as User;

                        var x13 = new ServerPacket(0x13);
                        x13.WriteUInt32(Id);
                        x13.WriteByte(0);
                        x13.WriteByte((byte)HealthPercent);
                        x13.WriteByte(0x01);
                        user.Enqueue(x13);
                    }
                }
            }
        }

        public bool WithinRangeOf(WorldObject user)
        {
            return WithinRangeOf(user.X, user.Y);
        }

        public bool WithinRangeOf(int x, int y)
        {
            var xDist = Math.Abs(X - x);
            var yDist = Math.Abs(Y - y);

            if (xDist > 13.0 ||
                yDist > 13.0)
            {
                return false;
            }
            return Math.Sqrt((xDist * xDist) + (yDist * yDist)) <= 13.0;
        }


        public virtual bool Walk(Direction direction)
        {
            return false;
        }
        public virtual bool Turn(Direction direction)
        {
            Direction = direction;


            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    var x11 = new ServerPacket(0x11);
                    x11.WriteUInt32(Id);
                    x11.WriteByte((byte)direction);
                    user.Enqueue(x11);
                }
            }

            return true;
        }

        public virtual void Motion(byte motion, short speed)
        {
            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    user.SendMotion(Id, motion, speed);
                }
            }
        }


        public virtual void Damage(double damage, Element element = Element.None, DamageType damageType = DamageType.Direct, Creature attacker = null)
        {
            if (damageType == DamageType.Physical && (AbsoluteImmortal || PhysicalImmortal))
            {
                return;
            }
            if (damageType == DamageType.Magical && (AbsoluteImmortal || MagicalImmortal))
            {
                return;
            }
            if (damageType != DamageType.Direct)
            {
                var armor = Ac * -1 + 100;
                var resist = Game.ElementTable[(int)element, 0];
                var reduction = damage * (armor / (armor + 50));
                damage = (damage - reduction) * resist;
            }

            if (attacker != null)
            {
                _mLastHitter = attacker.Id;
            }
            var normalized = (uint)damage;

            if (normalized > Hp)
            {
                normalized = Hp;
            }
            Hp -= normalized;
        }

        public virtual void Refresh()
        {
        }
    }
}
