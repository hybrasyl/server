using Hybrasyl.Castables;
using Hybrasyl.Objects;
using Hybrasyl.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Hybrasyl.Statuses;

namespace Hybrasyl
{

    /// <summary>
    /// This class is used to do a variety of numerical calculations, in order to consolidate those into
    /// one place. Specifically, healing and damage, are handled here.
    /// </summary>
    /// 

    static class NumberCruncher
    {

        public static ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // This is dumb, but it's a consequence of how xsd2code works
        static private double _evalSimple(dynamic simple)
        {
            if (simple is Statuses.SimpleQuantity || simple is Castables.SimpleQuantity)
            {
                var rand = new Random();
                return rand.Next(Convert.ToInt32(simple.Min), Convert.ToInt32(simple.Max));
            }
            throw new InvalidOperationException("Invalid type passed to _evalSimple");
        }

        static private double _evalFormula(string formula, Castable castable, Creature target, Creature source)
        {
            try
            {
                return new FormulaParser(source, castable, target).Eval(formula);
            }
            catch (Exception e)
            {
                Logger.Error($"NumberCruncher formula error: castable {castable.Name}, target {target.Name}, source {source?.Name ?? "no source"}: {formula}, error: {e}");
                return 0;
            }
        }

        static public (Enums.DamageType Type, double Amount) CalculateDamage(Castable castable, Creature target, Creature source = null)
        {
            var rand = new Random();
            // Defaults
            double dmg = 1;
            var type = EnumUtil.ParseEnum(castable.Effects.Damage.Type.ToString(), Enums.DamageType.Magical);

            if (!string.IsNullOrEmpty(castable.Effects?.Damage?.Formula))
            {
                var formula = castable.Effects.Damage.Formula;
                dmg = _evalFormula(formula, castable, target, source);
            }
            else if (castable.Effects.Damage?.Simple != null)
            {
                var simple = castable.Effects.Damage.Simple;
                dmg = _evalSimple(simple);
            }
        
            return (type, dmg);
        }

        static public double CalculateHeal(Castable castable, Creature target, Creature source = null )
        {
            var rand = new Random();
            if (castable.Effects?.Heal?.Simple != null)
            {
                return _evalSimple(castable.Effects.Heal.Simple);
            }
            else if (!string.IsNullOrEmpty(castable.Effects?.Heal?.Formula))
            {
                var formula = castable.Effects.Heal.Formula;
                return _evalFormula(formula, castable, target, source);
            }
            return 0;
        }

        static public (Enums.DamageType Type, double Amount) CalculateDamage(Castable castable, ModifierEffect effect, string statusName, Creature target, Creature source = null)
        {
            // Find the status first (we'll need intensity later)
            var statusAdd = castable.Statuses.Add.Where(e => e.Value == statusName).ToList();

            // Defaults
            double dmg = 0;
            var type = EnumUtil.ParseEnum(effect.Damage.Type.ToString(), Enums.DamageType.Magical);

            if (statusAdd.Count != 0)
            {
                if (effect.Damage != null)
                {
                    if (effect.Damage.Simple != null)
                        dmg = _evalSimple(effect.Damage.Simple);
                    else if (!string.IsNullOrEmpty(effect.Damage.Formula))
                        dmg = _evalFormula(effect.Damage.Formula, castable, target, source);
                }
            }
            else
            {
                Logger.Error($"CalculateDamage: castable {castable.Name}, status {statusName} - status not found in castable...?");
            }

            return (type, dmg * statusAdd[0].Intensity);
        }

        static public double CalculateHeal(Castable castable, ModifierEffect effect, string statusName, Creature target, Creature source = null)
        {
            // Find the status first
            var statusAdd = castable.Statuses.Add.Where(e => e.Value == statusName).ToList();
            // Defaults
            double heal = 0;

            if (statusAdd.Count != 0)
            {
                if (effect.Heal != null)
                {
                    if (effect.Heal.Simple != null)
                        heal = _evalSimple(effect.Heal.Simple);
                    else if (!string.IsNullOrEmpty(effect.Heal.Formula))
                        heal = _evalFormula(effect.Damage.Formula, castable, target, source);
                }
            }
            else
            {
                Logger.Error($"CalculateTickDamage: castable {castable.Name}, status {statusName} - status not found in castable...?");
            }

            return heal * statusAdd[0].Intensity;

        }

    }
}
