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
 
using Hybrasyl.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Xml;
using Creature = Hybrasyl.Objects.Creature;

namespace Hybrasyl;

// A simple class to hold damage output along with flags / element, which we use elsewhere (specifically statuses)
public class DamageOutput
{
    public double Amount { get; set; }
    public Xml.DamageType Type { get; set; }
    public Xml.DamageFlags Flags { get; set; }
    public Xml.ElementType Element { get; set; }
    public override string ToString() => $"{Element}, {Amount}: {Type} {Flags}";
}

public class CastCost
{
    public uint Hp { get; set; }
    public uint Mp { get; set; }
    public uint Gold { get; set; }
    public List<(byte Quantity, string Item)> Items { get; set; } = new();
    public bool IsNoCost => Hp == 0 && Mp == 0 && Gold == 0 && Items.Count == 0;
}

/// <summary>
/// This class is used to do a variety of numerical calculations, in order to consolidate those into
/// one place. Specifically, healing and damage, are handled here.
/// </summary>
///
static class NumberCruncher
{

    // This is dumb, but it's a consequence of how xsd2code works
    private static double _evalSimple(dynamic simple)
    {
        if (simple is not Xml.SimpleQuantity sq)
            throw new InvalidOperationException("Invalid type passed to _evalSimple");
        // Simple damage can either be expressed as a fixed value <Simple>50</Simple> or a min/max <Simple Min="50" Max="100"/>
        if (sq.Value != 0) return sq.Value;
        return Random.Shared.Next((int) sq.Min, (int) (sq.Max + 1));

    }

    private static double _evalFormula(string formula, ItemObject item, Creature source)
    {
        if (string.IsNullOrEmpty(formula)) return 0.0;

        try
        {
            return FormulaParser.Eval(formula,
                new FormulaEvaluation() { ItemObject = item, Source = source });
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error(
                $"NumberCruncher formula error: item {item.Name}, source {source?.Name ?? "no source"}: {formula}, error: {e}");
            return 0;
        }

    }
    private static double _evalFormula(string formula, Castable castable, Creature target, Creature source)
    {
        if (string.IsNullOrEmpty(formula)) return 0.0;

        try
        {
            return FormulaParser.Eval(formula,
                new FormulaEvaluation() {Castable = castable, Target = target, Source = source});
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error(
                $"NumberCruncher formula error: castable {castable.Name}, target {target.Name}, source {source?.Name ?? "no source"}: {formula}, error: {e}");
            return 0;
        }
    }

    /// <summary>
    /// Calculate the damage for a castable.
    /// </summary>
    /// <param name="castable">The castable to use for the calculation</param>
    /// <param name="target">The target of the castable (i.e. the spell/skill target)</param>
    /// <param name="source">The source of the castable (i.e. the caster)</param>
    /// <returns></returns>
    public static DamageOutput CalculateDamage(Castable castable, Creature target, Creature source = null)
    {
        // Defaults
        double dmg = 1;
        var type = castable.Effects?.Damage?.Type ?? DamageType.Magical;

        if (castable.Effects?.Damage == null)
            return new DamageOutput()
                {Amount = dmg, Type = type, Flags = DamageFlags.None, Element = castable.Element};

        if (castable.Effects.Damage.IsSimple)
        {
            var simple = castable.Effects.Damage.Simple;
            dmg = _evalSimple(simple);
        }
        else
        {
            var formula = castable.Effects.Damage.Formula;
            dmg = _evalFormula(formula, castable, target, source);
        }

        return new DamageOutput
        {
            Amount = dmg * target.Stats.InboundDamageModifier * source?.Stats?.OutboundHealModifier ?? 1.0,
            Type = type,
            Flags = castable.Effects.Damage.Flags,
            Element = castable.Element
        };
    }

    /// <summary>
    /// Calculate the healing for a castable.
    /// </summary>
    /// <param name="castable">The castable to use for the calculation</param>
    /// <param name="target">The target of the castable (i.e. the spell/skill target)</param>
    /// <param name="source">The source of the castable (i.e. the caster), optional parameter</param>
    /// <returns></returns>

    public static double CalculateHeal(Castable castable, Creature target, Creature source = null)
    {
        double heal = 0;
        if (castable.Effects?.Heal == null) return heal;

        heal = castable.Effects.Heal.IsSimple
            ? _evalSimple(castable.Effects.Heal.Simple) * target.Stats.InboundHealModifier
            : _evalFormula(castable.Effects.Heal.Formula, castable, target, source);
        return heal * target.Stats.InboundHealModifier * source?.Stats?.OutboundHealModifier ?? 1.0;
    }


