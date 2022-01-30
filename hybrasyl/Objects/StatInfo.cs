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
using Hybrasyl.Enums;
using Hybrasyl.Threading;
using Newtonsoft.Json;

namespace Hybrasyl.Objects;

/// <summary>
/// Any property with this attribute set is exposed to the formula parser with the name of the property (eg in a formula, $BASEHP).
/// This can be set on any type used by the FormulaEvaluation class (see FormulaParser)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FormulaVariable : Attribute
{
}

/// <summary>
/// Any property with this attribute set can be impacted by a status, with the expectation that there is a 1:1 mapping
/// of the name of the property in StatModifiers.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class StatusAttribute : Attribute
{
}


[JsonObject(MemberSerialization.OptIn)]
public class StatInfo
{
    // The actual lockable private properties
    private object _lock = new();


    #region private properties

    private byte _level { get; set; }
    private uint _experience { get; set; } 
    private byte _ability { get; set; } 
    private uint _abilityExp { get; set; }
    private uint _currentHp { get; set; }
    private uint _currentMp { get; set; }
    private long _deltaHp { get; set; }
    private long _deltaMp { get; set; }
    private long _baseHp { get; set; }
    private long _bonusHp { get; set; } 
    private long _baseMp { get; set; }
    private long _bonusMp { get; set; }
    private long _baseStr { get; set; }
    private long _bonusStr { get; set; }
    private long _baseInt { get; set; }
    private long _bonusInt { get; set; }
    private long _baseWis { get; set; }
    private long _bonusWis { get; set; }
    private long _baseCon { get; set; }
    private long _bonusCon { get; set; }
    private long _baseDex { get; set; }
    private long _bonusDex { get; set; }
    private double _baseCrit { get; set; }
    private double _bonusCrit { get; set; }
    private double _baseMagicCrit { get; set; }
    private double _bonusMagicCrit { get; set; }
    private long _baseDmg { get; set; }
    private long _bonusDmg { get; set; }
    private long _baseHit { get; set; }
    private long _bonusHit { get; set; }
    private long _baseAc { get; set; }
    private long _bonusAc { get; set; }
    private long _baseMr { get; set; }
    private long _bonusMr { get; set; }
    private long _baseRegen { get; set; }
    private long _bonusRegen { get; set; }
    private double _baseInboundDamageModifier { get; set; } 
    private double _bonusInboundDamageModifier { get; set; } 
    private double _baseInboundHealModifier { get; set; } 
    private double _bonusInboundHealModifier { get; set; }
    private double _baseOutboundDamageModifier { get; set; }
    private double _bonusOutboundDamageModifier { get; set; }
    private double _baseOutboundHealModifier { get; set; }
    private double _bonusOutboundHealModifier { get; set; }
    private double _baseReflectMagical { get; set; }
    private double _bonusReflectMagical { get; set; }
    private double _baseReflectPhysical { get; set; }
    private double _bonusReflectPhysical { get; set; }
    private double _baseExtraGold { get; set; }
    private double _bonusExtraGold { get; set; }
    private double _baseDodge { get; set; }
    private double _bonusDodge { get; set; }
    private double _baseMagicDodge { get; set; }
    private double _bonusMagicDodge { get; set; }
    private double _baseExtraXp { get; set; }
    private double _bonusExtraXp { get; set; }
    private double _baseExtraItemFind { get; set; }
    private double _bonusExtraItemFind { get; set; }
    private double _baseLifeSteal { get; set; }
    private double _bonusLifeSteal { get; set; }
    private double _baseManaSteal { get; set; }
    private double _bonusManaSteal { get; set; }
    private Xml.ElementType _baseOffensiveElement { get; set; } = Xml.ElementType.None;
    private Xml.ElementType _baseDefensiveElement { get; set; } = Xml.ElementType.None;
    private Xml.ElementType _offensiveElementOverride { get; set; } = Xml.ElementType.None;
    private Xml.ElementType _defensiveElementOverride { get; set; } = Xml.ElementType.None;

