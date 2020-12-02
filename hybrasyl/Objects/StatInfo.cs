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

using Hybrasyl.Threading;
using Newtonsoft.Json;
using System;

namespace Hybrasyl.Objects
{

    [JsonObject(MemberSerialization.OptIn)]
    public class StatInfo
    {
        // The actual lockable private properties
        #region lockables
        private Lockable<byte> _level { get; set; }
        private Lockable<uint> _experience { get; set; }
        private Lockable<byte> _ability { get; set; }
        private Lockable<uint> _abilityExp { get; set; }
        private Lockable<uint> _hp { get; set; }
        private Lockable<uint> _mp { get; set; }
        private Lockable<long> _baseHp { get; set; }
        private Lockable<long> _baseMp { get; set; }
        private Lockable<long> _baseStr { get; set; }
        private Lockable<long> _baseInt { get; set; }
        private Lockable<long> _baseWis { get; set; }
        private Lockable<long> _baseCon { get; set; }
        private Lockable<long> _baseDex { get; set; }
        private Lockable<long> _baseCrit { get; set; }

        private Lockable<long> _bonusHp { get; set; }
        private Lockable<long> _bonusMp { get; set; }
        private Lockable<long> _bonusStr { get; set; }
        private Lockable<long> _bonusInt { get; set; }
        private Lockable<long> _bonusWis { get; set; }
        private Lockable<long> _bonusCon { get; set; }
        private Lockable<long> _bonusDex { get; set; }
        private Lockable<long> _bonusDmg { get; set; }
        private Lockable<long> _bonusHit { get; set; }
        private Lockable<long> _bonusAc { get; set; }
        private Lockable<long> _bonusMr { get; set; }
        private Lockable<long> _bonusRegen { get; set; }
        private Lockable<Xml.ElementType> _baseOffensiveElement { get; set; }
        private Lockable<Xml.ElementType> _baseDefensiveElement { get; set; }
        private Lockable<Xml.ElementType> _offensiveElementOverride { get; set; }
        private Lockable<Xml.ElementType> _defensiveElementOverride { get; set; }
        private Lockable<double> _baseReflectChance { get; set; }
        private Lockable<double> _bonusReflectChance { get; set; }
        private Lockable<Xml.DamageType?> _damageTypeOverride { get; set; }
        private Lockable<double> _baseReflectIntensity { get; set; }
        private Lockable<double> _bonusReflectIntensity { get; set; }
        private Lockable<double> _baseHealModifier { get; set; }
        private Lockable<double> _bonusHealModifier { get; set; }
        private Lockable<double> _baseDamageModifier { get; set; }
        private Lockable<double> _bonusDamageModifier { get; set; }
        private Lockable<Random> _random { get; set; }


        #endregion

        // Publicly accessible getters/setters, relying on the lockables
        #region accessors

        public decimal HpPercentage => (decimal) Hp / MaximumHp * 100m;
        [JsonProperty]
        public Xml.ElementType BaseOffensiveElement { get { return _baseOffensiveElement.Value; } set { _baseOffensiveElement.Value = value; } }
        [JsonProperty]
        public Xml.ElementType BaseDefensiveElement { get { return _baseDefensiveElement.Value; } set { _baseDefensiveElement.Value = value; } }
        [JsonProperty]
        public Xml.ElementType OffensiveElementOverride { get { return _offensiveElementOverride.Value; } set { _offensiveElementOverride.Value = value; } }
        [JsonProperty]
        public Xml.ElementType DefensiveElementOverride { get { return _defensiveElementOverride.Value; } set { _defensiveElementOverride.Value = value; } }
        [JsonProperty]
        public byte Level { get { return _level.Value; } set { _level.Value = value; } }
        [JsonProperty]
        public uint Experience { get { return _experience.Value; } set { _experience.Value = value; } }
        [JsonProperty]
        public byte Ability { get { return _ability.Value; } set { _ability.Value = value; } }
        [JsonProperty]
        public uint AbilityExp { get { return _abilityExp.Value; } set { _abilityExp.Value = value; } }
        [JsonProperty]
        public uint Hp { get { return _hp.Value; } set { _hp.Value = value; } }
        [JsonProperty]
        public uint Mp { get { return _mp.Value; } set { _mp.Value = value; } }
        [JsonProperty]
        public long BaseHp { get { return _baseHp.Value; } set { _baseHp.Value = value; } }
        [JsonProperty]
        public long BaseMp { get { return _baseMp.Value; } set { _baseMp.Value = value; } }
        [JsonProperty]
        public long BaseStr { get { return _baseStr.Value; } set { _baseStr.Value = value; } }
        [JsonProperty]
        public long BaseInt { get { return _baseInt.Value; } set { _baseInt.Value = value; } }
        [JsonProperty]
        public long BaseCon { get { return _baseCon.Value; } set { _baseCon.Value = value; } }
        [JsonProperty]
        public long BaseWis { get { return _baseWis.Value; } set { _baseWis.Value = value; } }
        [JsonProperty]
        public long BaseDex { get { return _baseDex.Value; } set { _baseDex.Value = value; } }
        [JsonProperty]
        public long BaseCrit { get { return _baseCrit.Value; } set { _baseCrit.Value = value; } }
        [JsonProperty]
        public double BaseReflectChance { get { return _baseReflectChance.Value; } set { _baseReflectChance.Value = value; } }
        [JsonProperty]
        public double BaseReflectIntensity { get { return _baseReflectIntensity.Value; } set { _baseReflectIntensity.Value = value; } }
        [JsonProperty]
        public double BaseHealModifier { get { return _baseHealModifier.Value; } set { _baseHealModifier.Value = value; } }
        [JsonProperty]
        public double BaseDamageModifier { get { return _baseDamageModifier.Value; } set { _baseDamageModifier.Value = value; } }

