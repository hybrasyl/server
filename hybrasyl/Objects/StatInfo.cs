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

using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using System;

namespace Hybrasyl.Objects;

/// <summary>
///     Any property with this attribute set is exposed to the formula parser with the name of the property (eg in a
///     formula, $BASEHP).
///     This can be set on any type used by the FormulaEvaluation class (see FormulaParser)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FormulaVariable : Attribute { }

/// <summary>
///     Any property with this attribute set can be impacted by a status, with the expectation that there is a 1:1 mapping
///     of the name of the property in StatModifiers.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class StatusAttribute : Attribute { }

[JsonObject(MemberSerialization.OptIn)]
[MoonSharpUserData]
public class StatInfo
{
    // The actual lockable private properties
    private object _lock = new();

    public decimal HpPercentage => (decimal)Hp / MaximumHp * 100m;

    [FormulaVariable]
    [JsonProperty]
    public byte Level
    {
        get
        {
            lock (_lock)
            {
                return _level;
            }
        }
        set
        {
            lock (_lock)
            {
                _level = value;
            }
        }
    }

    [FormulaVariable]
    public int FormulaLevel => Level;

    [FormulaVariable]
    [JsonProperty]
    public uint Experience
    {
        get
        {
            lock (_lock)
            {
                return _experience;
            }
        }
        set
        {
            lock (_lock)
            {
                _experience = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public int Faith
    {
        get
        {
            lock (_lock)
                return _faith;
        }
        set
        {
            lock (_lock)
                _faith = value;
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public uint Gold
    {
        get
        {
            lock (_lock)
            {
                return _gold;
            }
        }
        set
        {
            lock (_lock)
            {
                _gold = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public byte Ability
    {
        get
        {
            lock (_lock)
            {
                return _ability;
            }
        }
        set
        {
            lock (_lock)
            {
                _ability = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public uint AbilityExp
    {
        get
        {
            lock (_lock)
            {
                return _abilityExp;
            }
        }
        set
        {
            lock (_lock)
            {
                _abilityExp = value;
            }
        }
    }

    public long DeltaHp
    {
        get
        {
            lock (_lock)
            {
                return _deltaHp;
            }
        }
        set
        {
            lock (_lock)
            {
                _deltaHp = value;
            }
        }
    }

    public long DeltaMp
    {
        get
        {
            lock (_lock)
            {
                return _deltaMp;
            }
        }
        set
        {
            lock (_lock)
            {
                _deltaMp = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseHp
    {
        get
        {
            lock (_lock)
            {
                return _baseHp;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseHp = value;
            }
        }
    }

    [FormulaVariable]
    public long BonusHp
    {
        get
        {
            lock (_lock)
            {
                return _bonusHp;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusHp = value;
            }
        }
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
                _currentHp = value > MaximumHp ? MaximumHp : value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseMp
    {
        get
        {
            lock (_lock)
            {
                return _baseMp;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseMp = value;
            }
        }
    }

    [FormulaVariable]
    public long BonusMp
    {
        get
        {
            lock (_lock)
            {
                return _bonusMp;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusMp = value;
            }
        }
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
                _currentMp = value > MaximumMp ? MaximumMp : value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseStr
    {
        get
        {
            lock (_lock)
            {
                return _baseStr;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseStr = value;
            }
        }
    }

    [FormulaVariable]
    public long BonusStr
    {
        get
        {
            lock (_lock)
            {
                return _bonusStr;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusStr = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseInt
    {
        get
        {
            lock (_lock)
            {
                return _baseInt;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseInt = value;
            }
        }
    }

    [FormulaVariable]
    public long BonusInt
    {
        get
        {
            lock (_lock)
            {
                return _bonusInt;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusInt = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseWis
    {
        get
        {
            lock (_lock)
            {
                return _baseWis;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseWis = value;
            }
        }
    }

    [FormulaVariable]
    public long BonusWis
    {
        get
        {
            lock (_lock)
            {
                return _bonusWis;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusWis = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseCon
    {
        get
        {
            lock (_lock)
            {
                return _baseCon;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseCon = value;
            }
        }
    }

    [FormulaVariable]
    public long BonusCon
    {
        get
        {
            lock (_lock)
            {
                return _bonusCon;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusCon = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseDex
    {
        get
        {
            lock (_lock)
            {
                return _baseDex;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseDex = value;
            }
        }
    }

    [FormulaVariable]
    public long BonusDex
    {
        get
        {
            lock (_lock)
            {
                return _bonusDex;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusDex = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public double BaseCrit
    {
        get
        {
            lock (_lock)
            {
                return _baseCrit;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseCrit = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusCrit
    {
        get
        {
            lock (_lock)
            {
                return _bonusCrit;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusCrit = value;
            }
        }
    }

    [FormulaVariable] public double Crit => BaseCrit + BonusCrit;

    [FormulaVariable]
    [JsonProperty]
    public double BaseMagicCrit
    {
        get
        {
            lock (_lock)
            {
                return _baseMagicCrit;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseMagicCrit = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusMagicCrit
    {
        get
        {
            lock (_lock)
            {
                return _bonusMagicCrit;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusMagicCrit = value;
            }
        }
    }

    [FormulaVariable] public double MagicCrit => BaseMagicCrit + BonusMagicCrit;

    [FormulaVariable]
    [JsonProperty]
    public double BaseDmg
    {
        get
        {
            lock (_lock)
            {
                return _baseDmg;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseDmg = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusDmg
    {
        get
        {
            lock (_lock)
            {
                return _bonusDmg;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusDmg = value;
            }
        }
    }


    [FormulaVariable]
    [JsonProperty]
    public double BaseHit
    {
        get
        {
            lock (_lock)
            {
                return _baseHit;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseHit = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusHit
    {
        get
        {
            lock (_lock)
            {
                return _bonusHit;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusHit = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public long BaseAc
    {
        get
        {
            lock (_lock)
            {
                return _baseAc;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseAc = value;
            }
        }
    }

    [FormulaVariable]
    public long BonusAc
    {
        get
        {
            lock (_lock)
            {
                return _bonusAc;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusAc = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public double BaseMr
    {
        get
        {
            lock (_lock)
            {
                return _baseMr;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseMr = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusMr
    {
        get
        {
            lock (_lock)
            {
                return _bonusMr;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusMr = value;
            }
        }
    }

    [FormulaVariable]
    [JsonProperty]
    public double BaseRegen
    {
        get
        {
            lock (_lock)
            {
                return _baseRegen;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseRegen = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusRegen
    {
        get
        {
            lock (_lock)
            {
                return _bonusRegen;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusRegen = value;
            }
        }
    }

    [JsonProperty]
    public double BaseInboundDamageModifier
    {
        get
        {
            lock (_lock)
            {
                return _baseInboundDamageModifier;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseInboundDamageModifier = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusInboundDamageModifier
    {
        get
        {
            lock (_lock)
            {
                return _bonusInboundDamageModifier;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusInboundDamageModifier = value;
            }
        }
    }

    [FormulaVariable] public double InboundDamageModifier => BaseInboundDamageModifier + BonusInboundDamageModifier;

    [JsonProperty]
    [FormulaVariable]
    public double BaseInboundHealModifier
    {
        get
        {
            lock (_lock)
            {
                return _baseInboundHealModifier;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseInboundHealModifier = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusInboundHealModifier
    {
        get
        {
            lock (_lock)
            {
                return _bonusInboundHealModifier;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusInboundHealModifier = value;
            }
        }
    }

    public double InboundHealModifier => BaseInboundHealModifier + BonusInboundHealModifier;

    [JsonProperty]
    public double BaseOutboundDamageModifier
    {
        get
        {
            lock (_lock)
            {
                return _baseOutboundDamageModifier;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseOutboundDamageModifier = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusOutboundDamageModifier
    {
        get
        {
            lock (_lock)
            {
                return _bonusOutboundDamageModifier;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusOutboundDamageModifier = value;
            }
        }
    }

    [FormulaVariable] public double OutboundDamageModifier => BaseOutboundDamageModifier + BonusOutboundDamageModifier;

    [JsonProperty]
    [FormulaVariable]
    public double BaseOutboundHealModifier
    {
        get
        {
            lock (_lock)
            {
                return _baseOutboundHealModifier;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseOutboundHealModifier = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusOutboundHealModifier
    {
        get
        {
            lock (_lock)
            {
                return _bonusOutboundHealModifier;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusOutboundHealModifier = value;
            }
        }
    }

    [FormulaVariable] public double OutboundHealModifier => BaseOutboundHealModifier + BonusOutboundHealModifier;

    [FormulaVariable]
    [JsonProperty]
    public double BaseReflectMagical
    {
        get
        {
            lock (_lock)
            {
                return _baseReflectMagical;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseReflectMagical = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusReflectMagical
    {
        get
        {
            lock (_lock)
            {
                return _bonusReflectMagical;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusReflectMagical = value;
            }
        }
    }

    [FormulaVariable] public double ReflectMagical => BaseReflectMagical + BonusReflectMagical;

    [FormulaVariable]
    [JsonProperty]
    public double BaseReflectPhysical
    {
        get
        {
            lock (_lock)
            {
                return _baseReflectPhysical;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseReflectPhysical = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusReflectPhysical
    {
        get
        {
            lock (_lock)
            {
                return _bonusReflectPhysical;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusReflectPhysical = value;
            }
        }
    }

    [FormulaVariable] public double ReflectPhysical => BaseReflectPhysical + BonusReflectPhysical;

    [FormulaVariable]
    [JsonProperty]
    public double BaseExtraGold
    {
        get
        {
            lock (_lock)
            {
                return _baseExtraGold;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseExtraGold = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusExtraGold
    {
        get
        {
            lock (_lock)
            {
                return _bonusExtraGold;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusExtraGold = value;
            }
        }
    }

    [FormulaVariable] public double ExtraGold => BaseExtraGold + BonusExtraGold;

    [FormulaVariable]
    [JsonProperty]
    public double BaseDodge
    {
        get
        {
            lock (_lock)
            {
                return _baseDodge;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseDodge = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusDodge
    {
        get
        {
            lock (_lock)
            {
                return _bonusDodge;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusDodge = value;
            }
        }
    }

    [FormulaVariable] public double Dodge => BaseDodge + BonusDodge;

    [FormulaVariable]
    [JsonProperty]
    public double BaseMagicDodge
    {
        get
        {
            lock (_lock)
            {
                return _baseMagicDodge;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseMagicDodge = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusMagicDodge
    {
        get
        {
            lock (_lock)
            {
                return _bonusMagicDodge;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusMagicDodge = value;
            }
        }
    }

    [FormulaVariable] public double MagicDodge => BaseMagicDodge + BonusMagicDodge;

    [FormulaVariable]
    [JsonProperty]
    public double BaseExtraXp
    {
        get
        {
            lock (_lock)
            {
                return _baseExtraXp;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseExtraXp = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusExtraXp
    {
        get
        {
            lock (_lock)
            {
                return _bonusExtraXp;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusExtraXp = value;
            }
        }
    }

    [FormulaVariable] public double ExtraXp => BaseExtraXp + BonusExtraXp;

    [FormulaVariable]
    [JsonProperty]
    public double BaseExtraItemFind
    {
        get
        {
            lock (_lock)
            {
                return _baseExtraItemFind;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseExtraItemFind = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusExtraItemFind
    {
        get
        {
            lock (_lock)
            {
                return _bonusExtraItemFind;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusExtraItemFind = value;
            }
        }
    }

    [FormulaVariable] public double ExtraItemFind => BaseExtraItemFind + BonusExtraItemFind;

    [FormulaVariable]
    [JsonProperty]
    public double BaseExtraFaith
    {
        get
        {
            lock (_lock)
                return _baseExtraFaith;

        }
        set
        {
            lock (_lock)
                _baseExtraFaith = value;
        }
    }

    [FormulaVariable]
    public double BonusExtraFaith
    {
        get
        {
            lock (_lock)
                return _bonusExtraFaith;

        }
        set
        {
            lock (_lock)
                _bonusExtraFaith = value;
        }
    }

    [FormulaVariable] public double ExtraFaith => BaseExtraFaith + BonusExtraFaith;

    [FormulaVariable]
    [JsonProperty]
    public double BaseLifeSteal
    {
        get
        {
            lock (_lock)
            {
                return _baseLifeSteal;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseLifeSteal = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusLifeSteal
    {
        get
        {
            lock (_lock)
            {
                return _bonusLifeSteal;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusLifeSteal = value;
            }
        }
    }

    [FormulaVariable] public double LifeSteal => BaseLifeSteal + BonusLifeSteal;

    [FormulaVariable]
    [JsonProperty]
    public double BaseManaSteal
    {
        get
        {
            lock (_lock)
            {
                return _baseManaSteal;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseManaSteal = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusManaSteal
    {
        get
        {
            lock (_lock)
            {
                return _bonusManaSteal;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusManaSteal = value;
            }
        }
    }

    [FormulaVariable] public double ManaSteal => BaseManaSteal + BonusManaSteal;

    [FormulaVariable]
    [JsonProperty]
    public double BaseInboundDamageToMp
    {
        get
        {
            lock (_lock)
            {
                return _baseInboundDamageToMp;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseInboundDamageToMp = value;
            }
        }
    }

    [FormulaVariable]
    public double BonusInboundDamageToMp
    {
        get
        {
            lock (_lock)
            {
                return _bonusInboundDamageToMp;
            }
        }
        set
        {
            lock (_lock)
            {
                _bonusInboundDamageToMp = value;
            }
        }
    }

    [FormulaVariable] public double InboundDamageToMp => BaseInboundDamageToMp + BonusInboundDamageToMp;

    [FormulaVariable]
    public double Shield
    {
        get => _shield;
        set => _shield = value < 0 ? 0 : value;
    }

    private double _shield { get; set; }

    public ElementType BaseOffensiveElement
    {
        get
        {
            lock (_lock)
            {
                return _baseOffensiveElement;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseOffensiveElement = value;
            }
        }
    }

    public ElementType BaseDefensiveElement
    {
        get
        {
            lock (_lock)
            {
                return _baseDefensiveElement;
            }
        }
        set
        {
            lock (_lock)
            {
                _baseDefensiveElement = value;
            }
        }
    }

    public ElementType OffensiveElementOverride
    {
        get
        {
            lock (_lock)
            {
                return _defensiveElementOverride;
            }
        }
        set
        {
            lock (_lock)
            {
                _defensiveElementOverride = value;
            }
        }
    }

    public ElementType DefensiveElementOverride
    {
        get
        {
            lock (_lock)
            {
                return _offensiveElementOverride;
            }
        }
        set
        {
            lock (_lock)
            {
                _offensiveElementOverride = value;
            }
        }
    }

    public ElementType OffensiveElement => OffensiveElementOverride == ElementType.None
        ? OffensiveElementOverride
        : BaseOffensiveElement;

    public ElementType DefensiveElement => DefensiveElementOverride == ElementType.None
        ? DefensiveElementOverride
        : BaseDefensiveElement;

    public string OffensiveElementStr => Enum.GetName(typeof(ElementType), OffensiveElement);
    public string DefensiveElementStr => Enum.GetName(typeof(ElementType), DefensiveElement);
    public string OffensiveElementOverrideStr => Enum.GetName(typeof(ElementType), OffensiveElementOverride);
    public string DefensiveElementOverrideStr => Enum.GetName(typeof(ElementType), DefensiveElementOverride);

    [FormulaVariable]
    public uint MaximumHp =>
        (uint)Math.Clamp(BaseHp + BonusHp, Game.ActiveConfiguration.Constants.PlayerMinBaseHpMp, Game.ActiveConfiguration.Constants.PlayerMaxBaseHpMp);

    [FormulaVariable]
    public uint MaximumMp =>
        (uint)Math.Clamp(BaseMp + BonusMp, Game.ActiveConfiguration.Constants.PlayerMinBaseHpMp, Game.ActiveConfiguration.Constants.PlayerMaxBaseHpMp);

    [FormulaVariable]
    public byte Str => (byte)Math.Clamp(BaseStr + BonusStr, Game.ActiveConfiguration.Constants.PlayerMinStat, Game.ActiveConfiguration.Constants.PlayerMaxStat);

    [FormulaVariable]
    public byte Int => (byte)Math.Clamp(BaseInt + BonusInt, Game.ActiveConfiguration.Constants.PlayerMinStat, Game.ActiveConfiguration.Constants.PlayerMaxStat);

    [FormulaVariable]
    public byte Wis => (byte)Math.Clamp(BaseWis + BonusWis, Game.ActiveConfiguration.Constants.PlayerMinStat, Game.ActiveConfiguration.Constants.PlayerMaxStat);

    [FormulaVariable]
    public byte Con => (byte)Math.Clamp(BaseCon + BonusCon, Game.ActiveConfiguration.Constants.PlayerMinStat, Game.ActiveConfiguration.Constants.PlayerMaxStat);

    [FormulaVariable]
    public byte Dex => (byte)Math.Clamp(BaseDex + BonusDex, Game.ActiveConfiguration.Constants.PlayerMinStat, Game.ActiveConfiguration.Constants.PlayerMaxStat);


    [FormulaVariable]
    // Normalize to a double between 0.84 / 1.16
    public double Dmg => Math.Clamp(BonusDmg, Game.ActiveConfiguration.Constants.PlayerMinDmg, Game.ActiveConfiguration.Constants.PlayerMaxDmg) + 1.0;

    // These are for the client 0x08, specifically, which has some annoying limitations.
    // MR in particular can only be displayed as multiples of 10% and no negatives can be
    // expressed by MR/DMG/HIT. 
    // We use a rating-esque system where 1280% / 128 / 128 is base, below is debuff, above is buff

    public byte MrRating
    {
        get
        {
            return Mr switch
            {
                < 1.0 => (byte) (128 - Math.Min((1.0 - Mr) * 800, 128)),
                > 1.0 => (byte) (128 + Math.Min((Mr - 1.0) * 800, 127)),
                _ => 128
            };
        }
    }

    public byte DmgRating
    {
        get
        {
            return Dmg switch
            {
                < 1.0 => (byte) (128 - Math.Min((1.0 - Dmg) * 800, 128)),
                > 1.0 => (byte) (128 + Math.Min((Dmg - 1.0) * 800, 127)),
                _ => 128
            };
        }
    }

    public byte HitRating
    {
        get
        {
            return Hit switch
            {
                < 1.0 => (byte) (128 - Math.Min((1.0 - Hit) * 800, 128)),
                > 1.0 => (byte) (128 + Math.Min((Hit - 1.0) * 800, 127)),
                _ => 128
            };
        }
    }

    [FormulaVariable]
    // Normalize to a double between -0.84 / 1.16
    public double Hit => Math.Clamp(BonusHit, Game.ActiveConfiguration.Constants.PlayerMinHit, Game.ActiveConfiguration.Constants.PlayerMaxHit) + 1.0;

    [FormulaVariable]
    public sbyte Ac =>
        (sbyte)Math.Clamp(BaseAc - Level / 3 + BonusAc, Game.ActiveConfiguration.Constants.PlayerMinAc, Game.ActiveConfiguration.Constants.PlayerMaxAc);

    [FormulaVariable]
    // Normalize to a double between -0.84 / 1.16
    public double Mr => Math.Clamp(BonusMr,  Game.ActiveConfiguration.Constants.PlayerMinMr, Game.ActiveConfiguration.Constants.PlayerMaxMr) + 1.0;

    [FormulaVariable]
    // Normalize to a double between -0.84 / 1.16
    public double Regen =>
        Math.Clamp(BonusRegen,  Game.ActiveConfiguration.Constants.PlayerMinRegen, Game.ActiveConfiguration.Constants.PlayerMaxRegen) + 1.0;

    public override string ToString() => $"Lv {Level} Hp {Hp} Mp {Mp} Stats {Str}/{Con}/{Int}/{Wis}/{Dex}";

    public ElementalModifiers ElementalModifiers
    {
        get
        {
            lock (_lock)
                return _elementalModifiers;
        }
        set
        {
            lock (_lock)
                _elementalModifiers = value;
        }
    }


    #region private properties

    private byte _level { get; set; }
    private uint _experience { get; set; }
    private int _faith { get; set; }
    private uint _gold { get; set; }
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
    private double _baseDmg { get; set; }
    private double _bonusDmg { get; set; }
    private double _baseHit { get; set; }
    private double _bonusHit { get; set; }
    private long _baseAc { get; set; }
    private long _bonusAc { get; set; }
    private double _baseMr { get; set; }
    private double _bonusMr { get; set; }
    private double _baseRegen { get; set; }
    private double _bonusRegen { get; set; }
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
    private double _baseInboundDamageToMp { get; set; }
    private double _bonusInboundDamageToMp { get; set; }
    private double _baseExtraFaith { get; set; }
    private double _bonusExtraFaith { get; set; }
    private ElementType _baseOffensiveElement { get; set; } = ElementType.None;
    private ElementType _baseDefensiveElement { get; set; } = ElementType.None;
    private ElementType _offensiveElementOverride { get; set; } = ElementType.None;
    private ElementType _defensiveElementOverride { get; set; } = ElementType.None;

    private ElementalModifiers _elementalModifiers { get; set; } = new();

    #endregion

    #region Functions

    /// <summary>
    ///     Apply the changes of a passed StatInfo. The attributes within are applied to this StatInfo object.
    /// </summary>
    /// <param name="si1">The StatInfo object to apply to this one</param>
    /// <param name="experience">Boolean indicating whether or not to handle experience (Level/Exp/Ab/AbExp)</param>
    /// <param name="asBonus">
    ///     Boolean indicating whether or not to apply the attributes in the passed object as bonuses or a
    ///     base attribute change
    /// </param>
    public void Apply(StatInfo si1, bool experience = false)
    {
        if (si1 == null || si1.Empty) return;
        // Always apply current hp/mp/gold changes
        var hp = (long)Hp;
        hp += si1.DeltaHp;
        if (hp < 0) hp = 0;
        Hp = (uint)Math.Clamp(hp, 0, uint.MaxValue);
        var mp = (long)Mp;
        mp += si1.DeltaMp;
        if (mp < 0) mp = 0;
        Mp = (uint)Math.Clamp(mp, 0, uint.MaxValue);
        var gold = Gold + si1.Gold;
        Gold = Math.Clamp(gold, 0, uint.MaxValue);

        BonusHp += si1.BonusHp;
        BonusMp += si1.BonusMp;
        BonusStr += si1.BonusStr;
        BonusCon += si1.BonusCon;
        BonusDex += si1.BonusDex;
        BonusInt += si1.BonusInt;
        BonusWis += si1.BonusWis;
        BonusCrit += si1.BonusCrit;
        BonusMagicCrit += si1.BonusMagicCrit;
        BonusDmg += si1.BonusDmg;
        BonusHit += si1.BonusHit;
        BonusAc += si1.BonusAc;
        BonusMr += si1.BonusMr;
        BonusRegen += si1.BonusRegen;
        BonusInboundHealModifier += si1.BonusInboundHealModifier;
        BonusInboundDamageModifier += si1.BonusInboundDamageModifier;
        BonusOutboundDamageModifier += si1.BonusOutboundDamageModifier;
        BonusOutboundHealModifier += si1.BonusOutboundHealModifier;
        BonusReflectMagical += si1.BonusReflectMagical;
        BonusReflectPhysical += si1.BonusReflectPhysical;
        BonusExtraGold += si1.BonusExtraGold;
        BonusDodge += si1.BonusDodge;
        BonusMagicDodge += si1.BonusMagicDodge;
        BonusExtraXp += si1.BonusExtraXp;
        BonusExtraItemFind += si1.BonusExtraItemFind;
        BonusLifeSteal += si1.BonusLifeSteal;
        BonusManaSteal += si1.BonusManaSteal;
        BonusInboundDamageToMp += si1.BonusInboundDamageToMp;
        BonusExtraFaith += si1.BonusExtraFaith;
        BaseHp += si1.BaseHp;
        BaseMp += si1.BaseMp;
        BaseStr += si1.BaseStr;
        BaseCon += si1.BaseCon;
        BaseDex += si1.BaseDex;
        BaseInt += si1.BaseInt;
        BaseWis += si1.BaseWis;
        BaseCrit += si1.BaseCrit;
        BaseMagicCrit += si1.BaseMagicCrit;
        BaseDmg += si1.BaseDmg;
        BaseHit += si1.BaseHit;
        BaseAc += si1.BaseAc;
        BaseMr += si1.BaseMr;
        BaseRegen += si1.BaseRegen;
        BaseInboundHealModifier += si1.BaseInboundHealModifier;
        BaseInboundDamageModifier += si1.BaseInboundDamageModifier;
        BaseOutboundDamageModifier += si1.BaseOutboundDamageModifier;
        BaseOutboundHealModifier += si1.BaseOutboundHealModifier;
        BaseReflectMagical += si1.BaseReflectMagical;
        BaseReflectPhysical += si1.BaseReflectPhysical;
        BaseExtraGold += si1.BaseExtraGold;
        BaseDodge += si1.BaseDodge;
        BaseMagicDodge += si1.BaseMagicDodge;
        BaseExtraXp += si1.BaseExtraXp;
        BaseExtraItemFind += si1.BaseExtraItemFind;
        BaseLifeSteal += si1.BaseLifeSteal;
        BaseManaSteal += si1.BaseManaSteal;
        BaseInboundDamageToMp += si1.BaseInboundDamageToMp;
        BaseExtraFaith += si1.BaseExtraFaith;
        Faith += si1.Faith;
        ElementalModifiers += si1.ElementalModifiers;

        if (!experience) return;
        Level += si1.Level;
        Experience += si1.Experience;
        Ability += si1.Ability;
        AbilityExp += si1.AbilityExp;
    }

    /// <summary>
    ///     Remove the changes of a passed StatInfo. The attributes within are applied to this StatInfo object.
    /// </summary>
    /// <param name="si1">The StatInfo object to apply to this one</param>
    /// <param name="experience">Boolean indicating whether or not to handle experience (Level/Exp/Ab/AbExp)</param>
    /// <param name="asBonus">
    ///     Boolean indicating whether or not to remove the attributes in the passed object as bonuses or a
    ///     base attribute change
    /// </param>
    public void Remove(StatInfo si1, bool experience = false)
    {
        if (si1 == null || si1.Empty) return;

        BonusHp -= si1.BonusHp;
        BonusMp -= si1.BonusMp;
        BonusStr -= si1.BonusStr;
        BonusCon -= si1.BonusCon;
        BonusDex -= si1.BonusDex;
        BonusInt -= si1.BonusInt;
        BonusWis -= si1.BonusWis;
        BonusCrit -= si1.BonusCrit;
        BonusMagicCrit -= si1.BonusMagicCrit;
        BonusDmg -= si1.BonusDmg;
        BonusHit -= si1.BonusHit;
        BonusAc -= si1.BonusAc;
        BonusMr -= si1.BonusMr;
        BonusRegen -= si1.BonusRegen;
        BonusInboundHealModifier -= si1.BonusInboundHealModifier;
        BonusInboundDamageModifier -= si1.BonusInboundDamageModifier;
        BonusOutboundDamageModifier -= si1.BonusOutboundDamageModifier;
        BonusOutboundHealModifier -= si1.BonusOutboundHealModifier;
        BonusReflectMagical -= si1.BonusReflectMagical;
        BonusReflectPhysical -= si1.BonusReflectPhysical;
        BonusExtraGold -= si1.BonusExtraGold;
        BonusDodge -= si1.BonusDodge;
        BonusMagicDodge -= si1.BonusMagicDodge;
        BonusExtraXp -= si1.BonusExtraXp;
        BonusExtraItemFind -= si1.BonusExtraItemFind;
        BonusLifeSteal -= si1.BonusLifeSteal;
        BonusManaSteal -= si1.BonusManaSteal;
        BonusInboundDamageToMp -= si1.BonusInboundDamageToMp;
        BonusExtraFaith -= si1.BonusExtraFaith;
        BaseHp -= si1.BaseHp;
        BaseMp -= si1.BaseMp;
        BaseStr -= si1.BaseStr;
        BaseCon -= si1.BaseCon;
        BaseDex -= si1.BaseDex;
        BaseInt -= si1.BaseInt;
        BaseWis -= si1.BaseWis;
        BaseCrit -= si1.BaseCrit;
        BaseMagicCrit -= si1.BaseMagicCrit;
        BaseDmg -= si1.BaseDmg;
        BaseHit -= si1.BaseHit;
        BaseAc -= si1.BaseAc;
        BaseMr -= si1.BaseMr;
        BaseRegen -= si1.BaseRegen;
        BaseInboundHealModifier -= si1.BaseInboundHealModifier;
        BaseInboundDamageModifier -= si1.BaseInboundDamageModifier;
        BaseOutboundDamageModifier -= si1.BaseOutboundDamageModifier;
        BaseOutboundHealModifier -= si1.BaseOutboundHealModifier;
        BaseReflectMagical -= si1.BaseReflectMagical;
        BaseReflectPhysical -= si1.BaseReflectPhysical;
        BaseExtraGold -= si1.BaseExtraGold;
        BaseDodge -= si1.BaseDodge;
        BaseMagicDodge -= si1.BaseMagicDodge;
        BaseExtraXp -= si1.BaseExtraXp;
        BaseExtraItemFind -= si1.BaseExtraItemFind;
        BaseLifeSteal -= si1.BaseLifeSteal;
        BaseManaSteal -= si1.BaseManaSteal;
        BaseInboundDamageToMp -= si1.BaseInboundDamageToMp;
        BaseExtraFaith -= si1.BaseExtraFaith;
        Faith -= si1.Faith;
        ElementalModifiers -= si1.ElementalModifiers;

        if (!experience) return;
        Level -= si1.Level;
        Experience -= si1.Experience;
        Ability -= si1.Ability;
        AbilityExp -= si1.AbilityExp;
    }

    public bool NoBaseChanges => BaseHp == 0 && BaseMp == 0 && BaseStr == 0 && BaseCon == 0 && BaseDex == 0 &&
                                 BaseInt == 0 && BaseWis == 0 && BaseCrit == 0 && BaseMagicCrit == 0 && BaseDmg == 0 &&
                                 BaseHit == 0 && BaseAc == 0 && BaseMr == 0 && BaseRegen == 0 &&
                                 BaseInboundDamageModifier == 0 && BaseInboundHealModifier == 0 &&
                                 BaseOutboundDamageModifier == 0 && BaseOutboundHealModifier == 0 &&
                                 BaseReflectMagical == 0 && BaseReflectPhysical == 0 && BaseExtraGold == 0 &&
                                 BaseDodge == 0 && BaseMagicDodge == 0 && BaseExtraXp == 0 && BaseExtraItemFind == 0 &&
                                 BaseExtraFaith == 0 && BaseLifeSteal == 0 && BaseManaSteal == 0 && BaseInboundDamageToMp == 0 &&
                                 DeltaHp == 0 && DeltaMp == 0 && Faith == 0;

    public bool NoBonusChanges => BonusHp == 0 && BonusMp == 0 && BonusStr == 0 && BonusCon == 0 && BonusDex == 0 &&
                                  BonusInt == 0 && BonusWis == 0 && BonusCrit == 0 && BonusMagicCrit == 0 &&
                                  BonusDmg == 0 && BonusHit == 0 && BonusAc == 0 && BonusMr == 0 && BonusRegen == 0 &&
                                  BonusInboundDamageModifier == 0 && BonusInboundHealModifier == 0 &&
                                  BonusOutboundDamageModifier == 0 && BonusOutboundHealModifier == 0 &&
                                  BonusReflectMagical == 0 && BonusReflectPhysical == 0 && BonusExtraGold == 0 &&
                                  BonusDodge == 0 && BonusMagicDodge == 0 && BonusExtraXp == 0 &&
                                  BonusExtraItemFind == 0 && BonusExtraFaith == 0 && BonusLifeSteal == 0 && BonusManaSteal == 0 &&
                                  BonusInboundDamageToMp == 0;

    public bool NoExperienceChanges => Level == 0 && (Experience == 0) & (Ability == 0) && AbilityExp == 0;

    public bool NoResistanceChanges => _elementalModifiers.NoResistances;
    public bool NoAugmentChanges => _elementalModifiers.NoAugments;
    public bool NoElementalModifiers => NoResistanceChanges && NoAugmentChanges;

    public bool Empty => NoExperienceChanges && NoBonusChanges && NoBaseChanges && NoElementalModifiers;

    #endregion
}