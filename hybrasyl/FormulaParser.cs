using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.XSD;
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
            List<string> tokens = GetTokens(expression);
            Stack<double> operandStack = new Stack<double>();
            Stack<string> operatorStack = new Stack<string>();
            int tokenIndex = 0;

            MapTokens(ref tokens);

            while (tokenIndex < tokens.Count)
            {
                string token = tokens[tokenIndex];
                if (token == "(")
                {
                    string subExpr = GetSubExpression(tokens, ref tokenIndex);
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
                        string op = operatorStack.Pop();
                        double arg2 = operandStack.Pop();
                        double arg1 = operandStack.Pop();
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
                string op = operatorStack.Pop();
                double arg2 = operandStack.Pop();
                double arg1 = operandStack.Pop();
                operandStack.Push(_operations[Array.IndexOf(_operators, op)](arg1, arg2));
            }
            return Math.Round(operandStack.Pop(), 0); //probably a better way to do this, however this is what we come up with for now.
        }



        public List<string> GetTokens(string expression)
        {
            string operators = "()^*/+-";
            List<string> tokens = new List<string>();
            StringBuilder sb = new StringBuilder();

            foreach (char c in expression.Replace(" ", string.Empty))
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
            StringBuilder subExpr = new StringBuilder();
            int parenlevels = 1;
            index += 1;
            while (index < tokens.Count && parenlevels > 0)
            {
                string token = tokens[index];
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
            for (int i = 0; i < tokens.Count; i++)
            {
                string s = tokens[i];
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
                        case "$CASTERONUSDMG":
                            tokens[i] = _caster.BonusDmg.ToString();
                            break;
                        case "$CASTERONUSHIT":
                            tokens[i] = _caster.BonusHit.ToString();
                            break;
                        case "$CASTERBONUSMR":
                            tokens[i] = _caster.BonusMr.ToString();
                            break;
                        case "$CASTERBONUSAC":
                            tokens[i] = _caster.BonusAc.ToString();
                            break;
                        case "$CASTABLELEVEL":
                            {
                                //this is temporary until castable.currentlevel is implemented.
                                tokens[i] = "1"; 
                                //if (_castable.Effects.CastableLevel == 0) tokens[i] = "1";
                                //tokens[i] = _caster.CastableLevel.ToString();
                            }
                            break;
                        default:
                            return false;
                            //handles an undefined token.
                    }

                }
            }
            return true;
        }
    }
}