        public long BonusHp { get { return _bonusHp.Value; } set { _bonusHp.Value = value; } }
        public long BonusMp { get { return _bonusMp.Value; } set { _bonusMp.Value = value; } }
        public long BonusStr { get { return _bonusStr.Value; } set { _bonusStr.Value = value; } }
        public long BonusInt { get { return _bonusInt.Value; } set { _bonusInt.Value = value; } }
        public long BonusCon { get { return _bonusCon.Value; } set { _bonusCon.Value = value; } }
        public long BonusWis { get { return _bonusWis.Value; } set { _bonusWis.Value = value; } }
        public long BonusDex { get { return _bonusDex.Value; } set { _bonusDex.Value = value; } }
        public long BonusDmg { get { return _bonusDmg.Value; } set { _bonusDmg.Value = value; } }
        public long BonusHit { get { return _bonusHit.Value; } set { _bonusHit.Value = value; } }
        public long BonusAc { get { return _bonusAc.Value; } set { _bonusAc.Value = value; } }
        public long BonusMr { get { return _bonusMr.Value; } set { _bonusMr.Value = value; } }
        public long BonusRegen { get { return _bonusRegen.Value; } set { _bonusRegen.Value = value; } }
        public double BonusReflectChance { get { return _bonusReflectChance.Value; } set { _bonusReflectChance.Value = value; } }
        public double BonusReflectIntensity { get { return _bonusReflectIntensity.Value; } set { _bonusReflectIntensity.Value = value; } }
        public double BonusHealModifier { get { return _bonusHealModifier.Value; } set { _bonusHealModifier.Value = value; } }
        public double BonusDamageModifier { get { return _bonusDamageModifier.Value; } set { _bonusDamageModifier.Value = value; } }

        #endregion

        public StatInfo(bool defaultAttr = true)
        {
            // TODO: DRY
            _level = new Lockable<byte>(1);
            _experience = new Lockable<uint>(0);
            _ability = new Lockable<byte>(0);
            _abilityExp = new Lockable<uint>(0);
            _hp = new Lockable<uint>(50);
            _mp = new Lockable<uint>(50);
            _baseHp = new Lockable<long>(50);
            _baseMp = new Lockable<long>(50);
            _baseStr = new Lockable<long>(defaultAttr ? 3 : 0);
            _baseInt = new Lockable<long>(defaultAttr ? 3 : 0);
            _baseWis = new Lockable<long>(defaultAttr ? 3 : 0);
            _baseCon = new Lockable<long>(defaultAttr ? 3 : 0);
            _baseDex = new Lockable<long>(defaultAttr ? 3 : 0);
            _bonusHp = new Lockable<long>(0);
            _bonusMp = new Lockable<long>(0);
            _bonusStr = new Lockable<long>(0);
            _bonusInt = new Lockable<long>(0);
            _bonusWis = new Lockable<long>(0);
            _bonusCon = new Lockable<long>(0);
            _bonusDex = new Lockable<long>(0);
            _bonusDmg = new Lockable<long>(0);
            _bonusHit = new Lockable<long>(0);
            _bonusAc = new Lockable<long>(0);
            _bonusMr = new Lockable<long>(0);
            _bonusRegen = new Lockable<long>(0);
            _baseOffensiveElement = new Lockable<Xml.ElementType>(Xml.ElementType.None);
            _baseDefensiveElement = new Lockable<Xml.ElementType>(Xml.ElementType.None);
            _offensiveElementOverride = new Lockable<Xml.ElementType>(Xml.ElementType.None);
            _defensiveElementOverride = new Lockable<Xml.ElementType>(Xml.ElementType.None);
            _baseReflectChance = new Lockable<double>(0);
            _bonusReflectChance = new Lockable<double>(0);
            _damageTypeOverride = new Lockable<Xml.DamageType?>(null);
            _baseReflectIntensity = new Lockable<double>(1);
            _bonusReflectIntensity = new Lockable<double>(0);
            _baseHealModifier = new Lockable<double>(1);
            _bonusHealModifier = new Lockable<double>(0);
            _baseDamageModifier = new Lockable<double>(1);
            _bonusDamageModifier = new Lockable<double>(0);
            _random = new Lockable<Random>(new Random());
        }

        #region Accessors for base stats
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
                    return 1;

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
                    return 1;

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

        public Xml.ElementType OffensiveElement
        {
            get
            {
                return (OffensiveElementOverride == Xml.ElementType.None ? OffensiveElementOverride : BaseOffensiveElement);
            }
        }
        public Xml.ElementType DefensiveElement
        {
            get
            {
                return (DefensiveElementOverride == Xml.ElementType.None ? DefensiveElementOverride : BaseDefensiveElement);
            }
        }

        #endregion

        #region Accessors for auxiliary stats
        public double ReflectChance
        {

            get
            {
                var value = BaseReflectChance + BonusReflectChance;

                if (value > 1.0)
                    return 1.0;

                if (value < 0)
                    return 0;

                return value;
            }
        }

        public double ReflectIntensity
        {

            get
            {
                var value = BaseReflectChance + BonusReflectChance;

                if (value < 0)
                    return 0;

                return value;
            }
        }

        public bool IsReflected => _random.Value.NextDouble() >= ReflectChance;

        public double HealModifier => BaseHealModifier + BonusHealModifier;
        public double DamageModifier => BaseDamageModifier + BonusDamageModifier;
    }
    #endregion

}