    /// <summary>
    /// Calculate the damage for a status tick.
    /// </summary>
    /// <param name="castable">Castable responsible for the status</param>
    /// <param name="effect">ModifierEffect structure for the status</param>
    /// <param name="target">Target for the damage (e.g. the player or creature with the status)</param>
    /// <param name="source">Original source of the status</param>
    /// <param name="statusName">The name of the status</param>
    /// <returns></returns>
    public static DamageOutput CalculateDamage(Castable castable, ModifierEffect effect, Creature target,
        Creature source, string statusName)
    {
        // Defaults
        double dmg = 0;
        var type = effect.Damage?.Type ?? DamageType.Magical;

        if (effect.Damage == null)
            return new DamageOutput
                {Amount = dmg, Type = type, Flags = DamageFlags.None, Element = castable.Element};

        var statusAdd = castable?.Effects?.Statuses?.Add?.Where(e => e.Value == statusName)?.ToList();
        var intensity = statusAdd != null ? statusAdd[0].Intensity : 1;

        dmg = effect.Damage.IsSimple
            ? _evalSimple(effect.Damage.Simple)
            : _evalFormula(effect.Damage.Formula, castable, target, source);

        return new DamageOutput
        {
            Amount = (dmg * intensity * target.Stats.InboundDamageModifier * source.Stats.OutboundDamageModifier),
            Type = type,
            Flags = effect.Damage.Flags,
            Element = castable?.Element ?? ElementType.None
        };
    }

    /// <summary>
    /// Calculate the healing for a status tick.
    /// </summary>
    /// <param name="castable">Castable responsible for the status</param>
    /// <param name="effect">ModifierEffect structure for the status</param>
    /// <param name="target">Target for the healing (e.g. the player or creature with the status)</param>
    /// <param name="source">Original source of the status</param>
    /// <param name="statusName">The name of the status</param>
    /// <returns></returns>
    public static double CalculateHeal(Castable castable, ModifierEffect effect, Creature target,
        Creature source, string statusName)
    {
        // Defaults
        double heal = 0;

        if (effect?.Heal == null) return heal;

        var statusAdd = castable?.Effects?.Statuses?.Add?.Where(e => e.Value == statusName)?.ToList();
        var intensity = statusAdd != null ? statusAdd[0].Intensity : 1;

        heal = effect.Heal.IsSimple ? _evalSimple(effect.Heal.Simple) : _evalFormula(effect.Heal.Formula, castable, target, source);

        return heal * intensity * source.Stats.OutboundHealModifier * target.Stats.InboundHealModifier;

    }

    /// <summary>
    /// Calculate the cast cost for a castable, which can also be a formula.
    /// </summary>
    /// <param name="castable">The castable being cast</param>
    /// <param name="target">The target, if applicable, for the castable</param>
    /// <param name="source">The source (caster) for the castable</param>
    /// <returns></returns>
    public static CastCost CalculateCastCost(Castable castable, Creature target, Creature source)
    {
        var cost = new CastCost();

        if (castable.CastCosts.Count == 0 || !(source is User user)) return cost;

        var costs = castable.CastCosts.Where(e => e.Class.Contains(user.Class));

        if (costs.Count() == 0)
            costs = castable.CastCosts.Where(e => e.Class.Count == 0);

        if (costs.Count() == 0)
            return cost;

        var toEvaluate = costs.First();

        if (toEvaluate.Stat?.Hp != null)
            cost.Hp = (uint) _evalFormula(toEvaluate.Stat.Hp, castable, target, source);

        if (toEvaluate.Stat?.Mp != null)
            cost.Mp = (uint) _evalFormula(toEvaluate.Stat.Mp, castable, target, source);

        if (toEvaluate.Gold > 0)
            cost.Gold = toEvaluate.Gold;

        if (toEvaluate.Items.Count > 0)
            cost.Items = toEvaluate.Items.Select(x => (x.Quantity, x.Value)).ToList();

        return cost;
    }

