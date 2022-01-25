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
using Hybrasyl.Objects;
using NCalc;

namespace Hybrasyl;

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

internal static class FormulaParser
{
    private static readonly Random _rnd = new Random();
        
    // TODO: potentially use reflection to simplify this
    public static Expression Parameterize(Expression e, FormulaEvaluation eval)
    {
            
        // Caster parameters
        e.Parameters["CASTERSTR"] = eval.Caster?.Stats.Str ?? 0;
        e.Parameters["CASTERINT"] = eval.Caster?.Stats.Int ?? 0;
        e.Parameters["CASTERWIS"] = eval.Caster?.Stats.Wis ?? 0;
        e.Parameters["CASTERDEX"] = eval.Caster?.Stats.Dex ?? 0;
        e.Parameters["CASTERCON"] = eval.Caster?.Stats.Con ?? 0;
        e.Parameters["CASTERBASESTR"] = eval.Caster?.Stats.BaseStr ?? 0;
        e.Parameters["CASTERBASEINT"] = eval.Caster?.Stats.BaseInt ?? 0;
        e.Parameters["CASTERBASEWIS"] = eval.Caster?.Stats.BaseWis ?? 0;
        e.Parameters["CASTERBASEDEX"] = eval.Caster?.Stats.BaseDex ?? 0;
        e.Parameters["CASTERBASECON"] = eval.Caster?.Stats.BaseCon ?? 0;
        e.Parameters["CASTERBASEHP"] = eval.Caster?.Stats.BaseHp ?? 0;
        e.Parameters["CASTERBASEMP"] = eval.Caster?.Stats.BaseMp ?? 0;
        e.Parameters["CASTERBONUSSTR"] = eval.Caster?.Stats.BonusStr ?? 0;
        e.Parameters["CASTERBONUSINT"] = eval.Caster?.Stats.BonusInt ?? 0;
        e.Parameters["CASTERBONUSWIS"] = eval.Caster?.Stats.BonusWis ?? 0;
        e.Parameters["CASTERBONUSDEX"] = eval.Caster?.Stats.BonusDex ?? 0;
        e.Parameters["CASTERBONUSCON"] = eval.Caster?.Stats.BonusCon ?? 0;
        e.Parameters["CASTERBONUSHP"] = eval.Caster?.Stats.BonusDex ?? 0;
        e.Parameters["CASTERBONUSMP"] = eval.Caster?.Stats.BonusCon ?? 0;
        e.Parameters["CASTERBONUSDMG"] = eval.Caster?.Stats.BonusDmg ?? 0;
        e.Parameters["CASTERBONUSHIT"] = eval.Caster?.Stats.BonusHit ?? 0;
        e.Parameters["CASTERBONUSMR"] = eval.Caster?.Stats.BonusMr ?? 0;
        e.Parameters["CASTERBONUSAC"] = eval.Caster?.Stats.BonusAc ?? 0;
        e.Parameters["CASTERDMG"] = eval.Caster?.Stats.Dmg ?? 0;
        e.Parameters["CASTERHIT"] = eval.Caster?.Stats.Dmg ?? 0;
        e.Parameters["CASTERMR"] = eval.Caster?.Stats.Dmg ?? 0;
        e.Parameters["CASTERAC"] = eval.Caster?.Stats.Dmg ?? 0;
        e.Parameters["CASTERHP"] = eval.Caster?.Stats.Dmg ?? 0;
        e.Parameters["CASTERMP"] = eval.Caster?.Stats.Dmg ?? 0;
        e.Parameters["CASTERLEVEL"] = eval.Caster?.Stats.Dmg ?? 0;
        e.Parameters["CASTERAB"] = eval.Caster?.Stats.Dmg ?? 0;
        e.Parameters["CASTERGOLD"] = eval.Caster?.Stats.Dmg ?? 0;

        e.EvaluateParameter += delegate (string name, ParameterArgs args)
        {
            if (name == "CASTERWEAPONDMG" || name == "CASTERWEAPONSDMG")
            {
                if (eval.Caster?.Equipment?.Weapon is null)
                    args.Result = 0;
                else
                {
                    var mindmg = (int)eval.Caster.Equipment.Weapon.MinSDamage;
                    var maxdmg = (int)eval.Caster.Equipment.Weapon.MaxSDamage;
                    if (mindmg == 0) mindmg = 1;
                    if (maxdmg == 0) maxdmg = 1;
                    args.Result = _rnd.Next(mindmg, maxdmg + 1);
                }
            }
            else if (name == "CASTERWEAPONSDMGMIN")
                args.Result = (eval.Caster?.Equipment?.Weapon?.MinSDamage ?? 1);
            else if (name == "CASTERWEAPONSDMGMAX")
                args.Result = (eval.Caster?.Equipment?.Weapon?.MaxSDamage ?? 1);
            else if (name == "CASTERWEAPONLDMG")
            {
                if (eval.Caster?.Equipment?.Weapon is null)
                    args.Result = 0;
                else
                {
                    var mindmg = (int)eval.Caster.Equipment.Weapon.MinLDamage;
                    var maxdmg = (int)eval.Caster.Equipment.Weapon.MaxLDamage;
                    if (mindmg == 0) mindmg = 1;
                    if (maxdmg == 0) maxdmg = 1;
                    args.Result = _rnd.Next(mindmg, maxdmg + 1);
                }
            }
            else if (name == "CASTERWEAPONLDMGMIN")
                args.Result = (eval.Caster?.Equipment?.Weapon?.MinLDamage ?? 1);
            else if (name == "CASTERWEAPONLDMGMAX")
                args.Result = (eval.Caster?.Equipment?.Weapon?.MaxLDamage ?? 1);
            else if (name == "CASTABLELEVEL")
                args.Result = eval.Castable.CastableLevel;
        };

        e.Parameters["TARGETSTR"] = eval.Target?.Stats.Str ?? 0;
        e.Parameters["TARGETINT"] = eval.Target?.Stats.Int ?? 0;
        e.Parameters["TARGETWIS"] = eval.Target?.Stats.Wis ?? 0;
        e.Parameters["TARGETDEX"] = eval.Target?.Stats.Dex ?? 0;
        e.Parameters["TARGETCON"] = eval.Target?.Stats.Con ?? 0;
        e.Parameters["TARGETBASESTR"] = eval.Target?.Stats.BaseStr ?? 0;
        e.Parameters["TARGETBASEINT"] = eval.Target?.Stats.BaseInt ?? 0;
        e.Parameters["TARGETBASEWIS"] = eval.Target?.Stats.BaseWis ?? 0;
        e.Parameters["TARGETBASEDEX"] = eval.Target?.Stats.BaseDex ?? 0;
        e.Parameters["TARGETBASECON"] = eval.Target?.Stats.BaseCon ?? 0;
        e.Parameters["TARGETBASEHP"] = eval.Target?.Stats.BaseHp ?? 0;
        e.Parameters["TARGETBASEMP"] = eval.Target?.Stats.BaseMp ?? 0;
        e.Parameters["TARGETBONUSSTR"] = eval.Target?.Stats.BonusStr ?? 0;
        e.Parameters["TARGETBONUSINT"] = eval.Target?.Stats.BonusInt ?? 0;
        e.Parameters["TARGETBONUSWIS"] = eval.Target?.Stats.BonusWis ?? 0;
        e.Parameters["TARGETBONUSDEX"] = eval.Target?.Stats.BonusDex ?? 0;
        e.Parameters["TARGETBONUSCON"] = eval.Target?.Stats.BonusCon ?? 0;
        e.Parameters["TARGETBONUSHP"] = eval.Target?.Stats.BonusDex ?? 0;
        e.Parameters["TARGETBONUSMP"] = eval.Target?.Stats.BonusCon ?? 0;
        e.Parameters["TARGETBONUSDMG"] = eval.Target?.Stats.BonusDmg ?? 0;
        e.Parameters["TARGETBONUSHIT"] = eval.Target?.Stats.BonusHit ?? 0;
        e.Parameters["TARGETBONUSMR"] = eval.Target?.Stats.BonusMr ?? 0;
        e.Parameters["TARGETBONUSAC"] = eval.Target?.Stats.BonusAc ?? 0;
        e.Parameters["TARGETDMG"] = eval.Target?.Stats.Dmg ?? 0;
        e.Parameters["TARGETHIT"] = eval.Target?.Stats.Dmg ?? 0;
        e.Parameters["TARGETMR"] = eval.Target?.Stats.Dmg ?? 0;
        e.Parameters["TARGETAC"] = eval.Target?.Stats.Dmg ?? 0;
        e.Parameters["TARGETHP"] = eval.Target?.Stats.Dmg ?? 0;
        e.Parameters["TARGETMP"] = eval.Target?.Stats.Dmg ?? 0;
        e.Parameters["TARGETLEVEL"] = eval.Target?.Stats.Dmg ?? 0;
        e.Parameters["TARGETAB"] = eval.Target?.Stats.Dmg ?? 0;
        e.Parameters["TARGETGOLD"] = eval.Target?.Stats.Dmg ?? 0;

        e.Parameters["MAPBASELEVEL"] = eval.Map?.SpawnDirectives?.BaseLevel ?? "1";

        e.Parameters["MAPTILES"] = eval.Map?.X ?? 0 * eval.Map?.Y ?? 0;
        e.Parameters["MAPX"] = eval.Map?.X ?? 0;
        e.Parameters["MAPY"] = eval.Map?.Y ?? 0;
        e.Parameters["DAMAGE"] = eval.Damage ?? 0;

        e.Parameters["USERCRIT"] = eval.User?.Stats?.BaseCrit ?? 0;
        e.Parameters["USERHIT"] = eval.User?.Stats?.Hit ?? 0;
        e.Parameters["USERMR"] = eval.User?.Stats?.Mr ?? 0;
        e.Parameters["USERHP"] = eval.User?.Stats?.Hp ?? 0;
        e.Parameters["USERMP"] = eval.User?.Stats?.Mp ?? 0;

        e.Parameters["SPAWNHP"] = eval.Spawn?.Stats?.Hp ?? 0;
        e.Parameters["SPAWNMP"] = eval.Spawn?.Stats?.Hp ?? 0;
        e.Parameters["SPAWNXP"] = eval.Spawn?.Stats?.Hp ?? 0;

        e.Parameters["RAND_5"] = _rnd.Next(0, 6);
        e.Parameters["RAND_10"] = _rnd.Next(0, 11);
        e.Parameters["RAND_100"] = _rnd.Next(0, 101);
        e.Parameters["RAND_1000"] = _rnd.Next(0, 1001);
        return e;
    }
    public static double Eval(string expression, FormulaEvaluation evalEnvironment)
    {
        Expression e = new(expression);
        e = Parameterize(e, evalEnvironment);
        var ret = e.Evaluate();
        GameLog.Info($"Eval of {expression} : {ret} ");
        return Convert.ToDouble(e.Evaluate());
    }
}