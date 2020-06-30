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

    internal class FormulaParser
    {
        private Creature _caster;
        private Xml.Castable _castable;
        private Creature _target;

        public FormulaParser(Creature caster, Xml.Castable castable, Creature target = null)
        {
            _caster = caster;
            _castable = castable;
            _target = target;
        }

        private string[] _operators = { "-", "+", "/", "*", "^" };
        private Func<double, double, double>[] _operations = {
        (a1, a2) => a1 - a2,
        (a1, a2) => a1 + a2,
        (a1, a2) => a1 / a2,
        (a1, a2) => a1 * a2,
        (a1, a2) => Math.Pow(a1, a2)
        };

        public double Eval(string expression)
        {
            var tokens = GetTokens(expression);
            var operandStack = new Stack<double>();
            var operatorStack = new Stack<string>();
            var tokenIndex = 0;

            MapTokens(ref tokens);

            while (tokenIndex < tokens.Count)
            {
                var token = tokens[tokenIndex];
                if (token == "(")
                {
                    var subExpr = GetSubExpression(tokens, ref tokenIndex);
                    operandStack.Push(Eval(subExpr));
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



        public List<string> GetTokens(string expression)
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

        public string GetSubExpression(List<string> tokens, ref int index)
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

        public bool MapTokens(ref List<string> tokens)
        {
            for (var i = 0; i < tokens.Count; i++)
            {
                var s = tokens[i];
                if (s.StartsWith("$"))
                {
                    switch (s)
                    {
                        case "$CASTERSTR":
                            tokens[i] = _caster.Stats.Str.ToString();
                            break;
                        case "$CASTERINT":
                            tokens[i] = _caster.Stats.Int.ToString();
                            break;
                        case "$CASTERWIS":
                            tokens[i] = _caster.Stats.Wis.ToString();
                            break;
                        case "$CASTERCON":
                            tokens[i] = _caster.Stats.Con.ToString();
                            break;
                        case "$CASTERDEX":
                            tokens[i] = _caster.Stats.Dex.ToString();
                            break;
                        case "$CASTERDMG":
                            tokens[i] = _caster.Stats.Dmg.ToString();
                            break;
                        case "$CASTERHIT":
                            tokens[i] = _caster.Stats.Hit.ToString();
                            break;
                        case "$CASTERMR":
                            tokens[i] = _caster.Stats.Mr.ToString();
                            break;
                        case "$CASTERAC":
                            tokens[i] = _caster.Stats.Ac.ToString();
                            break;
                        case "$CASTERHP":
                            tokens[i] = _caster.Stats.Hp.ToString();
                            break;
                        case "$CASTERMP":
                            tokens[i] = _caster.Stats.Mp.ToString();
                            break;
                        case "$CASTERLEVEL":
                            tokens[i] = _caster.Stats.Level.ToString();
                            break;
                        case "$CASTERAB":
                            tokens[i] = _caster.Stats.Ability.ToString();
                            break;
                        case "$CASTERGOLD":
                            tokens[i] = _caster.Gold.ToString();
                            break;
                        case "$CASTERBASESTR":
                            tokens[i] = _caster.Stats.BaseStr.ToString();
                            break;
                        case "$CASTERBASEINT":
                            tokens[i] = _caster.Stats.BaseInt.ToString();
                            break;
                        case "$CASTERBASEWIS":
                            tokens[i] = _caster.Stats.BaseWis.ToString();
                            break;
                        case "$CASTERBASECON":
                            tokens[i] = _caster.Stats.BaseCon.ToString();
                            break;
                        case "$CASTERBASEDEX":
                            tokens[i] = _caster.Stats.BaseDex.ToString();
                            break;
                        case "$CASTERBASEHP":
                            tokens[i] = _caster.Stats.BaseHp.ToString();
                            break;
                        case "$CASTERBASEMP":
                            tokens[i] = _caster.Stats.BaseMp.ToString();
                            break;
                        case "$CASTERBONUSSTR":
                            tokens[i] = _caster.Stats.BonusStr.ToString();
                            break;
                        case "$CASTERBONUSINT":
                            tokens[i] = _caster.Stats.BonusInt.ToString();
                            break;
                        case "$CASTERBONUSWIS":
                            tokens[i] = _caster.Stats.BonusWis.ToString();
                            break;
                        case "$CASTERBONUSCON":
                            tokens[i] = _caster.Stats.BonusCon.ToString();
                            break;
                        case "$CASTERBONUSDEX":
                            tokens[i] = _caster.Stats.BonusDex.ToString();
                            break;
                        case "$CASTERBONUSHP":
                            tokens[i] = _caster.Stats.BonusHp.ToString();
                            break;
                        case "$CASTERBONUSMP":
                            tokens[i] = _caster.Stats.BonusMp.ToString();
                            break;
                        case "$CASTERBONUSDMG":
                            tokens[i] = _caster.Stats.BonusDmg.ToString();
                            break;
                        case "$CASTERBONUSHIT":
                            tokens[i] = _caster.Stats.BonusHit.ToString();
                            break;
                        case "$CASTERBONUSMR":
                            tokens[i] = _caster.Stats.BonusMr.ToString();
                            break;
                        case "$CASTERBONUSAC":
                            tokens[i] = _caster.Stats.BonusAc.ToString();
                            break;
                        case "$CASTERWEAPONDMG":
                            {
                                var rand = new Random();
                                if (_caster.Equipment.Weapon == null)
                                    tokens[i] = "0";
                                else
                                {
                                    var mindmg = (int)_caster.Equipment.Weapon.MinSDamage;
                                    var maxdmg = (int)_caster.Equipment.Weapon.MaxSDamage;
                                    if (mindmg == 0) mindmg = 1;
                                    if (maxdmg == 0) maxdmg = 1;
                                    tokens[i] = rand.Next(mindmg, maxdmg).ToString();
                                }
                            }
                            break;
                        case "$CASTERWEAPONSDMG":
                            {
                                var rand = new Random();
                                if (_caster.Equipment.Weapon == null)
                                    tokens[i] = "0";
                                else
                                {
                                    var mindmg = (int)_caster.Equipment.Weapon.MinSDamage;
                                    var maxdmg = (int)_caster.Equipment.Weapon.MaxSDamage;
                                    if (mindmg == 0) mindmg = 1;
                                    if (maxdmg == 0) maxdmg = 1;
                                    tokens[i] = rand.Next(mindmg, maxdmg).ToString();
                                }
                            }
                            break;
                        case "$CASTERWEAPONSDMGMIN":
                            {
                                tokens[i] = (_caster.Equipment.Weapon?.MinSDamage ?? '0').ToString();
                            }
                            break;
                        case "$CASTERWEAPONSDMGMAX":
                            {
                                tokens[i] = (_caster.Equipment.Weapon?.MaxSDamage ?? '0').ToString();
                            }
                            break;
                        case "$CASTERWEAPONLDMG":
                            {
                                var rand = new Random();
                                if (_caster.Equipment.Weapon == null)
                                    tokens[i] = "0";
                                else
                                {
                                    var mindmg = (int)_caster.Equipment.Weapon.MinLDamage;
                                    var maxdmg = (int)_caster.Equipment.Weapon.MaxLDamage;
                                    if (mindmg == 0) mindmg = 1;
                                    if (maxdmg == 0) maxdmg = 1;
                                    tokens[i] = rand.Next(mindmg, maxdmg).ToString();
                                }
                            }
                            break;
                        case "$CASTERWEAPONLDMGMIN":
                            {
                                tokens[i] = (_caster.Equipment.Weapon?.MinLDamage ?? '0').ToString();
                            }
                            break;
                        case "$CASTERWEAPONLDMGMAX":
                            {
                                tokens[i] = (_caster.Equipment.Weapon?.MaxLDamage ?? '0').ToString();
                            }
                            break;
                        case "$CASTABLELEVEL":
                            {
                                //this is temporary until castable.currentlevel is implemented.
                                tokens[i] = "1"; 
                                //if (_castable.Effects.CastableLevel == 0) tokens[i] = "1";
                                //tokens[i] = _caster.CastableLevel.ToString();
                            }
                            break;
                        case "$TARGETSTR":
                            tokens[i] = _target.Stats.Str.ToString();
                            break;
                        case "$TARGETINT":
                            tokens[i] = _target.Stats.Int.ToString();
                            break;
                        case "$TARGETWIS":
                            tokens[i] = _target.Stats.Wis.ToString();
                            break;
                        case "$TARGETCON":
                            tokens[i] = _target.Stats.Con.ToString();
                            break;
                        case "$TARGETDEX":
                            tokens[i] = _target.Stats.Dex.ToString();
                            break;
                        case "$TARGETDMG":
                            tokens[i] = _target.Stats.Dmg.ToString();
                            break;
                        case "$TARGETHIT":
                            tokens[i] = _target.Stats.Hit.ToString();
                            break;
                        case "$TARGETMR":
                            tokens[i] = _target.Stats.Mr.ToString();
                            break;
                        case "$TARGETAC":
                            tokens[i] = _target.Stats.Ac.ToString();
                            break;
                        case "$TARGETHP":
                            tokens[i] = _target.Stats.Hp.ToString();
                            break;
                        case "$TARGETMP":
                            tokens[i] = _target.Stats.Mp.ToString();
                            break;
                        case "$TARGETLEVEL":
                            tokens[i] = _target.Stats.Level.ToString();
                            break;
                        case "$TARGETAB":
                            tokens[i] = _target.Stats.Ability.ToString();
                            break;
                        case "$TARGETGOLD":
                            tokens[i] = _target.Gold.ToString();
                            break;
                        case "$TARGETBASESTR":
                            tokens[i] = _target.Stats.BaseStr.ToString();
                            break;
                        case "$TARGETBASEINT":
                            tokens[i] = _target.Stats.BaseInt.ToString();
                            break;
                        case "$TARGETBASEWIS":
                            tokens[i] = _target.Stats.BaseWis.ToString();
                            break;
                        case "$TARGETBASECON":
                            tokens[i] = _target.Stats.BaseCon.ToString();
                            break;
                        case "$TARGETBASEDEX":
                            tokens[i] = _target.Stats.BaseDex.ToString();
                            break;
                        case "$TARGETBASEHP":
                            tokens[i] = _target.Stats.BaseHp.ToString();
                            break;
                        case "$TARGETBASEMP":
                            tokens[i] = _target.Stats.BaseMp.ToString();
                            break;
                        case "$TARGETBONUSSTR":
                            tokens[i] = _target.Stats.BonusStr.ToString();
                            break;
                        case "$TARGETBONUSINT":
                            tokens[i] = _target.Stats.BonusInt.ToString();
                            break;
                        case "$TARGETBONUSWIS":
                            tokens[i] = _target.Stats.BonusWis.ToString();
                            break;
                        case "$TARGETBONUSCON":
                            tokens[i] = _target.Stats.BonusCon.ToString();
                            break;
                        case "$TARGETBONUSDEX":
                            tokens[i] = _target.Stats.BonusDex.ToString();
                            break;
                        case "$TARGETBONUSHP":
                            tokens[i] = _target.Stats.BonusHp.ToString();
                            break;
                        case "$TARGETBONUSMP":
                            tokens[i] = _target.Stats.BonusMp.ToString();
                            break;
                        case "$TARGETBONUSDMG":
                            tokens[i] = _target.Stats.BonusDmg.ToString();
                            break;
                        case "$TARGETBONUSHIT":
                            tokens[i] = _target.Stats.BonusHit.ToString();
                            break;
                        case "$TARGETBONUSMR":
                            tokens[i] = _target.Stats.BonusMr.ToString();
                            break;
                        case "$TARGETBONUSAC":
                            tokens[i] = _target.Stats.BonusAc.ToString();
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