    public static StatInfo CalculateItemModifiers(ItemObject item, Creature source, StatModifiers effect=null)
    {
        if (effect == null)
            effect = item.Template.Properties.StatModifiers;
        if (effect == null)
            return new StatInfo();

        var modifiers = new StatInfo
        {
            DeltaHp = (long)_evalFormula(effect.CurrentHp, item, source),
            DeltaMp = (long)_evalFormula(effect.CurrentMp, item, source),
            BaseHp = (long)_evalFormula(effect.BaseHp, item, source),
            BaseMp = (long)_evalFormula(effect.BaseMp, item, source),
            BaseStr = (long)_evalFormula(effect.BaseStr, item, source),
            BaseCon = (long)_evalFormula(effect.BaseCon, item, source),
            BaseDex = (long)_evalFormula(effect.BaseDex, item, source),
            BaseInt = (long)_evalFormula(effect.BaseInt, item, source),
            BaseWis = (long)_evalFormula(effect.BaseWis, item, source),
            BaseCrit = _evalFormula(effect.BaseCrit, item, source),
            BaseMagicCrit = _evalFormula(effect.BaseMagicCrit, item, source),
            BaseDodge = _evalFormula(effect.BaseDodge, item, source),
            BaseMagicDodge = _evalFormula(effect.BaseMagicDodge, item, source),
            BaseDmg = (long)_evalFormula(effect.BaseDmg, item, source),
            BaseHit = (long)_evalFormula(effect.BaseHit, item, source),
            BaseAc = (long)_evalFormula(effect.BaseAc, item, source),
            BaseMr = (long)_evalFormula(effect.BaseMr, item, source),
            BaseRegen = (long)_evalFormula(effect.BaseRegen, item, source),
            BaseInboundDamageModifier = _evalFormula(effect.BaseInboundDamageModifier, item, source),
            BaseInboundHealModifier = _evalFormula(effect.BaseInboundHealModifier, item, source),
            BaseOutboundDamageModifier = _evalFormula(effect.BaseOutboundDamageModifier, item, source),
            BaseOutboundHealModifier = _evalFormula(effect.BaseOutboundHealModifier, item, source),
            BaseReflectMagical = _evalFormula(effect.BaseReflectMagical, item, source),
            BaseReflectPhysical = _evalFormula(effect.BaseReflectPhysical, item, source),
            BaseExtraGold = _evalFormula(effect.BaseExtraGold, item, source),
            BaseExtraXp = _evalFormula(effect.BaseExtraXp, item, source),
            BaseExtraItemFind = _evalFormula(effect.BaseExtraItemFind, item, source),
            BaseLifeSteal = _evalFormula(effect.BaseLifeSteal, item, source),
            BaseManaSteal = _evalFormula(effect.BaseManaSteal, item, source),
            BaseInboundDamageToMp = _evalFormula(effect.BaseInboundDamageToMp, item, source),
            BonusHp = (long)_evalFormula(effect.BonusHp, item, source),
            BonusMp = (long)_evalFormula(effect.BonusMp, item, source),
            BonusStr = (long)_evalFormula(effect.BonusStr, item, source),
            BonusCon = (long)_evalFormula(effect.BonusCon, item, source),
            BonusDex = (long)_evalFormula(effect.BonusDex, item, source),
            BonusInt = (long)_evalFormula(effect.BonusInt, item, source),
            BonusWis = (long)_evalFormula(effect.BonusWis, item, source),
            BonusCrit = _evalFormula(effect.BonusCrit, item, source),
            BonusMagicCrit = _evalFormula(effect.BonusMagicCrit, item, source),
            BonusDodge = _evalFormula(effect.BonusDodge, item, source),
            BonusMagicDodge = _evalFormula(effect.BonusMagicDodge, item, source),
            BonusDmg = (long)_evalFormula(effect.BonusDmg, item, source),
            BonusHit = (long)_evalFormula(effect.BonusHit, item, source),
            BonusAc = (long)_evalFormula(effect.BonusAc, item, source),
            BonusMr = (long)_evalFormula(effect.BonusMr, item, source),
            BonusRegen = (long)_evalFormula(effect.BonusRegen, item, source),
            BonusInboundDamageModifier = _evalFormula(effect.BonusInboundDamageModifier, item, source),
            BonusInboundHealModifier = _evalFormula(effect.BonusInboundHealModifier, item, source),
            BonusOutboundDamageModifier = _evalFormula(effect.BonusOutboundDamageModifier, item, source),
            BonusOutboundHealModifier = _evalFormula(effect.BonusOutboundHealModifier, item, source),
            BonusReflectMagical = _evalFormula(effect.BonusReflectMagical, item, source),
            BonusReflectPhysical = _evalFormula(effect.BonusReflectPhysical, item, source),
            BonusExtraGold = _evalFormula(effect.BonusExtraGold, item, source),
            BonusExtraXp = _evalFormula(effect.BonusExtraXp, item, source),
            BonusExtraItemFind = _evalFormula(effect.BonusExtraItemFind, item, source),
            BonusLifeSteal = _evalFormula(effect.BonusLifeSteal, item, source),
            BonusManaSteal = _evalFormula(effect.BonusManaSteal, item, source),
            BonusInboundDamageToMp = _evalFormula(effect.BonusInboundDamageToMp, item, source)
        };

        if (effect.BaseOffensiveElement != Xml.ElementType.None)
            modifiers.OffensiveElementOverride = effect.BaseOffensiveElement;
        if (effect.BaseDefensiveElement != Xml.ElementType.None)
            modifiers.DefensiveElementOverride = effect.BaseDefensiveElement;

        return modifiers;

    }

