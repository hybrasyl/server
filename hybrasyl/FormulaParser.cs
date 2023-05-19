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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using NCalc;
using Creature = Hybrasyl.Objects.Creature;

namespace Hybrasyl;

// ungodly hack until we can go back and refactor this for real
public class FormulaEvaluation
{
    public Creature Source { get; set; } = null;
    public Castable Castable { get; set; } = null;
    public Creature Target { get; set; } = null;
    public MapObject Map { get; set; } = null;
    public Monster Spawn { get; set; } = null;
    public User User { get; set; } = null;
    public double? Damage { get; set; } = null;
    public Spawn XmlSpawn { get; set; } = null;
    public SpawnGroup SpawnGroup { get; set; } = null;
    public ItemObject ItemObject { get; set; } = null;
}

internal static class FormulaParser
{
    // TODO: potentially use reflection to simplify this

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
        }

        foreach (var prop in FormulaTokens[typeof(Creature)])
        {
            if (eval.Source != null)
                e.Parameters[$"SOURCE{prop.Name.ToUpper()}"] = prop.GetValue(eval.Source);
            if (eval.Target != null)
                e.Parameters[$"TARGET{prop.Name.ToUpper()}"] = prop.GetValue(eval.Target);
        }

        foreach (var prop in FormulaTokens[typeof(Castable)])
            if (eval.Castable != null)
                e.Parameters[$"CASTABLE{prop.Name.ToUpper()}"] = prop.GetValue(eval.Castable);

        foreach (var prop in FormulaTokens[typeof(MapObject)])
            if (eval.Map != null)
                e.Parameters[$"MAP{prop.Name.ToUpper()}"] = prop.GetValue(eval.Map);

        foreach (var prop in FormulaTokens[typeof(ItemObject)])
            if (eval.ItemObject != null)
                e.Parameters[$"ITEM{prop.Name.ToUpper()}"] = prop.GetValue(eval.ItemObject);

        // Handle non-typebound variables, or static values
        e.Parameters["DAMAGE"] = eval.Damage ?? 0;
        e.Parameters["RAND_5"] = Random.Shared.Next(0, 6);
        e.Parameters["RAND_10"] = Random.Shared.Next(0, 11);
        e.Parameters["RAND_100"] = Random.Shared.Next(0, 101);
        e.Parameters["RAND_1000"] = Random.Shared.Next(0, 1001);

        return e;
    }

    public static double Eval(string expression, FormulaEvaluation evalEnvironment)
    {
        if (string.IsNullOrEmpty(expression)) return 0.0;
        Expression e = new(expression);
        e = Parameterize(e, evalEnvironment);
        try
        {
            //GameLog.Info($"Eval of {expression} : {ret} ");
            return Convert.ToDouble(e.Evaluate());
        }
        catch (Exception ex)
        {
            GameLog.SpawnError($"Eval error: expression {expression} - {ex}");
            return 0.0;
        }
    }
}