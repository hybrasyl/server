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
        private Lockable<double> _reflectMagicalChance { get; set; }
        private Lockable<double> _reflectMagicalIntensity { get; set; }
        private Lockable<double> _reflectPhysicalChance { get; set; }
        private Lockable<double> _reflectPhysicalIntensity { get; set; }
        private Lockable<double> _bonusGoldChance { get; set; }
        private Lockable<double> _bonusGoldIntensity { get; set; }
        private Lockable<double> _dodge { get; set; }
        private Lockable<double> _bonusXpChance { get; set; }
        private Lockable<double> _bonusXpIntensity { get; set; }
        private Lockable<double> _bonusItemFind { get; set; }

        #endregion

        // Publicly accessible getters/setters, relying on the lockables

        #region accessors

        public decimal HpPercentage => (decimal) Hp / MaximumHp * 100m;

        [JsonProperty]
        public Xml.ElementType BaseOffensiveElement
        {
            get => _baseOffensiveElement.Value;
            set => _baseOffensiveElement.Value = value;
        }

        [JsonProperty]
        public Xml.ElementType BaseDefensiveElement
        {
            get => _baseDefensiveElement.Value;
            set => _baseDefensiveElement.Value = value;
        }

        [JsonProperty]
        public Xml.ElementType OffensiveElementOverride
        {
            get => _offensiveElementOverride.Value;
            set => _offensiveElementOverride.Value = value;
        }

        [JsonProperty]
        public Xml.ElementType DefensiveElementOverride
        {
            get => _defensiveElementOverride.Value;
            set => _defensiveElementOverride.Value = value;
        }

        [JsonProperty]
        public byte Level
        {
            get => _level.Value;
            set => _level.Value = value;
        }

        [JsonProperty]
        public uint Experience
        {
            get => _experience.Value;
            set => _experience.Value = value;
        }

        [JsonProperty]
        public byte Ability
        {
            get => _ability.Value;
            set => _ability.Value = value;
        }

        [JsonProperty]
        public uint AbilityExp
        {
            get => _abilityExp.Value;
            set => _abilityExp.Value = value;
        }

        [JsonProperty]
        public uint Hp
        {
            get => _hp.Value;
            set => _hp.Value = value;
        }

        [JsonProperty]
        public uint Mp
        {
            get => _mp.Value;
            set => _mp.Value = value;
        }

        [JsonProperty]
        public long BaseHp
        {
            get => _baseHp.Value;
            set => _baseHp.Value = value;
        }

        [JsonProperty]
        public long BaseMp
        {
            get => _baseMp.Value;
            set => _baseMp.Value = value;
        }

        [JsonProperty]
        public long BaseStr
        {
            get => _baseStr.Value;
            set => _baseStr.Value = value;
        }

        [JsonProperty]
        public long BaseInt
        {
            get => _baseInt.Value;
            set => _baseInt.Value = value;
        }

        [JsonProperty]
        public long BaseCon
        {
            get => _baseCon.Value;
            set => _baseCon.Value = value;
        }

        [JsonProperty]
        public long BaseWis
        {
            get => _baseWis.Value;
            set => _baseWis.Value = value;
        }

        [JsonProperty]
        public long BaseDex
        {
            get => _baseDex.Value;
            set => _baseDex.Value = value;
        }

        [JsonProperty]
        public long BaseCrit
        {
            get => _baseCrit.Value;
            set => _baseCrit.Value = value;
        }

        [JsonProperty]
        public double BaseReflectChance
        {
            get => _baseReflectChance.Value;
            set => _baseReflectChance.Value = value;
        }

        [JsonProperty]
        public double BaseReflectIntensity
        {
            get => _baseReflectIntensity.Value;
            set => _baseReflectIntensity.Value = value;
        }

        [JsonProperty]
        public double BaseHealModifier
        {
            get => _baseHealModifier.Value;
            set => _baseHealModifier.Value = value;
        }

        [JsonProperty]
        public double BaseDamageModifier
        {
            get => _baseDamageModifier.Value;
            set => _baseDamageModifier.Value = value;
        }

        public long BonusHp
        {
            get => _bonusHp.Value;
            set => _bonusHp.Value = value;
        }

        public long BonusMp
        {
            get => _bonusMp.Value;
            set => _bonusMp.Value = value;
        }

        public long BonusStr
        {
            get => _bonusStr.Value;
            set => _bonusStr.Value = value;
        }

        public long BonusInt
        {
            get => _bonusInt.Value;
            set => _bonusInt.Value = value;
        }

        public long BonusCon
        {
            get => _bonusCon.Value;
            set => _bonusCon.Value = value;
        }

        public long BonusWis
        {
            get => _bonusWis.Value;
            set => _bonusWis.Value = value;
        }

        public long BonusDex
        {
            get => _bonusDex.Value;
            set => _bonusDex.Value = value;
        }

        public long BonusDmg
        {
            get => _bonusDmg.Value;
            set => _bonusDmg.Value = value;
        }

        public long BonusHit
        {
            get => _bonusHit.Value;
            set => _bonusHit.Value = value;
        }

        public long BonusAc
        {
            get => _bonusAc.Value;
            set => _bonusAc.Value = value;
        }

        public long BonusMr
        {
            get => _bonusMr.Value;
            set => _bonusMr.Value = value;
        }

        public long BonusRegen
        {
            get => _bonusRegen.Value;
            set => _bonusRegen.Value = value;
        }

        public double BonusReflectChance
        {
            get => _bonusReflectChance.Value;
            set => _bonusReflectChance.Value = value;
        }

        public double BonusReflectIntensity
        {
            get => _bonusReflectIntensity.Value;
            set => _bonusReflectIntensity.Value = value;

        }

        public double BonusHealModifier
        {
            get => _bonusHealModifier.Value;
            set => _bonusHealModifier.Value = value;
        }

        public double BonusDamageModifier
        {
            get => _bonusDamageModifier.Value;
            set => _bonusDamageModifier.Value = value;
        }

        public double ReflectMagicalChance
        {
            get => _reflectMagicalChance.Value;
            set => _reflectMagicalChance.Value = value;
        }

        public double ReflectMagicalIntensity
        {
            get => _reflectMagicalIntensity.Value;
            set => _reflectMagicalIntensity.Value = value;
        }

        public double ReflectPhysicalChance
        {
            get => _reflectPhysicalChance.Value;
            set => _reflectPhysicalChance.Value = value;
        }

        public double ReflectPhysicalIntensity
        {
            get => _reflectPhysicalIntensity.Value;
            set => _reflectPhysicalIntensity.Value = value;
        }

        public double BonusGoldChance
        {
            get => _bonusGoldChance.Value;
            set => _bonusGoldChance.Value = value;
        }

        public double BonusGoldIntensity
        {
            get => _bonusGoldIntensity.Value;
            set => _bonusGoldIntensity.Value = value;
        }

        public double Dodge
        {
            get => _dodge.Value;
            set => _dodge.Value = value;
        }

        public double BonusXpChance
        {
            get => _bonusXpChance.Value;
            set => _bonusXpChance.Value = value;
        }

        public double BonusXpIntensity
        {
            get => _bonusXpIntensity.Value;
            set => _bonusXpIntensity.Value = value;
        }

        public double BonusItemFind
        {
            get => _bonusItemFind.Value;
            set => _bonusItemFind.Value = value;
        }

        #endregion

        public override string ToString() => $"Lv {Level} Hp {Hp} Mp {Mp} Stats {Str}/{Con}/{Int}/{Wis}/{Dex}";

        public void ApplyModifier(long modifier)
        {
            BaseHp *= modifier;
            BaseDamageModifier = modifier;
            BaseMp *= modifier;
        }

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
            _baseCrit = new Lockable<long>(0);
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
            _reflectMagicalChance = new Lockable<double>(0);
            _reflectMagicalIntensity = new Lockable<double>(0);
            _reflectPhysicalChance = new Lockable<double>(0);
            _reflectPhysicalIntensity = new Lockable<double>(0);
            _bonusGoldChance = new Lockable<double>(0);
            _bonusGoldIntensity = new Lockable<double>(0);
            _dodge = new Lockable<double>(0);
            _bonusXpChance = new Lockable<double>(0);
            _bonusXpIntensity = new Lockable<double>(0);
            _bonusItemFind = new Lockable<double>(0);
        }

        #region Accessors for base stats

        // Restrict to (inclusive) range between [min, max]. Max is optional, and if its
        // not present then no upper limit will be enforced.
        private static long BindToRange(long start, long? min, long? max)
        {
            if (start < min)
                return min.GetValueOrDefault();
            return start > max ? max.GetValueOrDefault() : start;
        }

        public uint MaximumHp
        {
            get
            {
                var value = BaseHp + BonusHp;

                return value switch
                {
                    > uint.MaxValue => uint.MaxValue,
                    < uint.MinValue => 1,
                    _ => (uint) BindToRange(value, StatLimitConstants.MIN_BASE_HPMP, StatLimitConstants.MAX_BASE_HPMP)
                };
            }
        }

        public uint MaximumMp
        {
            get
            {
                var value = BaseMp + BonusMp;

                return value switch
                {
                    > uint.MaxValue => uint.MaxValue,
                    < uint.MinValue => 1,
                    _ => (uint) BindToRange(value, StatLimitConstants.MIN_BASE_HPMP, StatLimitConstants.MAX_BASE_HPMP)
                };
            }
        }

        public byte Str
        {
            get
            {
                var value = BaseStr + BonusStr;

                return value switch
                {
                    > byte.MaxValue => byte.MaxValue,
                    < byte.MinValue => byte.MinValue,
                    _ => (byte) BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT)
                };
            }
        }

        public byte Int
        {
            get
            {
                var value = BaseInt + BonusInt;

                return value switch
                {
                    > byte.MaxValue => byte.MaxValue,
                    < byte.MinValue => byte.MinValue,
                    _ => (byte) BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT)
                };
            }
        }

        public byte Wis
        {
            get
            {
                var value = BaseWis + BonusWis;

                return value switch
                {
                    > byte.MaxValue => byte.MaxValue,
                    < byte.MinValue => byte.MinValue,
                    _ => (byte) BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT)
                };
            }
        }

        public byte Con
        {
            get
            {
                var value = BaseCon + BonusCon;

                return value switch
                {
                    > byte.MaxValue => byte.MaxValue,
                    < byte.MinValue => byte.MinValue,
                    _ => (byte) BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT)
                };
            }
        }

        public byte Dex
        {
            get
            {
                var value = BaseDex + BonusDex;

                return value switch
                {
                    > byte.MaxValue => byte.MaxValue,
                    < byte.MinValue => byte.MinValue,
                    _ => (byte) BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT)
                };
            }
        }

        public byte Dmg
        {
            get
            {
                return BonusDmg switch
                {
                    > byte.MaxValue => byte.MaxValue,
                    < byte.MinValue => byte.MinValue,
                    _ => (byte) BindToRange(BonusDmg, StatLimitConstants.MIN_DMG, StatLimitConstants.MAX_DMG)
                };
            }
        }

        public byte Hit
        {
            get
            {
                return BonusHit switch
                {
                    > byte.MaxValue => byte.MaxValue,
                    < byte.MinValue => byte.MinValue,
                    _ => (byte) BindToRange(BonusHit, StatLimitConstants.MIN_HIT, StatLimitConstants.MAX_HIT)
                };
            }
        }

        public sbyte Ac
        {
            get
            {
                var value = 100 - Level / 3 + BonusAc;

                return value switch
                {
                    > sbyte.MaxValue => sbyte.MaxValue,
                    < sbyte.MinValue => sbyte.MinValue,
                    _ => (sbyte) BindToRange(value, StatLimitConstants.MIN_AC, StatLimitConstants.MAX_AC)
                };
            }
        }

        public sbyte Mr
        {
            get
            {
                return BonusMr switch
                {
                    > sbyte.MaxValue => sbyte.MaxValue,
                    < sbyte.MinValue => sbyte.MinValue,
                    _ => (sbyte) BindToRange(BonusMr, StatLimitConstants.MIN_MR, StatLimitConstants.MAX_MR)
                };
            }
        }

        public sbyte Regen
        {
            get
            {
                return BonusRegen switch
                {
                    > sbyte.MaxValue => sbyte.MaxValue,
                    < sbyte.MinValue => sbyte.MinValue,
                    _ => (sbyte) BonusRegen
                };
            }
        }

        public Xml.ElementType OffensiveElement => (OffensiveElementOverride == Xml.ElementType.None
            ? OffensiveElementOverride
            : BaseOffensiveElement);

        public Xml.ElementType DefensiveElement => (DefensiveElementOverride == Xml.ElementType.None
            ? DefensiveElementOverride
            : BaseDefensiveElement);

        #endregion

        #region Accessors for auxiliary stats

        public double ReflectChance
        {

            get
            {
                var value = BaseReflectChance + BonusReflectChance;

                return value switch
                {
                    > 1.0 => 1.0,
                    < 0 => 0,
                    _ => value
                };
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
