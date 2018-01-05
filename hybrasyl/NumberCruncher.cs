using Hybrasyl.Castables;
using Hybrasyl.Objects;
using Hybrasyl.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl
{

    /// <summary>
    /// This class is used to do a variety of numerical calculations, in order to consolidate those into
    /// one place. Specifically, healing and damage, are handled here.
    /// </summary>
    /// 

    static class NumberCruncher
    {
        static public (Enums.DamageType Type, double Amount) CalculateDamage(Castable castable, Creature target, Creature source = null)
        {
            var rand = new Random();
            // Defaults
            double dmg = 1;
            Enums.DamageType type;

            if (castable.Effects.Damage?.Simple != null)
            {
                var simple = castable.Effects.Damage.Simple;
                type = EnumUtil.ParseEnum(castable.Effects.Damage.Type.ToString(), Enums.DamageType.Magical);
                dmg = rand.Next(Convert.ToInt32(simple.Min), Convert.ToInt32(simple.Max));
            }
            else if (castable.Effects?.Heal?.Formula != null)
            {
                var formula = castable.Effects.Damage.Formula;
                type = EnumUtil.ParseEnum(castable.Effects.Damage.Type.ToString(), Enums.DamageType.Magical);
                var parser = new FormulaParser(source, castable, target);
                dmg = parser.Eval(formula);
            }
            else
            {
                // catchall for now
                type = Enums.DamageType.Direct;
            }
            return (type, dmg);
        }

        static public double CalculateHealing(Castable castable, Creature target, Creature source = null )
        {
            var rand = new Random();
            if (castable.Effects?.Heal?.Simple == null)
            {
                var simple = castable.Effects.Heal.Simple;
                return rand.Next(Convert.ToInt32(simple.Min), Convert.ToInt32(simple.Max));
            }
            else if (castable.Effects?.Heal?.Formula != null)
            {
                var formula = castable.Effects.Heal.Formula;
                var parser = new FormulaParser(source, castable, target);
                return parser.Eval(formula);
            }
            return 0;
        }
    }
}
