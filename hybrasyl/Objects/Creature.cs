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
using System.Linq;
using Hybrasyl.Castables;
using Hybrasyl.Enums;
using log4net;
using Newtonsoft.Json;

namespace Hybrasyl.Objects
{
    public class Creature : VisibleObject
    {
        public new static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [JsonProperty]
        public byte Level { get; set; }

        [JsonProperty]
        public uint Experience { get; set; }

        [JsonProperty]
        public byte Ability { get; set; }

        [JsonProperty]
        public uint AbilityExp { get; set; }

        [JsonProperty]
        public uint Hp { get; set; }

        [JsonProperty]
        public uint Mp { get; set; }

        [JsonProperty]
        public long BaseHp { get; set; }

        [JsonProperty]
        public long BaseMp { get; set; }

        [JsonProperty]
        public long BaseStr { get; set; }

        [JsonProperty]
        public long BaseInt { get; set; }

        [JsonProperty]
        public long BaseWis { get; set; }

        [JsonProperty]
        public long BaseCon { get; set; }

        [JsonProperty]
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

        public Enums.Element OffensiveElement { get; set; }
        public Enums.Element DefensiveElement { get; set; }

        public ushort MapId { get; protected set; }
        public byte MapX { get; protected set; }
        public byte MapY { get; protected set; }

        [JsonProperty]
        public uint Gold { get; set; }

        [JsonProperty]
        public Inventory Inventory { get; protected set; }

        [JsonProperty("Equipment")]
        public Inventory Equipment { get; protected set; }

        public Creature()
        {
            Gold = 0;
            Inventory = new Inventory(59);
            Equipment = new Inventory(18);
        }

        public override void OnClick(User invoker)
        {
        }

        // Restrict to (inclusive) range between [min, max]. Max is optional, and if its
        // not present then no upper limit will be enforced.
        private static long BindToRange(long start, long? min, long? max)
        {
            if (min != null && start < min)
                return min.GetValueOrDefault();
            else if (max != null && start > max)
                return max.GetValueOrDefault();
            else
                return start;
        }

        public uint MaximumHp
        {
            get
            {
                var value = BaseHp + BonusHp;

                if (value > uint.MaxValue)
                    return uint.MaxValue;

                if (value < uint.MinValue)
                    return uint.MinValue;

                return (uint)BindToRange(value, StatLimitConstants.MIN_BASE_HPMP, StatLimitConstants.MAX_BASE_HPMP);
            }
        }

        public uint MaximumMp
        {
            get
            {
                var value = BaseMp + BonusMp;

                if (value > uint.MaxValue)
                    return uint.MaxValue;

                if (value < uint.MinValue)
                    return uint.MinValue;

                return (uint)BindToRange(value, StatLimitConstants.MIN_BASE_HPMP, StatLimitConstants.MAX_BASE_HPMP);
            }
        }

