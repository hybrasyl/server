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

namespace Hybrasyl.Objects;

[JsonObject(MemberSerialization.OptIn)]
public class StatInfo
{
    // The actual lockable private properties

    #region lockables (base stats)

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

    #endregion

    #region Lockables (auxiliary stats)
    private Lockable<double> _baseInboundDamageModifier { get; set; }
    private Lockable<double> _bonusInboundDamageModifier { get; set; }
    private Lockable<double> _baseOutboundDamageModifier { get; set; }
    private Lockable<double> _bonusOutboundDamageModifier { get; set; }
    private Lockable<double> _baseInboundHealModifier { get; set; }
    private Lockable<double> _bonusInboundHealModifier { get; set; }
    private Lockable<double> _baseOutboundHealModifier { get; set; }
    private Lockable<double> _bonusOutboundHealModifier { get; set; }
    private Lockable<double> _baseReflectMagical { get; set; }
    private Lockable<double> _bonusReflectMagical { get; set; }
    private Lockable<double> _baseReflectPhysical { get; set; }
    private Lockable<double> _bonusReflectPhysical { get; set; }
    private Lockable<double> _baseExtraGold { get; set; }
    private Lockable<double> _bonusExtraGold { get; set; }
    private Lockable<double> _baseDodge { get; set; }
    private Lockable<double> _bonusDodge { get; set; }
    private Lockable<double> _baseExtraXp { get; set; }
    private Lockable<double> _bonusExtraXp { get; set; }
    private Lockable<double> _baseExtraItemFind { get; set; }
    private Lockable<double> _bonusExtraItemFind { get; set; }
    private Lockable<double> _baseLifeSteal { get; set; }
    private Lockable<double> _bonusLifeSteal { get; set; }
    private Lockable<double> _baseManaSteal { get; set; }
    private Lockable<double> _bonusManaSteal { get; set; }
    #endregion  
    // Publicly accessible getters/setters, relying on the lockables

    public decimal HpPercentage => (decimal)Hp / MaximumHp * 100m;

    #region Standard stats (base/bonus)

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

    #endregion

    #region Auxiliary stats (base/bonus)

    [JsonProperty]
    public double BaseInboundDamageModifier
    {
        get => _baseInboundDamageModifier.Value;
        set => _baseInboundDamageModifier.Value = value;
    }

    public double BonusInboundDamageModifier
    {
        get => _bonusInboundDamageModifier.Value;
        set => _bonusInboundDamageModifier.Value = value;
    }

    [JsonProperty]
    public double BaseOutboundDamageModifier
    {
        get => _baseOutboundDamageModifier.Value;
        set => _baseOutboundDamageModifier.Value = value;
    }

    public double BonusOutboundDamageModifier
    {
        get => _bonusOutboundDamageModifier.Value;
        set => _bonusOutboundDamageModifier.Value = value;
    }

    [JsonProperty]
    public double BaseInboundHealModifier
    {
        get => _baseInboundHealModifier.Value;
        set => _baseInboundHealModifier.Value = value;
    }

    public double BonusInboundHealModifier
    {
        get => _bonusInboundHealModifier.Value;
        set => _bonusInboundHealModifier.Value = value;
    }

    [JsonProperty]
    public double BaseOutboundHealModifier
    {
        get => _baseOutboundHealModifier.Value;
        set => _baseOutboundHealModifier.Value = value;
    }

    public double BonusOutboundHealModifier
    {
        get => _bonusOutboundHealModifier.Value;
        set => _bonusOutboundHealModifier.Value = value;
    }

    [JsonProperty]
    public double BaseReflectMagical
    {
        get => _baseReflectMagical.Value;
        set => _baseReflectMagical.Value = value;
    }

    public double BonusReflectMagical
    {
        get => _bonusReflectMagical.Value;
        set => _bonusReflectMagical.Value = value;
    }

    [JsonProperty]
    public double BaseReflectPhysical
    {
        get => _baseReflectPhysical.Value;
        set => _baseReflectPhysical.Value = value;
    }

    public double BonusReflectPhysical
    {
        get => _bonusReflectPhysical.Value;
        set => _bonusReflectPhysical.Value = value;
    }

    [JsonProperty]
    public double BaseExtraGold
    {
        get => _baseExtraGold.Value;
        set => _baseExtraGold.Value = value;
    }

    public double BonusExtraGold
    {
        get => _bonusExtraGold.Value;
        set => _bonusExtraGold.Value = value;
    }