    #endregion

    public decimal HpPercentage => (decimal)Hp / MaximumHp * 100m;

    [FormulaVariable]
    [JsonProperty]
    public byte Level
    {
        get { lock (_lock) { return _level; } }
        set { lock (_lock) { _level = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public uint Experience
    {
        get { lock (_lock) { return _experience; } }
        set { lock (_lock) { _experience = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public byte Ability
    {
        get { lock (_lock) { return _ability; } }
        set { lock (_lock) { _ability = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public uint AbilityExp
    {
        get { lock (_lock) { return _abilityExp; } } 
        set { lock (_lock) { _abilityExp = value; } }
    }
    public long DeltaHp
    {
        get { lock (_lock) { return _deltaHp; } }
        set { lock (_lock) { _deltaHp = value; } }
    }

    public long DeltaMp
    {
        get { lock (_lock) { return _deltaMp; } }
        set { lock (_lock) { _deltaMp= value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseHp
    {
        get { lock (_lock) { return _baseHp; } }
        set { lock (_lock) { _baseHp = value; } }
    }

    [FormulaVariable]
    public long BonusHp
    {
        get { lock (_lock) { return _bonusHp; } }
        set { lock (_lock) { _bonusHp = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public uint Hp
    {
        get
        {
            lock (_lock)
            {
                return _currentHp;
            }
        }
        set
        {
            lock (_lock)
            {
                _currentHp = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseMp
    {
        get { lock (_lock) { return _baseMp; } }
        set { lock (_lock) { _baseMp = value; } }
    }

    [FormulaVariable]
    public long BonusMp
    {
        get { lock (_lock) { return _bonusMp; } }
        set { lock (_lock) { _bonusMp = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public uint Mp
    {
        get
        {
            lock (_lock)
            {
                return _currentMp;
            }
        }
        set
        {
            lock (_lock)
            {
                _currentMp = value;
            }
        }
    }

    [FormulaVariable]
    public long BaseStr
    {
        get { lock (_lock) { return _baseStr; } }
        set { lock (_lock) { _baseStr = value; } }
    }
    
    [FormulaVariable]
    public long BonusStr
    {
        get { lock (_lock) { return _bonusStr; } }
        set { lock (_lock) { _bonusStr = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseInt
    {
        get { lock (_lock) { return _baseInt; } }
        set { lock (_lock) { _baseInt = value; } }
    }

    [FormulaVariable] 
    public long BonusInt
    {
        get { lock (_lock) { return _bonusInt; } }
        set { lock (_lock) { _bonusInt = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseWis
    {
        get { lock (_lock) { return _baseWis; } }
        set { lock (_lock) { _baseWis = value; } }
    }

    [FormulaVariable]
    public long BonusWis
    {
        get { lock (_lock) { return _bonusWis; } }
        set { lock (_lock) { _bonusWis = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseCon
    {
        get { lock (_lock) { return _baseCon; } }
        set { lock (_lock) { _baseCon = value; } }
    }

    [FormulaVariable]
    public long BonusCon
    {
        get { lock (_lock) { return _bonusCon; } }
        set { lock (_lock) { _bonusCon = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseDex
    {
        get { lock (_lock) { return _baseDex; } }
        set { lock (_lock) { _baseDex = value; } }
    }

    [FormulaVariable]
    public long BonusDex
    {
        get { lock (_lock) { return _bonusDex; } }
        set { lock (_lock) { _bonusDex = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public double BaseCrit
    {
        get { lock (_lock) { return _baseCrit; } }
        set { lock (_lock) { _baseCrit = value; } }
    }

    [FormulaVariable]
    public double BonusCrit
    {
        get { lock (_lock) { return _bonusCrit; } }
        set { lock (_lock) { _bonusCrit = value; } }
    }

    [FormulaVariable] public double Crit => BaseCrit + BonusCrit;

    [FormulaVariable]
    [JsonProperty]
    public double BaseMagicCrit
    {
        get { lock (_lock) { return _baseMagicCrit; } }
        set { lock (_lock) { _baseMagicCrit = value; } }
    }

    [FormulaVariable]
    public double BonusMagicCrit
    {
        get { lock (_lock) { return _bonusMagicCrit; } }
        set { lock (_lock) { _bonusMagicCrit = value; } }
    }

    [FormulaVariable] public double MagicCrit => BaseMagicCrit + BonusMagicCrit;

    [FormulaVariable]
    [JsonProperty]
    public long BaseDmg
    {
        get { lock (_lock) { return _baseDmg; } }
        set { lock (_lock) { _baseDmg = value; } }
    }

    [FormulaVariable]
    public long BonusDmg
    {
        get { lock (_lock) { return _bonusDmg; } }
        set { lock (_lock) { _bonusDmg = value; } }
    }


    [FormulaVariable]
    [JsonProperty]
    public long BaseHit
    {
        get { lock (_lock) { return _baseHit; } }
        set { lock (_lock) { _baseHit = value; } }
    }

    [FormulaVariable]
    public long BonusHit
    {
        get { lock (_lock) { return _bonusHit; } }
        set { lock (_lock) { _bonusHit = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseAc
    {
        get { lock (_lock) { return _baseAc; } }
        set { lock (_lock) { _baseAc = value; } }
    }

    [FormulaVariable]
    public long BonusAc
    {
        get { lock (_lock) { return _bonusAc; } }
        set { lock (_lock) { _bonusAc = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseMr
    {
        get { lock (_lock) { return _baseMr; } }
        set { lock (_lock) { _baseMr = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BonusMr
    {
        get { lock (_lock) { return _bonusMr; } }
        set { lock (_lock) { _bonusMr = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseRegen
    {
        get { lock (_lock) { return _baseRegen; } }
        set { lock (_lock) { _baseRegen = value; } }
    }

    [FormulaVariable]
    public long BonusRegen
    {
        get { lock (_lock) { return _bonusRegen; } }
        set { lock (_lock) { _bonusRegen = value; } }
    }

    [JsonProperty]
    public double BaseInboundDamageModifier
    {
        get { lock (_lock) { return _baseInboundDamageModifier; } }
        set { lock (_lock) { _baseInboundDamageModifier = value; } }
    }

    public double BonusInboundDamageModifier
    {
        get { lock (_lock) { return _bonusInboundDamageModifier; } }
        set { lock (_lock) { _bonusInboundDamageModifier = value; } }
    }

    public double InboundDamageModifier => BaseInboundDamageModifier + BonusInboundDamageModifier;

    [JsonProperty]
    public double BaseInboundHealModifier
    {
        get { lock (_lock) { return _baseInboundHealModifier; } }
        set { lock (_lock) { _baseInboundHealModifier = value; } }
    }

    public double BonusInboundHealModifier
    {
        get { lock (_lock) { return _bonusInboundHealModifier; } }
        set { lock (_lock) { _bonusInboundHealModifier = value; } }
    }

    public double InboundHealModifier => BaseInboundHealModifier + BonusInboundHealModifier;

    [JsonProperty]
    public double BaseOutboundDamageModifier
    {
        get { lock (_lock) { return _baseOutboundDamageModifier; } }
        set { lock (_lock) { _baseOutboundDamageModifier = value; } }
    }

    public double BonusOutboundDamageModifier
    {
        get { lock (_lock) { return _bonusOutboundDamageModifier; } }
        set { lock (_lock) { _bonusOutboundDamageModifier = value; } }
    }

    public double OutboundDamageModifier => BaseOutboundDamageModifier + BonusOutboundDamageModifier;

    [JsonProperty]
    public double BaseOutboundHealModifier
    {
        get { lock (_lock) { return _baseOutboundHealModifier; } }
        set { lock (_lock) { _baseOutboundHealModifier = value; } }
    }

    public double BonusOutboundHealModifier
    {
        get { lock (_lock) { return _bonusOutboundHealModifier; } }
        set { lock (_lock) { _bonusOutboundHealModifier = value; } }
    }

    public double OutboundHealModifier => BaseOutboundHealModifier + BonusOutboundHealModifier;

    [FormulaVariable]
    [JsonProperty]
    public double BaseReflectMagical
    {
        get { lock (_lock) { return _baseReflectMagical; } }
        set { lock (_lock) { _baseReflectMagical = value; } }
    }

    [FormulaVariable]
    public double BonusReflectMagical
    {
        get { lock (_lock) { return _bonusReflectMagical; } }
        set { lock (_lock) { _bonusReflectMagical = value; } }
    }

    [FormulaVariable] public double ReflectMagical => BaseReflectMagical + BonusReflectMagical;

    [FormulaVariable]
    [JsonProperty]
    public double BaseReflectPhysical
    {
        get { lock (_lock) { return _baseReflectPhysical; } }
        set { lock (_lock) { _baseReflectPhysical = value; } }
    }

    [FormulaVariable]
    public double BonusReflectPhysical
    {
        get { lock (_lock) { return _bonusReflectPhysical; } }
        set { lock (_lock) { _bonusReflectPhysical = value; } }
    }

    [FormulaVariable] public double ReflectPhysical => BaseReflectPhysical + BonusReflectPhysical;

    [FormulaVariable]
    [JsonProperty]
    public double BaseExtraGold
    {
        get { lock (_lock) { return _baseExtraGold; } }
        set { lock (_lock) { _baseExtraGold = value; } }
    }

    [FormulaVariable]
    public double BonusExtraGold
    {
        get { lock (_lock) { return _bonusExtraGold; } }
        set { lock (_lock) { _bonusExtraGold = value; } }
    }

    [FormulaVariable] public double ExtraGold => BaseExtraGold + BonusExtraGold;

    [FormulaVariable]
    [JsonProperty]
    public double BaseDodge
    {
        get { lock (_lock) { return _baseDodge; } }
        set { lock (_lock) { _baseDodge = value; } }
    }

    [FormulaVariable]
    public double BonusDodge
    {
        get { lock (_lock) { return _bonusDodge; } }
        set { lock (_lock) { _bonusDodge = value; } }
    }

    [FormulaVariable] public double Dodge => BaseDodge + BonusDodge;

    [FormulaVariable]
    [JsonProperty]
    public double BaseMagicDodge
    {
        get { lock (_lock) { return _baseMagicDodge; } }
        set { lock (_lock) { _baseMagicDodge = value; } }
    }

    [FormulaVariable]
    public double BonusMagicDodge
    {
        get { lock (_lock) { return _bonusMagicDodge; } }
        set { lock (_lock) { _bonusMagicDodge = value; } }
    }

    [FormulaVariable] public double MagicDodge => BaseMagicDodge + BonusMagicDodge;

    [FormulaVariable]
    [JsonProperty]
    public double BaseExtraXp
    {
        get { lock (_lock) { return _baseExtraXp; } }
        set { lock (_lock) { _baseExtraXp = value; } }
    }

    [FormulaVariable]
    public double BonusExtraXp
    {
        get { lock (_lock) { return _bonusExtraXp; } }
        set { lock (_lock) { _bonusExtraXp = value; } }
    }

    [FormulaVariable] public double ExtraXp => BaseExtraXp + BonusExtraXp;

    [FormulaVariable]
    [JsonProperty]
    public double BaseExtraItemFind
    {
        get { lock (_lock) { return _baseExtraItemFind; } }
        set { lock (_lock) { _baseExtraItemFind = value; } }
    }

    [FormulaVariable]
    public double BonusExtraItemFind
    {
        get { lock (_lock) { return _bonusExtraItemFind; } }
        set { lock (_lock) { _bonusExtraItemFind = value; } }
    }

    [FormulaVariable] public double ExtraItemFind => BaseExtraItemFind + BonusExtraItemFind;

    [FormulaVariable]
    [JsonProperty]
    public double BaseLifeSteal
    {
        get { lock (_lock) { return _baseLifeSteal; } }
        set { lock (_lock) { _baseLifeSteal = value; } }
    }

    [FormulaVariable]
    [JsonProperty]
    public double BonusLifeSteal
    {
        get { lock (_lock) { return _bonusLifeSteal; } }
        set { lock (_lock) { _bonusLifeSteal = value; } }
    }

    [FormulaVariable] public double LifeSteal => BaseLifeSteal + BonusLifeSteal;

    [FormulaVariable]
    [JsonProperty]
    public double BaseManaSteal
    {
        get { lock (_lock) { return _baseManaSteal; } }
        set { lock (_lock) { _baseManaSteal = value; } }
    }

    [FormulaVariable]
    public double BonusManaSteal
    {
        get { lock (_lock) { return _bonusManaSteal; } }
        set { lock (_lock) { _bonusManaSteal = value; } }
    }

    [FormulaVariable] public double ManaSteal => BaseManaSteal + BonusManaSteal;

    public Xml.ElementType BaseOffensiveElement
    {
        get
        {
            lock (_lock)
                return _baseOffensiveElement;
        }
        set
        {
            lock (_lock)
                _baseOffensiveElement = value;
        }
    }

    public Xml.ElementType BaseDefensiveElement
    {
        get
        {
            lock (_lock)
                return _baseDefensiveElement;
        }
        set
        {
            lock (_lock)
                _baseDefensiveElement = value;
        }
    }

    public Xml.ElementType OffensiveElementOverride
    {
        get
        {
            lock (_lock)
                return _defensiveElementOverride;
        }
        set
        {
            lock (_lock)
                _defensiveElementOverride = value;
        }
    }

    public Xml.ElementType DefensiveElementOverride
    {
        get
        {
            lock (_lock)
                return _offensiveElementOverride;
        }
        set
        {
            lock (_lock)
                _offensiveElementOverride = value;
        }
    }

    public Xml.ElementType OffensiveElement => OffensiveElementOverride == Xml.ElementType.None
        ? OffensiveElementOverride
        : BaseOffensiveElement;

    public Xml.ElementType DefensiveElement => DefensiveElementOverride == Xml.ElementType.None
        ? DefensiveElementOverride
        : BaseDefensiveElement;

    public override string ToString() => $"Lv {Level} Hp {Hp} Mp {Mp} Stats {Str}/{Con}/{Int}/{Wis}/{Dex}";

 
    private static long BindToRange(long start, long? min, long? max)
    {
        if (min != null && start < min)
            return min.GetValueOrDefault();
        else if (max != null && start > max)
            return max.GetValueOrDefault();
        else
            return start;
    }

    [FormulaVariable]
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

    [FormulaVariable]
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

    [FormulaVariable]
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

    [FormulaVariable]
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

    [FormulaVariable]
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

    [FormulaVariable]
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

    [FormulaVariable]
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

    [FormulaVariable]
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

    [FormulaVariable]
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

    [FormulaVariable]
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
    #region Functions

    /// <summary>
    /// Apply the changes of a passed StatInfo. The attributes within are applied to this StatInfo object.
    /// </summary>
    /// <param name="si1">The StatInfo object to apply to this one</param>
    /// <param name="experience">Boolean indicating whether or not to handle experience (Level/Exp/Ab/AbExp)</param>
    /// <param name="asBonus">Boolean indicating whether or not to apply the attributes in the passed object as bonuses or a base attribute change</param>
    public void Apply(StatInfo si1, bool experience = false, bool asBonus = false)
    {
        // Always apply current hp/mp changes
        var hp = (long) Hp;
        hp += DeltaHp;
        if (hp < 0) hp = 0;
        Hp = (uint) BindToRange(hp, 0, uint.MaxValue);
        var mp  = (long) Mp;
        mp += DeltaMp;
        if (mp < 0) mp = 0;
        Mp = (uint) BindToRange(mp, 0, uint.MaxValue);

        if (asBonus)
        {
            BonusHp += si1.BonusHp;
            BonusMp += si1.BonusMp;
            BonusStr += si1.Str;
            BonusCon += si1.Con;
            BonusDex += si1.Dex;
            BonusInt += si1.Int;
            BonusWis += si1.Wis;
            BonusCrit += si1.Crit;
            BonusDmg += si1.Dmg;
            BonusHit += si1.Hit;
            BonusAc += si1.Ac;
            BonusMr += si1.Mr;
            BonusRegen += si1.Regen;
            BonusInboundHealModifier += si1.InboundHealModifier;
            BonusOutboundDamageModifier += si1.OutboundDamageModifier;
            BonusOutboundHealModifier += si1.OutboundHealModifier;
            BonusReflectMagical += si1.ReflectMagical;
            BonusReflectPhysical += si1.ReflectPhysical;
            BonusExtraGold += si1.ExtraGold;
            BonusDodge += si1.Dodge;
            BonusExtraXp += si1.ExtraXp;
            BonusExtraItemFind += si1.ExtraItemFind;
            BaseLifeSteal += si1.LifeSteal;
            BaseManaSteal += si1.ManaSteal;
        }
        else
        {
            BaseHp += si1.BaseHp;
            BaseMp += si1.BaseMp;
            BaseStr += si1.Str;
            BaseCon += si1.Con;
            BaseDex += si1.Dex;
            BaseInt += si1.Int;
            BaseWis += si1.Wis;
            BaseCrit += si1.Crit;
            BaseDmg += si1.Dmg;
            BaseHit += si1.Hit;
            BaseAc += si1.Ac;
            BaseMr += si1.Mr;
            BaseRegen += si1.Regen;
            BaseInboundHealModifier += si1.InboundHealModifier;
            BaseOutboundDamageModifier += si1.OutboundDamageModifier;
            BaseOutboundHealModifier += si1.OutboundHealModifier;
            BaseReflectMagical += si1.ReflectMagical;
            BaseReflectPhysical += si1.ReflectPhysical;
            BaseExtraGold += si1.ExtraGold;
            BaseDodge += si1.Dodge;
            BaseExtraXp += si1.ExtraXp;
            BaseExtraItemFind += si1.ExtraItemFind;
            BaseLifeSteal += si1.LifeSteal;
            BaseManaSteal += si1.ManaSteal;
        }

        if (!experience) return;
        Level += si1.Level;
        Experience += si1.Experience;
        Ability += si1.Ability;
        AbilityExp += si1.AbilityExp;
    }

    /// <summary>
    /// Remove the changes of a passed StatInfo. The attributes within are applied to this StatInfo object.
    /// </summary>
    /// <param name="si1">The StatInfo object to apply to this one</param>
    /// <param name="experience">Boolean indicating whether or not to handle experience (Level/Exp/Ab/AbExp)</param>
    /// <param name="asBonus">Boolean indicating whether or not to remove the attributes in the passed object as bonuses or a base attribute change</param>
    public void Remove(StatInfo si1, bool experience=false, bool asBonus = false)
    {
        if (asBonus)
        {
            BonusHp -= si1.Hp;
            BonusMp -= si1.Mp;
            BonusStr -= si1.Str;
            BonusCon -= si1.Con;
            BonusDex -= si1.Dex;
            BonusInt -= si1.Int;
            BonusWis -= si1.Wis;
            BonusCrit -= si1.Crit;
            BonusDmg -= si1.Dmg;
            BonusHit -= si1.Hit;
            BonusAc -= si1.Ac;
            BonusMr -= si1.Mr;
            BonusRegen -= si1.Regen;
            BonusInboundHealModifier -= si1.InboundHealModifier;
            BonusOutboundDamageModifier -= si1.OutboundDamageModifier;
            BonusOutboundHealModifier -= si1.OutboundHealModifier;
            BonusReflectMagical -= si1.ReflectMagical;
            BonusReflectPhysical -= si1.ReflectPhysical;
            BonusExtraGold -= si1.ExtraGold;
            BonusDodge -= si1.Dodge;
            BonusExtraXp -= si1.ExtraXp;
            BonusExtraItemFind -= si1.ExtraItemFind;
            BaseLifeSteal -= si1.LifeSteal;
            BaseManaSteal -= si1.ManaSteal;
        }
        else
        {
            BaseHp -= si1.Hp;
            BaseMp -= si1.Mp;
            BaseStr -= si1.Str;
            BaseCon -= si1.Con;
            BaseDex -= si1.Dex;
            BaseInt -= si1.Int;
            BaseWis -= si1.Wis;
            BaseCrit -= si1.Crit;
            BaseDmg -= si1.Dmg;
            BaseHit -= si1.Hit;
            BaseAc -= si1.Ac;
            BaseMr -= si1.Mr;
            BaseRegen -= si1.Regen;
            BaseInboundHealModifier -= si1.InboundHealModifier;
            BaseOutboundDamageModifier -= si1.OutboundDamageModifier;
            BaseOutboundHealModifier -= si1.OutboundHealModifier;
            BaseReflectMagical -= si1.ReflectMagical;
            BaseReflectPhysical -= si1.ReflectPhysical;
            BaseExtraGold -= si1.ExtraGold;
            BaseDodge -= si1.Dodge;
            BaseExtraXp -= si1.ExtraXp;
            BaseExtraItemFind -= si1.ExtraItemFind;
            BaseLifeSteal -= si1.LifeSteal;
            BaseManaSteal -= si1.ManaSteal;
        }

        if (!experience) return;
        Level -= si1.Level;
        Experience -= si1.Experience;
        Ability -= si1.Ability;
        AbilityExp -= si1.AbilityExp;
    }

    /// <summary>
    /// Convenience function for applying this StatInfo as a base attribute change (eg this.BaseStr -= si1.Str)
    /// </summary>
    /// <param name="si1">The StatInfo object to apply to this one</param>
    /// <param name="experience">Boolean indicating whether or not to handle experience</param>
    public void ApplyBase(StatInfo si1, bool experience = false) => Apply(si1, experience);

    /// <summary>
    /// Convenience function for applying this StatInfo as a bonus attribute change (eg this.BonusStr += si1.Str)
    /// </summary>
    /// <param name="si1">The StatInfo object to apply to this one</param>
    /// <param name="experience">Boolean indicating whether or not to handle experience</param>
    public void ApplyBonus(StatInfo si1, bool experience = false) => Apply(si1, experience, true);

    /// <summary>
    /// Convenience function for removing this StatInfo as a base attribute change (eg this.BaseStr -= si1.Str)
    /// </summary>
    /// <param name="si1">The StatInfo object to apply to this one</param>
    /// <param name="experience">Boolean indicating whether or not to handle experience</param>
    public void RemoveBase(StatInfo si1, bool experience = false) => Remove(si1, experience);
    
    /// <summary>
    /// Convenience function for removing this StatInfo as a bonus attribute change (eg this.BonusStr -= si1.Str)
    /// </summary>
    /// <param name="si1">The StatInfo object to apply to this one</param>
    /// <param name="experience">Boolean indicating whether or not to handle experience</param>
    public void RemoveBonus(StatInfo si1, bool experience = false) => Remove(si1, experience, true);


    #endregion

}


