// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using NCalc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Creature = Hybrasyl.Objects.Creature;

namespace Hybrasyl.Subsystems.Formulas;

// ungodly hack until we can go back and refactor this for real

internal static class FormulaParser
{
    private static readonly Dictionary<Type, List<PropertyInfo>> FormulaTokens = new();

    static FormulaParser()
    {
        // TODO: DRY even further with type attributes but this is a significant improvement
        FormulaTokens[typeof(StatInfo)] = typeof(StatInfo).GetProperties()
            .Where(predicate: prop => prop.IsDefined(typeof(FormulaVariable), false)).ToList();
        FormulaTokens[typeof(Creature)] = typeof(Creature).GetProperties()
            .Where(predicate: prop => prop.IsDefined(typeof(FormulaVariable), false)).ToList();
        FormulaTokens[typeof(MapObject)] = typeof(MapObject).GetProperties()
            .Where(predicate: prop => prop.IsDefined(typeof(FormulaVariable), false)).ToList();
        FormulaTokens[typeof(Castable)] = typeof(Castable).GetProperties()
            .Where(predicate: prop => prop.IsDefined(typeof(FormulaVariable), false)).ToList();
        FormulaTokens[typeof(ItemObject)] = typeof(ItemObject).GetProperties()
            .Where(predicate: prop => prop.IsDefined(typeof(FormulaVariable), false)).ToList();
    }

    public static Expression Parameterize(Expression e, FormulaEvaluation eval)
    {
        foreach (var prop in FormulaTokens[typeof(StatInfo)])
        {
            if (eval.Source != null)
                e.Parameters[$"SOURCE{prop.Name.ToUpper()}"] = prop.GetValue(eval.Source.Stats);
            if (eval.Target != null)
                e.Parameters[$"TARGET{prop.Name.ToUpper()}"] = prop.GetValue(eval.Target.Stats);
            if (eval.OriginalCaster != null)
                e.Parameters[$"ORIGINALCASTER{prop.Name.ToUpper()}"] = prop.GetValue(eval.OriginalCaster);

        }

        foreach (var prop in FormulaTokens[typeof(Creature)])
        {
            if (eval.Source != null)
                e.Parameters[$"SOURCE{prop.Name.ToUpper()}"] = prop.GetValue(eval.Source);
            if (eval.Target != null)
                e.Parameters[$"TARGET{prop.Name.ToUpper()}"] = prop.GetValue(eval.Target);
            // OriginalCaster is a StatInfo, not a Creature — only the StatInfo loop applies to it.
        }

        foreach (var prop in FormulaTokens[typeof(Castable)].Where(prop => eval.Castable != null))
            e.Parameters[$"CASTABLE{prop.Name.ToUpper()}"] = prop.GetValue(eval.Castable);

        foreach (var prop in FormulaTokens[typeof(MapObject)].Where(prop => eval.Map != null))
            e.Parameters[$"MAP{prop.Name.ToUpper()}"] = prop.GetValue(eval.Map);

        foreach (var prop in FormulaTokens[typeof(ItemObject)].Where(prop => eval.ItemObject != null))
            e.Parameters[$"ITEM{prop.Name.ToUpper()}"] = prop.GetValue(eval.ItemObject);

        // Handle non-typebound variables, or static values
        e.Parameters["DAMAGE"] = eval.Damage ?? 0;
        e.Parameters["RAND_5"] = Random.Shared.Next(0, 6);
        e.Parameters["RAND_10"] = Random.Shared.Next(0, 11);
        e.Parameters["RAND_100"] = Random.Shared.Next(0, 101);
        e.Parameters["RAND_1000"] = Random.Shared.Next(0, 1001);
        e.Parameters["RANDDOUBLE"] = Random.Shared.NextDouble();

        return e;
    }

    // Guard rails for designer-authored formulas. OverflowProtection turns silently-wrapping
    // integer arithmetic (2000000000*2 was -294967296) and division by zero into exceptions
    // the catch below reports. RoundAwayFromZero replaces .NET's banker's rounding, under
    // which Round(2.5) is 2 -- rarely what a formula author means.
    private const ExpressionOptions EvalOptions =
        ExpressionOptions.OverflowProtection | ExpressionOptions.RoundAwayFromZero;

    public static double Eval(string expression, FormulaEvaluation evalEnvironment)
    {
        if (string.IsNullOrEmpty(expression)) return 0.0;
        Expression e = new(expression, EvalOptions);
        e = Parameterize(e, evalEnvironment);
        try
        {
            // Evaluate exactly once: RAND_* parameters are fixed at Parameterize time, but a
            // second Evaluate() is still wasted work on a per-tick/per-hit path.
            var result = Convert.ToDouble(e.Evaluate());

            // OverflowProtection covers x/0 but NOT 0/0 (NaN) or Pow overflow (infinity), and
            // every caller casts this double to a narrower unsigned type where .NET saturates
            // rather than erroring -- (uint)infinity is 4294967295, which would read as a
            // legitimate award of gold, xp or regen. Fail closed instead.
            if (!double.IsFinite(result))
            {
                GameLog.SpawnError("Eval error: expression {Expression} produced non-finite result {Result}",
                    expression, result);
                return 0.0;
            }

            return result;
        }
        catch (Exception ex)
        {
            GameLog.SpawnError(ex, "Eval error: expression {Expression}", expression);
            return 0.0;
        }
    }
}
