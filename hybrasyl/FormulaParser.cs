﻿using System;
using System.Collections.Generic;
using System.Text;
using Hybrasyl.Castables;
using Hybrasyl.Objects;

namespace Hybrasyl
{
    internal class FormulaParser
    {
        private Creature _caster;
        private Castable _castable;
        private Creature _target;
        public FormulaParser(Creature caster, Castable castable, Creature target = null)
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
                            tokens[i] = _caster.Str.ToString();
                            break;
                        case "$CASTERINT":
                            tokens[i] = _caster.Int.ToString();
                            break;
                        case "$CASTERWIS":
                            tokens[i] = _caster.Wis.ToString();
                            break;
                        case "$CASTERCON":
                            tokens[i] = _caster.Con.ToString();
                            break;
                        case "$CASTERDEX":
                            tokens[i] = _caster.Dex.ToString();
                            break;
                        case "$CASTERDMG":
                            tokens[i] = _caster.Dmg.ToString();
                            break;
                        case "$CASTERHIT":
                            tokens[i] = _caster.Hit.ToString();
                            break;
                        case "$CASTERMR":
                            tokens[i] = _caster.Mr.ToString();
                            break;
                        case "$CASTERAC":
                            tokens[i] = _caster.Ac.ToString();
                            break;
                        case "$CASTERHP":
                            tokens[i] = _caster.Hp.ToString();
                            break;
                        case "$CASTERMP":
                            tokens[i] = _caster.Mp.ToString();
                            break;
                        case "$CASTERLEVEL":
                            tokens[i] = _caster.Level.ToString();
                            break;
                        case "$CASTERAB":
                            tokens[i] = _caster.Ability.ToString();
                            break;
                        case "$CASTERGOLD":
                            tokens[i] = _caster.Gold.ToString();
                            break;
                        case "$CASTERBASESTR":
                            tokens[i] = _caster.BaseStr.ToString();
                            break;
                        case "$CASTERBASEINT":
                            tokens[i] = _caster.BaseInt.ToString();
                            break;
                        case "$CASTERBASEWIS":
                            tokens[i] = _caster.BaseWis.ToString();
                            break;
                        case "$CASTERBASECON":
                            tokens[i] = _caster.BaseCon.ToString();
                            break;
                        case "$CASTERBASEDEX":
                            tokens[i] = _caster.BaseDex.ToString();
                            break;
                        case "$CASTERBASEHP":
                            tokens[i] = _caster.BaseHp.ToString();
                            break;
                        case "$CASTERBASEMP":
                            tokens[i] = _caster.BaseMp.ToString();
                            break;
                        case "$CASTERBONUSSTR":
                            tokens[i] = _caster.BonusStr.ToString();
                            break;
                        case "$CASTERBONUSINT":
                            tokens[i] = _caster.BonusInt.ToString();
                            break;
                        case "$CASTERBONUSWIS":
                            tokens[i] = _caster.BonusWis.ToString();
                            break;
                        case "$CASTERBONUSCON":
                            tokens[i] = _caster.BonusCon.ToString();
                            break;
                        case "$CASTERBONUSDEX":
                            tokens[i] = _caster.BonusDex.ToString();
                            break;
                        case "$CASTERBONUSHP":
                            tokens[i] = _caster.BonusHp.ToString();
                            break;
                        case "$CASTERBONUSMP":
                            tokens[i] = _caster.BonusMp.ToString();
                            break;
                        case "$CASTERBONUSDMG":
                            tokens[i] = _caster.BonusDmg.ToString();
                            break;
                        case "$CASTERBONUSHIT":
                            tokens[i] = _caster.BonusHit.ToString();
                            break;
                        case "$CASTERBONUSMR":
                            tokens[i] = _caster.BonusMr.ToString();
                            break;
                        case "$CASTERBONUSAC":
                            tokens[i] = _caster.BonusAc.ToString();
                            break;
                        case "$CASTERWEAPONSDMG":
                            {
                                var rand = new Random();
                                var mindmg = (int)_caster.Equipment.Weapon.MinSDamage;
                                var maxdmg = (int)_caster.Equipment.Weapon.MaxSDamage;
                                if (mindmg == 0) mindmg = 1;
                                if (maxdmg == 0) maxdmg = 1;
                                tokens[i] = rand.Next(mindmg, maxdmg).ToString();
                            }
                            break;
                        case "$CASTERWEAPONSDMGMIN":
                            {
                                tokens[i] = _caster.Equipment.Weapon.MinSDamage.ToString();
                            }
                            break;
                        case "$CASTERWEAPONSDMGMAX":
                            {
                                tokens[i] = _caster.Equipment.Weapon.MaxSDamage.ToString();
                            }
                            break;
                        case "$CASTERWEAPONLDMG":
                            {
                                var rand = new Random();
                                var mindmg = (int)_caster.Equipment.Weapon.MinLDamage;
                                var maxdmg = (int)_caster.Equipment.Weapon.MaxLDamage;
                                if (mindmg == 0) mindmg = 1;
                                if (maxdmg == 0) maxdmg = 1;
                                tokens[i] = rand.Next(mindmg, maxdmg).ToString();
                            }
                            break;
                        case "$CASTERWEAPONLDMGMIN":
                            {
                                tokens[i] = _caster.Equipment.Weapon.MinLDamage.ToString();
                            }
                            break;
                        case "$CASTERWEAPONLDMGMAX":
                            {
                                tokens[i] = _caster.Equipment.Weapon.MaxLDamage.ToString();
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
                            tokens[i] = _target.Str.ToString();
                            break;
                        case "$TARGETINT":
                            tokens[i] = _target.Int.ToString();
                            break;
                        case "$TARGETWIS":
                            tokens[i] = _target.Wis.ToString();
                            break;
                        case "$TARGETCON":
                            tokens[i] = _target.Con.ToString();
                            break;
                        case "$TARGETDEX":
                            tokens[i] = _target.Dex.ToString();
                            break;
                        case "$TARGETDMG":
                            tokens[i] = _target.Dmg.ToString();
                            break;
                        case "$TARGETHIT":
                            tokens[i] = _target.Hit.ToString();
                            break;
                        case "$TARGETMR":
                            tokens[i] = _target.Mr.ToString();
                            break;
                        case "$TARGETAC":
                            tokens[i] = _target.Ac.ToString();
                            break;
                        case "$TARGETHP":
                            tokens[i] = _target.Hp.ToString();
                            break;
                        case "$TARGETMP":
                            tokens[i] = _target.Mp.ToString();
                            break;
                        case "$TARGETLEVEL":
                            tokens[i] = _target.Level.ToString();
                            break;
                        case "$TARGETAB":
                            tokens[i] = _target.Ability.ToString();
                            break;
                        case "$TARGETGOLD":
                            tokens[i] = _target.Gold.ToString();
                            break;
                        case "$TARGETBASESTR":
                            tokens[i] = _target.BaseStr.ToString();
                            break;
                        case "$TARGETBASEINT":
                            tokens[i] = _target.BaseInt.ToString();
                            break;
                        case "$TARGETBASEWIS":
                            tokens[i] = _target.BaseWis.ToString();
                            break;
                        case "$TARGETBASECON":
                            tokens[i] = _target.BaseCon.ToString();
                            break;
                        case "$TARGETBASEDEX":
                            tokens[i] = _target.BaseDex.ToString();
                            break;
                        case "$TARGETBASEHP":
                            tokens[i] = _target.BaseHp.ToString();
                            break;
                        case "$TARGETBASEMP":
                            tokens[i] = _target.BaseMp.ToString();
                            break;
                        case "$TARGETBONUSSTR":
                            tokens[i] = _target.BonusStr.ToString();
                            break;
                        case "$TARGETBONUSINT":
                            tokens[i] = _target.BonusInt.ToString();
                            break;
                        case "$TARGETBONUSWIS":
                            tokens[i] = _target.BonusWis.ToString();
                            break;
                        case "$TARGETBONUSCON":
                            tokens[i] = _target.BonusCon.ToString();
                            break;
                        case "$TARGETBONUSDEX":
                            tokens[i] = _target.BonusDex.ToString();
                            break;
                        case "$TARGETBONUSHP":
                            tokens[i] = _target.BonusHp.ToString();
                            break;
                        case "$TARGETBONUSMP":
                            tokens[i] = _target.BonusMp.ToString();
                            break;
                        case "$TARGETBONUSDMG":
                            tokens[i] = _target.BonusDmg.ToString();
                            break;
                        case "$TARGETBONUSHIT":
                            tokens[i] = _target.BonusHit.ToString();
                            break;
                        case "$TARGETBONUSMR":
                            tokens[i] = _target.BonusMr.ToString();
                            break;
                        case "$TARGETBONUSAC":
                            tokens[i] = _target.BonusAc.ToString();
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
