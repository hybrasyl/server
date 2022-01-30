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

            defaultDesc += FormatBonusPct(BonusHp, "Hp");
            defaultDesc += FormatBonusPct(BonusMp, "Mp");
            defaultDesc += FormatBonusPct(BonusStr, "Str");
            defaultDesc += FormatBonusPct(BonusInt, "Int");
            defaultDesc += FormatBonusPct(BonusWis, "Wis");
            defaultDesc += FormatBonusPct(BonusCon, "Con");
            defaultDesc += FormatBonusPct(BonusDex, "Dex");
            defaultDesc += FormatBonusNum(BonusCrit, "Crit");
            defaultDesc += FormatBonusNum(BonusMagicCrit, "Magic Crit");
            defaultDesc += FormatBonusPct(BonusDmg, "Dmg");
            defaultDesc += FormatBonusPct(BonusHit, "Hit");
            defaultDesc += FormatBonusPct(BonusAc, "Ac");
            defaultDesc += FormatBonusPct(BonusMr, "Mr");
            defaultDesc += FormatBonusPct(BonusRegen, "Regen");
            defaultDesc += FormatBonusNum(BonusReflectMagical, "Reflect Magic");
            defaultDesc += FormatBonusNum(BonusReflectPhysical, "Reflect Phys");
            defaultDesc += FormatBonusNum(BonusExtraGold, "Gold");
            defaultDesc += FormatBonusNum(BonusDodge, "Dodge");
            defaultDesc += FormatBonusNum(BonusMagicDodge, "Magic Dodge");
            defaultDesc += FormatBonusNum(BonusExtraXp, "Xp");
            defaultDesc += FormatBonusNum(BonusExtraItemFind, "Items");
            defaultDesc += FormatBonusNum(BonusLifeSteal, "Life Steal");
            defaultDesc += FormatBonusNum(BonusManaSteal, "Mana Steal");
            return defaultDesc;
        }
    }
}

