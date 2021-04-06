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
using System.Text;
using Hybrasyl.Objects;

namespace Hybrasyl
{

    // ungodly hack until we can go back and refactor this for real
    public class FormulaEvaluation
    {
        public Creature Caster { get; set; } = null;
        public Xml.Castable Castable { get; set; } = null;
        public Creature Target { get; set; } = null;
        public Map Map { get; set; } = null;
        public Monster Spawn { get; set; } = null;
        public User User { get; set; } = null;
        public double? Damage { get; set; } = null;
        public Xml.Spawn XmlSpawn { get; set; } = null;
        public Xml.SpawnGroup SpawnGroup { get; set; } = null;
    }

    // TODO: overhaul all of this
    internal static class FormulaParser
    {
        private static Random _rnd = new Random();

        private static string[] _operators = { "-", "+", "/", "*", "^" };
        private static Func<double, double, double>[] _operations = {
        (a1, a2) => a1 - a2,
        (a1, a2) => a1 + a2,
        (a1, a2) => a1 / a2,
        (a1, a2) => a1 * a2,
        (a1, a2) => Math.Pow(a1, a2)
        };

        public static double Eval(string expression, FormulaEvaluation eval)
        {
            var tokens = GetTokens(expression);
            var operandStack = new Stack<double>();
            var operatorStack = new Stack<string>();
            var tokenIndex = 0;

            MapTokens(ref tokens, eval);

            while (tokenIndex < tokens.Count)
            {
                var token = tokens[tokenIndex];
                if (token == "(")
                {
                    var subExpr = GetSubExpression(tokens, ref tokenIndex);
                    operandStack.Push(Eval(subExpr, eval));
                    continue;
                }
                if (token == ")")
                {
                    throw new ArgumentException("Mismatched parentheses in expression");
                }
                //If this is an operator  
                if (Array.IndexOf(_operators, token) >= 0)
                {
                    while (operatorStack.Count > 0 && Array.IndexOf(_operators, token) < Array.IndexOf(_operators, operatorStack.Peek()))
                    {
                        var op = operatorStack.Pop();
                        var arg2 = operandStack.Pop();
                        var arg1 = operandStack.Pop();
                        operandStack.Push(_operations[Array.IndexOf(_operators, op)](arg1, arg2));
                    }
                    operatorStack.Push(token);
                }
                else {
                    operandStack.Push(double.Parse(token));
                }
                tokenIndex += 1;
            }

            while (operatorStack.Count > 0)
            {
                var op = operatorStack.Pop();
                var arg2 = operandStack.Pop();
                var arg1 = operandStack.Pop();
                operandStack.Push(_operations[Array.IndexOf(_operators, op)](arg1, arg2));
            }
            return Math.Round(operandStack.Pop(), 0); //probably a better way to do this, however this is what we come up with for now.
        }

        public static List<string> GetTokens(string expression)
        {
            var operators = "()^*/+-";
            var tokens = new List<string>();
            var sb = new StringBuilder();

            foreach (var c in expression.Replace(" ", string.Empty))
            {
                if (operators.IndexOf(c) >= 0)
                {
                    if ((sb.Length > 0))
                    {
                        tokens.Add(sb.ToString());
                        sb.Length = 0;
                    }
                    tokens.Add(c.ToString());
                }
                else {
                    sb.Append(c);
                }
            }

            if ((sb.Length > 0))
            {
                tokens.Add(sb.ToString());
            }
            return tokens;
        }

        public static string GetSubExpression(List<string> tokens, ref int index)
        {
            var subExpr = new StringBuilder();
            var parenlevels = 1;
            index += 1;
            while (index < tokens.Count && parenlevels > 0)
            {
                var token = tokens[index];
                if (tokens[index] == "(")
                {
                    parenlevels += 1;
                }

                if (tokens[index] == ")")
                {
                    parenlevels -= 1;
                }

                if (parenlevels > 0)
                {
                    subExpr.Append(token);
                }

                index += 1;
            }

            if ((parenlevels > 0))
            {
                throw new ArgumentException("Mismatched parentheses in expression");
            }
            return subExpr.ToString();
        }