    public static long Modify(double val, double intensity)
    {
        if (intensity == 0) return (long) val;
        return (long) (val * intensity);
    }

    public static StatInfo CalculateStatusModifiers(Castable castable, double intensity, StatModifiers effect,
        Creature source, Creature target=null)
    {
        var modifiers = new StatInfo
        {
            DeltaHp = Modify(_evalFormula(effect.CurrentHp, castable, target, source), intensity),
            DeltaMp = (long)_evalFormula(effect.CurrentMp, castable, target, source),
            BonusHp = Modify(_evalFormula(effect.BonusHp, castable, target, source), intensity),
            BonusMp = Modify(_evalFormula(effect.BonusMp, castable, target, source), intensity),
            BonusStr = Modify(_evalFormula(effect.BonusStr, castable, target, source), intensity),
            BonusInt = Modify(_evalFormula(effect.BonusInt, castable, target, source), intensity),
            BonusWis = Modify(_evalFormula(effect.BonusWis, castable, target, source), intensity),
            BonusCon = Modify(_evalFormula(effect.BonusCon, castable, target, source), intensity),
            BonusDex = Modify(_evalFormula(effect.BonusDex, castable, target, source), intensity),
            BonusCrit = Modify(_evalFormula(effect.BonusCrit, castable, target, source), intensity),
            BonusMagicCrit = Modify(_evalFormula(effect.BonusMagicCrit, castable, target, source), intensity),
            BonusDmg = Modify(_evalFormula(effect.BonusDmg, castable, target, source), intensity),
            BonusHit = Modify(_evalFormula(effect.BonusHit, castable, target, source), intensity),
            BonusAc = Modify(_evalFormula(effect.BonusAc, castable, target, source), intensity),
            BonusMr = Modify(_evalFormula(effect.BonusMr, castable, target, source), intensity),
            BonusRegen = Modify(_evalFormula(effect.BonusRegen, castable, target, source), intensity),
            BonusInboundDamageModifier = Modify(_evalFormula(effect.BonusInboundDamageModifier, castable, target, source), intensity),
            BonusInboundHealModifier = Modify(_evalFormula(effect.BonusInboundHealModifier, castable, target, source), intensity),
            BonusOutboundDamageModifier = Modify(_evalFormula(effect.BonusOutboundDamageModifier, castable, target, source), intensity),
            BonusOutboundHealModifier = Modify(_evalFormula(effect.BonusOutboundHealModifier, castable, target, source), intensity),
            BonusReflectMagical = Modify(_evalFormula(effect.BonusReflectMagical, castable, target, source), intensity),
            BonusReflectPhysical = Modify(_evalFormula(effect.BonusReflectPhysical, castable, target, source), intensity),
            BonusExtraGold = Modify(_evalFormula(effect.BonusExtraGold, castable, target, source), intensity),
            BonusDodge = Modify(_evalFormula(effect.BonusDodge, castable, target, source), intensity),
            BonusMagicDodge = Modify(_evalFormula(effect.BonusMagicDodge, castable, target, source), intensity),
            BonusExtraXp = Modify(_evalFormula(effect.BonusExtraXp, castable, target, source), intensity),
            BonusExtraItemFind = Modify(_evalFormula(effect.BonusExtraItemFind, castable, target, source), intensity),
            BonusLifeSteal = Modify(_evalFormula(effect.BonusLifeSteal, castable, target, source), intensity),
            BonusManaSteal = Modify(_evalFormula(effect.BonusManaSteal, castable, target, source), intensity),
        };

        if (effect.BaseOffensiveElement != Xml.ElementType.None)
            modifiers.OffensiveElementOverride = effect.BaseOffensiveElement;
        if (effect.BaseDefensiveElement != Xml.ElementType.None)
            modifiers.DefensiveElementOverride = effect.BaseDefensiveElement;

        return modifiers;

    }

}