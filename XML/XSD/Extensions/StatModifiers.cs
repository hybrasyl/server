using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Xml;

public partial class StatModifiers
{

    public static string FormatBonusPct(string bonus, string name)
    {
        if (!double.TryParse(bonus, out var num)) return $"??? {name} \n";
        return num == 0 ? string.Empty : $"{(num > 0 ? "+" + num.ToString("P") : "-" + num.ToString("P"))} {name} \n";
    }



    public static string FormatBonusNum(string bonus, string name)
    {
        if (!long.TryParse(bonus, out var num)) return $"??? {name} \n";
        return num == 0 ? string.Empty : $"{(num > 0 ? "+" + num.ToString("P") : "-" + num.ToString("P"))} {name} \n";

    }

    public string BonusString
    {
        get
        {
            var defaultDesc = "";

            defaultDesc += FormatBonusPct(Hp, "Hp");
            defaultDesc += FormatBonusPct(Mp, "Mp");
            defaultDesc += FormatBonusPct(Str, "Str");
            defaultDesc += FormatBonusPct(Int, "Int");
            defaultDesc += FormatBonusPct(Wis, "Wis");
            defaultDesc += FormatBonusPct(Con, "Con");
            defaultDesc += FormatBonusPct(Dex, "Dex");
            defaultDesc += FormatBonusNum(Crit, "Crit");
            defaultDesc += FormatBonusNum(MagicCrit, "Magic Crit");
            defaultDesc += FormatBonusPct(Dmg, "Dmg");
            defaultDesc += FormatBonusPct(Hit, "Hit");
            defaultDesc += FormatBonusPct(Ac, "Ac");
            defaultDesc += FormatBonusPct(Mr, "Mr");
            defaultDesc += FormatBonusPct(Regen, "Regen");
            defaultDesc += FormatBonusNum(ReflectMagical, "Reflect Magic");
            defaultDesc += FormatBonusNum(ReflectPhysical, "Reflect Phys");
            defaultDesc += FormatBonusNum(ExtraGold, "Gold");
            defaultDesc += FormatBonusNum(Dodge, "Dodge");
            defaultDesc += FormatBonusNum(MagicDodge, "Magic Dodge");
            defaultDesc += FormatBonusNum(ExtraXp, "Xp");
            defaultDesc += FormatBonusNum(ExtraItemFind, "Items");
            defaultDesc += FormatBonusNum(LifeSteal, "Life Steal");
            defaultDesc += FormatBonusNum(ManaSteal, "Mana Steal");
            return defaultDesc;
        }
    }
}

