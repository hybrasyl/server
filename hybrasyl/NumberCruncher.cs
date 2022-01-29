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

    public static StatInfo CalculateStatusModifiers(Castable castable, StatModifiers effect,
        Creature source, Creature target = null)
    {
        StatInfo modifiers = new StatInfo();
        modifiers.BonusHp = (long)_evalFormula(effect.Hp, castable, target, source);
        modifiers.BonusMp = (long)_evalFormula(effect.Mp, castable, target, source);
        modifiers.BonusStr = (long)_evalFormula(effect.Str, castable, target, source);
        modifiers.BonusCon = (long)_evalFormula(effect.Con, castable, target, source);
        modifiers.BonusDex = (long)_evalFormula(effect.Dex, castable, target, source);
        modifiers.BonusInt = (long)_evalFormula(effect.Int, castable, target, source);
        modifiers.BonusWis = (long)_evalFormula(effect.Wis, castable, target, source);
        modifiers.BonusCrit = _evalFormula(effect.Crit, castable, target, source);
        modifiers.BonusDmg = (long)_evalFormula(effect.Dmg, castable, target, source);
        modifiers.BonusHit = (long)_evalFormula(effect.Hit, castable, target, source);
        modifiers.BonusAc = (long)_evalFormula(effect.Ac, castable, target, source);
        modifiers.BonusMr = (long)_evalFormula(effect.Mr, castable, target, source);
        modifiers.BonusRegen = (long)_evalFormula(effect.Regen, castable, target, source);
        modifiers.BonusInboundDamageModifier = _evalFormula(effect.InboundDamageModifier, castable, target, source);
        modifiers.BonusInboundHealModifier = _evalFormula(effect.InboundHealModifier, castable, target, source);
        modifiers.BonusOutboundDamageModifier = _evalFormula(effect.OutboundDamageModifier, castable, target, source);
        modifiers.BonusOutboundHealModifier = _evalFormula(effect.OutboundHealModifier, castable, target, source);
        modifiers.BonusReflectMagical = _evalFormula(effect.ReflectMagical, castable, target, source);
        modifiers.BonusReflectPhysical = _evalFormula(effect.ReflectPhysical, castable, target, source);
        modifiers.BonusExtraGold = _evalFormula(effect.ExtraGold, castable, target, source);
        modifiers.BonusDodge = _evalFormula(effect.Dodge, castable, target, source);
        modifiers.BonusExtraXp = _evalFormula(effect.ExtraXp, castable, target, source);
        modifiers.BonusExtraItemFind = _evalFormula(effect.ExtraItemFind, castable, target, source);
        modifiers.BonusLifeSteal = _evalFormula(effect.LifeSteal, castable, target, source);
        modifiers.BonusManaSteal = _evalFormula(effect.ManaSteal, castable, target, source);

        if (effect.OffensiveElement != Xml.ElementType.None)
            modifiers.OffensiveElementOverride = effect.OffensiveElement;
        if (effect.DefensiveElement != Xml.ElementType.None)
            modifiers.DefensiveElementOverride = effect.DefensiveElement;

        return modifiers;

    }

}