        public byte Str
        {
            get
            {
                var value = BaseStr + BonusStr;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Int
        {
            get
            {
                var value = BaseInt + BonusInt;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Wis
        {
            get
            {
                var value = BaseWis + BonusWis;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Con
        {
            get
            {
                var value = BaseCon + BonusCon;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Dex
        {
            get
            {
                var value = BaseDex + BonusDex;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Dmg
        {
            get
            {
                if (BonusDmg > byte.MaxValue)
                    return byte.MaxValue;

                if (BonusDmg < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(BonusDmg, StatLimitConstants.MIN_DMG, StatLimitConstants.MAX_DMG);
            }
        }

        public byte Hit
        {
            get
            {
                if (BonusHit > byte.MaxValue)
                    return byte.MaxValue;

                if (BonusHit < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(BonusHit, StatLimitConstants.MIN_HIT, StatLimitConstants.MAX_HIT);
            }
        }

        public sbyte Ac
        {
            get
            {
                Logger.DebugFormat("BonusAc is {0}", BonusAc);
                var value = 100 - Level / 3 + BonusAc;

                if (value > sbyte.MaxValue)
                    return sbyte.MaxValue;

                if (value < sbyte.MinValue)
                    return sbyte.MinValue;

                return (sbyte)BindToRange(value, StatLimitConstants.MIN_AC, StatLimitConstants.MAX_AC);
            }
        }

        public sbyte Mr
        {
            get
            {
                if (BonusMr > sbyte.MaxValue)
                    return sbyte.MaxValue;

                if (BonusMr < sbyte.MinValue)
                    return sbyte.MinValue;

                return (sbyte)BindToRange(BonusMr, StatLimitConstants.MIN_MR, StatLimitConstants.MAX_MR);
            }
        }

        public sbyte Regen
        {
            get
            {
                if (BonusRegen > sbyte.MaxValue)
                    return sbyte.MaxValue;

                if (BonusRegen < sbyte.MinValue)
                    return sbyte.MinValue;

                return (sbyte)BonusRegen;
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
                _mLastHitter = value?.Id ?? 0;
            }
        }

        public bool AbsoluteImmortal { get; set; }
        public bool PhysicalImmortal { get; set; }
        public bool MagicalImmortal { get; set; }

        public virtual void Attack(Direction direction, Castable castObject, Creature target = null)
        {
            //do something?
        }

        public virtual void Attack(Castable castObject, Creature target)
        {
            //do spell?
        }

        public virtual void Attack(Castable castObject)
        {
            //do aoe?
        }

        public void SendAnimation(ServerPacket packet)
        {
            Logger.InfoFormat("SendAnimation");
            Logger.InfoFormat("SendAnimation byte format is: {0}", BitConverter.ToString(packet.ToArray()));
            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
            {
                var nPacket = (ServerPacket)packet.Clone();
                Logger.InfoFormat("SendAnimation to {0}", user.Name);
                user.Enqueue(nPacket);

            }
        }

        public virtual void UpdateAttributes(StatUpdateFlags flags)
        {
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
                if (!(obj is User)) continue;
                var user = obj as User;
                var x11 = new ServerPacket(0x11);
                x11.WriteUInt32(Id);
                x11.WriteByte((byte)direction);
                user.Enqueue(x11);
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

        //public virtual bool AddItem(ItemObject item, bool updateWeight = true) { return false; }
        //public virtual bool AddItem(ItemObject item, int slot, bool updateWeight = true) { return false; }
        //public virtual bool RemoveItem(int slot, bool updateWeight = true) { return false; }
        //public virtual bool RemoveItem(int slot, int count, bool updateWeight = true) { return false; }
        //public virtual bool AddEquipment(ItemObject item) { return false; }
        //public virtual bool AddEquipment(ItemObject item, byte slot, bool sendUpdate = true) { return false; }
        //public virtual bool RemoveEquipment(byte slot) { return false; }

        public virtual void Heal(double heal, Creature healer = null)
        {
            if (AbsoluteImmortal || PhysicalImmortal)
                return;

            if (Hp == MaximumHp || heal > MaximumHp)
                return;

            Hp = heal > uint.MaxValue ? MaximumHp : Math.Min(MaximumHp, (uint)(Hp + heal));

            SendDamageUpdate(this);

        }

        public virtual void RegenerateMp(double mp, Creature regenerator = null)
        {
            if (AbsoluteImmortal)
                return;

            if (Mp == MaximumMp || mp > MaximumMp)
                return;

            Mp = mp > uint.MaxValue ? MaximumMp : Math.Min(MaximumMp, (uint)(Mp + mp));
        }

        public virtual void Damage(double damage, Enums.Element element = Enums.Element.None, Enums.DamageType damageType = Enums.DamageType.Direct, Creature attacker = null)
        {
            if (damageType == Enums.DamageType.Physical && (AbsoluteImmortal || PhysicalImmortal))
                return;

            if (damageType == Enums.DamageType.Magical && (AbsoluteImmortal || MagicalImmortal))
                return;

            if (damageType != Enums.DamageType.Direct)
            {
                double armor = Ac * -1 + 100;
                var resist = Game.ElementTable[(int)element, 0];
                var reduction = damage * (armor / (armor + 50));
                damage = (damage - reduction) * resist;
            }

            if (attacker != null)
                _mLastHitter = attacker.Id;

            var normalized = (uint)damage;

            if (normalized > Hp)
                normalized = Hp;

            Hp -= normalized;

            SendDamageUpdate(this);
        }

        private void SendDamageUpdate(Creature creature)
        {
            var percent = ((creature.Hp / (double)creature.MaximumHp) * 100);
            var healthbar = new ServerPacketStructures.HealthBar() { CurrentPercent = (byte)percent, ObjId = creature.Id };

            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
            {
                var nPacket = (ServerPacket)healthbar.Packet().Clone();
                user.Enqueue(nPacket);
            }
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (!(obj is User)) return;
            var user = (User)obj;
            user.SendVisibleCreature(this);
        }

        public virtual void Refresh()
        {
        }
    }

}