    [JsonProperty]
    public double BaseDodge
    {
        get => _baseDodge.Value;
        set => _baseDodge.Value = value;
    }

    public double BonusDodge
    {
        get => _bonusDodge.Value;
        set => _bonusDodge.Value = value;
    }

    [JsonProperty]
    public double BaseExtraXp
    {
        get => _baseExtraXp.Value;
        set => _baseExtraXp.Value = value;
    }

    public double BonusExtraXp
    {
        get => _bonusExtraXp.Value;
        set => _bonusExtraXp.Value = value;
    }

    [JsonProperty]
    public double BaseExtraItemFind
    {
        get => _baseExtraItemFind.Value;
        set => _baseExtraItemFind.Value = value;
    }

    public double BonusExtraItemFind
    {
        get => _bonusExtraItemFind.Value;
        set => _bonusExtraItemFind.Value = value;
    }

    [JsonProperty]
    public double BaseLifeSteal
    {
        get => _baseLifeSteal.Value;
        set => _baseLifeSteal.Value = value;
    }

    public double BonusLifeSteal
    {
        get => _bonusLifeSteal.Value;
        set => _bonusLifeSteal.Value = value;
    }

    [JsonProperty]
    public double BaseManaSteal
    {
        get => _baseManaSteal.Value;
        set => _baseManaSteal.Value = value;
    }

    public double BonusManaSteal
    {
        get => _bonusManaSteal.Value;
        set => _bonusManaSteal.Value = value;
    }


    #endregion

    public override string ToString() => $"Lv {Level} Hp {Hp} Mp {Mp} Stats {Str}/{Con}/{Int}/{Wis}/{Dex}";

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
        _baseInboundDamageModifier = new Lockable<double>(0);
        _bonusInboundDamageModifier = new Lockable<double>(0);
        _baseOutboundDamageModifier = new Lockable<double>(0);
        _bonusOutboundDamageModifier = new Lockable<double>(0);
        _baseInboundHealModifier = new Lockable<double>(0);
        _bonusInboundHealModifier = new Lockable<double>(0);
        _baseOutboundHealModifier = new Lockable<double>(0);
        _bonusOutboundHealModifier = new Lockable<double>(0);
        _baseReflectMagical = new Lockable<double>(0);
        _bonusReflectMagical = new Lockable<double>(0);
        _baseReflectPhysical = new Lockable<double>(0);
        _bonusReflectPhysical = new Lockable<double>(0);
        _baseExtraGold = new Lockable<double>(0);
        _bonusExtraGold = new Lockable<double>(0);
        _baseDodge = new Lockable<double>(0);
        _bonusDodge = new Lockable<double>(0);
        _baseExtraXp = new Lockable<double>(0);
        _bonusExtraXp  = new Lockable<double>(0);
        _baseExtraItemFind = new Lockable<double>(0);
        _bonusExtraItemFind = new Lockable<double>(0);
        _baseLifeSteal = new Lockable<double>(0);
        _bonusLifeSteal = new Lockable<double>(0);
        _baseManaSteal = new Lockable<double>(0);
        _bonusManaSteal = new Lockable<double>(0);
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
    public double InboundDamageModifier => _baseInboundDamageModifier.Value + _bonusInboundDamageModifier.Value;
    public double OutboundDamageModifier => _baseOutboundDamageModifier.Value + _bonusOutboundDamageModifier.Value;
    public double InboundHealModifier => _baseInboundHealModifier.Value + _bonusInboundHealModifier.Value;
    public double OutboundHealModifier => _baseOutboundHealModifier.Value + _bonusOutboundHealModifier.Value;
    public double ReflectMagical => _baseReflectMagical.Value + _bonusReflectMagical.Value;
    public double ReflectPhysical => _baseReflectPhysical.Value + _bonusReflectPhysical.Value;
    public double ExtraGold => _baseExtraGold.Value + _bonusExtraGold.Value;
    public double Dodge => _baseDodge.Value + _bonusDodge.Value;
    public double ExtraXp => _baseExtraXp.Value + _bonusExtraXp.Value;
    public double ExtraItemFind => _baseExtraItemFind.Value + _bonusExtraItemFind.Value;
    public double LifeSteal => _baseLifeSteal.Value + _bonusLifeSteal.Value;
    public double ManaSteal => _baseManaSteal.Value + _bonusManaSteal.Value;


}

#endregion