        public static bool MapTokens(ref List<string> tokens, FormulaEvaluation eval)
        {
            for (var i = 0; i < tokens.Count; i++)
            {
                var s = tokens[i];
                if (s.StartsWith("$"))
                {
                    switch (s)
                    {
                        case "$CASTERSTR":
                            tokens[i] = eval.Caster.Stats.Str.ToString();
                            break;
                        case "$CASTERINT":
                            tokens[i] = eval.Caster.Stats.Int.ToString();
                            break;
                        case "$CASTERWIS":
                            tokens[i] = eval.Caster.Stats.Wis.ToString();
                            break;
                        case "$CASTERCON":
                            tokens[i] = eval.Caster.Stats.Con.ToString();
                            break;
                        case "$CASTERDEX":
                            tokens[i] = eval.Caster.Stats.Dex.ToString();
                            break;
                        case "$CASTERDMG":
                            tokens[i] = eval.Caster.Stats.Dmg.ToString();
                            break;
                        case "$CASTERHIT":
                            tokens[i] = eval.Caster.Stats.Hit.ToString();
                            break;
                        case "$CASTERMR":
                            tokens[i] = eval.Caster.Stats.Mr.ToString();
                            break;
                        case "$CASTERAC":
                            tokens[i] = eval.Caster.Stats.Ac.ToString();
                            break;
                        case "$CASTERHP":
                            tokens[i] = eval.Caster.Stats.Hp.ToString();
                            break;
                        case "$CASTERMP":
                            tokens[i] = eval.Caster.Stats.Mp.ToString();
                            break;
                        case "$CASTERLEVEL":
                            tokens[i] = eval.Caster.Stats.Level.ToString();
                            break;
                        case "$CASTERAB":
                            tokens[i] = eval.Caster.Stats.Ability.ToString();
                            break;
                        case "$CASTERGOLD":
                            tokens[i] = eval.Caster.Gold.ToString();
                            break;
                        case "$CASTERBASESTR":
                            tokens[i] = eval.Caster.Stats.BaseStr.ToString();
                            break;
                        case "$CASTERBASEINT":
                            tokens[i] = eval.Caster.Stats.BaseInt.ToString();
                            break;
                        case "$CASTERBASEWIS":
                            tokens[i] = eval.Caster.Stats.BaseWis.ToString();
                            break;
                        case "$CASTERBASECON":
                            tokens[i] = eval.Caster.Stats.BaseCon.ToString();
                            break;
                        case "$CASTERBASEDEX":
                            tokens[i] = eval.Caster.Stats.BaseDex.ToString();
                            break;
                        case "$CASTERBASEHP":
                            tokens[i] = eval.Caster.Stats.BaseHp.ToString();
                            break;
                        case "$CASTERBASEMP":
                            tokens[i] = eval.Caster.Stats.BaseMp.ToString();
                            break;
                        case "$CASTERBONUSSTR":
                            tokens[i] = eval.Caster.Stats.BonusStr.ToString();
                            break;
                        case "$CASTERBONUSINT":
                            tokens[i] = eval.Caster.Stats.BonusInt.ToString();
                            break;
                        case "$CASTERBONUSWIS":
                            tokens[i] = eval.Caster.Stats.BonusWis.ToString();
                            break;
                        case "$CASTERBONUSCON":
                            tokens[i] = eval.Caster.Stats.BonusCon.ToString();
                            break;
                        case "$CASTERBONUSDEX":
                            tokens[i] = eval.Caster.Stats.BonusDex.ToString();
                            break;
                        case "$CASTERBONUSHP":
                            tokens[i] = eval.Caster.Stats.BonusHp.ToString();
                            break;
                        case "$CASTERBONUSMP":
                            tokens[i] = eval.Caster.Stats.BonusMp.ToString();
                            break;
                        case "$CASTERBONUSDMG":
                            tokens[i] = eval.Caster.Stats.BonusDmg.ToString();
                            break;
                        case "$CASTERBONUSHIT":
                            tokens[i] = eval.Caster.Stats.BonusHit.ToString();
                            break;
                        case "$CASTERBONUSMR":
                            tokens[i] = eval.Caster.Stats.BonusMr.ToString();
                            break;
                        case "$CASTERBONUSAC":
                            tokens[i] = eval.Caster.Stats.BonusAc.ToString();
                            break;
                        case "$CASTERWEAPONDMG":
                            {
                                var rand = new Random();
                                if (eval.Caster.Equipment.Weapon == null)
                                    tokens[i] = "0";
                                else
                                {
                                    var mindmg = (int)eval.Caster.Equipment.Weapon.MinSDamage;
                                    var maxdmg = (int)eval.Caster.Equipment.Weapon.MaxSDamage;
                                    if (mindmg == 0) mindmg = 1;
                                    if (maxdmg == 0) maxdmg = 1;
                                    tokens[i] = rand.Next(mindmg, maxdmg + 1).ToString();
                                }
                            }
                            break;
                        case "$CASTERWEAPONSDMG":
                            {
                                var rand = new Random();
                                if (eval.Caster.Equipment.Weapon == null)
                                    tokens[i] = "0";
                                else
                                {
                                    var mindmg = (int)eval.Caster.Equipment.Weapon.MinSDamage;
                                    var maxdmg = (int)eval.Caster.Equipment.Weapon.MaxSDamage;
                                    if (mindmg == 0) mindmg = 1;
                                    if (maxdmg == 0) maxdmg = 1;
                                    tokens[i] = rand.Next(mindmg, maxdmg + 1).ToString();
                                }
                            }
                            break;
                        case "$CASTERWEAPONSDMGMIN":
                            {
                                tokens[i] = (eval.Caster.Equipment.Weapon?.MinSDamage ?? '1').ToString();
                            }
                            break;
                        case "$CASTERWEAPONSDMGMAX":
                            {
                                tokens[i] = (eval.Caster.Equipment.Weapon?.MaxSDamage ?? '1').ToString();
                            }
                            break;
                        case "$CASTERWEAPONLDMG":
                            {
                                var rand = new Random();
                                if (eval.Caster.Equipment.Weapon == null)
                                    tokens[i] = "0";
                                else
                                {
                                    var mindmg = (int)eval.Caster.Equipment.Weapon.MinLDamage;
                                    var maxdmg = (int)eval.Caster.Equipment.Weapon.MaxLDamage;
                                    if (mindmg == 0) mindmg = 1;
                                    if (maxdmg == 0) maxdmg = 1;
                                    tokens[i] = rand.Next(mindmg, maxdmg + 1).ToString();
                                }
                            }
                            break;
                        case "$CASTERWEAPONLDMGMIN":
                            {
                                tokens[i] = (eval.Caster.Equipment.Weapon?.MinLDamage ?? '1').ToString();
                            }
                            break;
                        case "$CASTERWEAPONLDMGMAX":
                            {
                                tokens[i] = (eval.Caster.Equipment.Weapon?.MaxLDamage ?? '1').ToString();
                            }
                            break;
                        case "$CASTABLELEVEL":
                            {
                                //this is temporary until castable.currentlevel is implemented.
                                tokens[i] = "1"; 
                                //if (_castable.Effects.CastableLevel == 0) tokens[i] = "1";
                                //tokens[i] = eval.Caster.CastableLevel.ToString();
                            }
                            break;
                        case "$TARGETSTR":
                            tokens[i] = eval.Target.Stats.Str.ToString();
                            break;
                        case "$TARGETINT":
                            tokens[i] = eval.Target.Stats.Int.ToString();
                            break;
                        case "$TARGETWIS":
                            tokens[i] = eval.Target.Stats.Wis.ToString();
                            break;
                        case "$TARGETCON":
                            tokens[i] = eval.Target.Stats.Con.ToString();
                            break;
                        case "$TARGETDEX":
                            tokens[i] = eval.Target.Stats.Dex.ToString();
                            break;
                        case "$TARGETDMG":
                            tokens[i] = eval.Target.Stats.Dmg.ToString();
                            break;
                        case "$TARGETHIT":
                            tokens[i] = eval.Target.Stats.Hit.ToString();
                            break;
                        case "$TARGETMR":
                            tokens[i] = eval.Target.Stats.Mr.ToString();
                            break;
                        case "$TARGETAC":
                            tokens[i] = eval.Target.Stats.Ac.ToString();
                            break;
                        case "$TARGETHP":
                            tokens[i] = eval.Target.Stats.Hp.ToString();
                            break;
                        case "$TARGETMP":
                            tokens[i] = eval.Target.Stats.Mp.ToString();
                            break;
                        case "$TARGETLEVEL":
                            tokens[i] = eval.Target.Stats.Level.ToString();
                            break;
                        case "$TARGETAB":
                            tokens[i] = eval.Target.Stats.Ability.ToString();
                            break;
                        case "$TARGETGOLD":
                            tokens[i] = eval.Target.Gold.ToString();
                            break;
                        case "$TARGETBASESTR":
                            tokens[i] = eval.Target.Stats.BaseStr.ToString();
                            break;
                        case "$TARGETBASEINT":
                            tokens[i] = eval.Target.Stats.BaseInt.ToString();
                            break;
                        case "$TARGETBASEWIS":
                            tokens[i] = eval.Target.Stats.BaseWis.ToString();
                            break;
                        case "$TARGETBASECON":
                            tokens[i] = eval.Target.Stats.BaseCon.ToString();
                            break;
                        case "$TARGETBASEDEX":
                            tokens[i] = eval.Target.Stats.BaseDex.ToString();
                            break;
                        case "$TARGETBASEHP":
                            tokens[i] = eval.Target.Stats.BaseHp.ToString();
                            break;
                        case "$TARGETBASEMP":
                            tokens[i] = eval.Target.Stats.BaseMp.ToString();
                            break;
                        case "$TARGETBONUSSTR":
                            tokens[i] = eval.Target.Stats.BonusStr.ToString();
                            break;
                        case "$TARGETBONUSINT":
                            tokens[i] = eval.Target.Stats.BonusInt.ToString();
                            break;
                        case "$TARGETBONUSWIS":
                            tokens[i] = eval.Target.Stats.BonusWis.ToString();
                            break;
                        case "$TARGETBONUSCON":
                            tokens[i] = eval.Target.Stats.BonusCon.ToString();
                            break;
                        case "$TARGETBONUSDEX":
                            tokens[i] = eval.Target.Stats.BonusDex.ToString();
                            break;
                        case "$TARGETBONUSHP":
                            tokens[i] = eval.Target.Stats.BonusHp.ToString();
                            break;
                        case "$TARGETBONUSMP":
                            tokens[i] = eval.Target.Stats.BonusMp.ToString();
                            break;
                        case "$TARGETBONUSDMG":
                            tokens[i] = eval.Target.Stats.BonusDmg.ToString();
                            break;
                        case "$TARGETBONUSHIT":
                            tokens[i] = eval.Target.Stats.BonusHit.ToString();
                            break;
                        case "$TARGETBONUSMR":
                            tokens[i] = eval.Target.Stats.BonusMr.ToString();
                            break;
                        case "$TARGETBONUSAC":
                            tokens[i] = eval.Target.Stats.BonusAc.ToString();
                            break;
                        case "$MAPBASELEVEL":
                            tokens[i] = (eval.Map.SpawnDirectives?.BaseLevel ?? "1").ToString();
                            break;
                        case "$MAPTILES":
                            tokens[i] = (eval.Map.X * eval.Map.Y).ToString();
                            break;
                        case "$MAPX":
                            tokens[i] = eval.Map.X.ToString();
                            break;
                        case "$MAPY":
                            tokens[i] = eval.Map.Y.ToString();
                            break;
                        case "$DAMAGE":
                            tokens[i] = eval.Damage?.ToString() ?? "0";
                            break;
                        case "$USERCRIT":
                            tokens[i] = eval.User.Stats.BaseCrit.ToString() ?? "0";
                            break;
                        case "$USERHIT":
                            tokens[i] = eval.User.Stats.Hit.ToString() ?? "0";
                            break;
                        case "$USERMR":
                            tokens[i] = eval.User.Stats.Mr.ToString() ?? "0";
                            break;
                        case "$USERHP":
                            tokens[i] = eval.User.Stats.Hp.ToString() ?? "0";
                            break;
                        case "$USERMP":
                            tokens[i] = eval.User.Stats.Hp.ToString() ?? "0";
                            break;
                        case "$SPAWNHP":
                            tokens[i] = eval.Spawn.Stats.Hp.ToString() ?? "0";
                            break;
                        case "$SPAWNMP":
                            tokens[i] = eval.Spawn.Stats.Mp.ToString() ?? "0";
                            break;
                        case "$SPAWNXP":
                            tokens[i] = eval.Spawn.LootableXP.ToString() ?? "0";
                            break;
                        case "$RAND_5":
                            tokens[i] = _rnd.Next(0, 5).ToString();
                            break;
                        case "$RAND_10":
                            tokens[i] = _rnd.Next(0, 11).ToString();
                            break;
                        case "$RAND_100":
                            tokens[i] = _rnd.Next(0, 101).ToString();
                            break;
                        case "$RAND_1000":
                            tokens[i] = _rnd.Next(0, 1001).ToString();
                            break;
                        default:
                            tokens[i] = "0";
                            return false;
                            //handles an undefined token.
                    }
                }
            }
            return true;
        }
    }
}
