using System;
using System.Reflection.Metadata.Ecma335;


namespace Hybrasyl.Xml;

public partial class StatModifiers
{

    public static string FormatBonusPct(string bonus, string name, float scale=1)
    {
        if (string.IsNullOrEmpty(bonus)) return string.Empty;
        if (!double.TryParse(bonus, out var num)) return $"??? {name} \n";
        num /= scale;
        return num == 0 ? string.Empty : $"{(num > 0 ? "+" + num + "%" : num + "%")} {name} \n";
    }


    public static string FormatBonusNum(string bonus, string name)
    {
        if (string.IsNullOrEmpty(bonus)) return string.Empty;
        if (!long.TryParse(bonus, out var num)) return $"??? {name} \n";
        return num == 0 ? string.Empty : $"{(num > 0 ? "+" + num : num)} {name} \n";

    }

    public string BonusString
    {
        get
        {
            var defaultDesc = "";

            defaultDesc += FormatBonusNum(BonusHp, "Hp");
            defaultDesc += FormatBonusNum(BonusMp, "Mp");
            defaultDesc += FormatBonusNum(BonusStr, "Str");
            defaultDesc += FormatBonusNum(BonusInt, "Int");
            defaultDesc += FormatBonusNum(BonusWis, "Wis");
            defaultDesc += FormatBonusNum(BonusCon, "Con");
            defaultDesc += FormatBonusNum(BonusDex, "Dex");
            defaultDesc += FormatBonusPct(BonusCrit, "Crit");
            defaultDesc += FormatBonusPct(BonusMagicCrit, "Magic Crit");
            defaultDesc += FormatBonusPct(BonusDmg, "Dmg", 8);
            defaultDesc += FormatBonusPct(BonusHit, "Hit",8);
            defaultDesc += FormatBonusNum(BonusAc, "Ac");
            defaultDesc += FormatBonusPct(BonusMr, "Mr");
            defaultDesc += FormatBonusPct(BonusRegen, "Regen",8);
            defaultDesc += FormatBonusPct(BonusReflectMagical, "Reflect Magic");
            defaultDesc += FormatBonusPct(BonusReflectPhysical, "Reflect Phys");
            defaultDesc += FormatBonusPct(BonusExtraGold, "Gold");
            defaultDesc += FormatBonusPct(BonusDodge, "Dodge");
            defaultDesc += FormatBonusPct(BonusMagicDodge, "Magic Dodge");
            defaultDesc += FormatBonusPct(BonusExtraXp, "Xp");
            defaultDesc += FormatBonusPct(BonusExtraItemFind, "Items");
            defaultDesc += FormatBonusPct(BonusLifeSteal, "Life Steal");
            defaultDesc += FormatBonusPct(BonusManaSteal, "Mana Steal");
            return defaultDesc;
        }
    }

    public static string Combine(string sm1, string sm2)
    {
        if (string.IsNullOrEmpty(sm1) && string.IsNullOrEmpty(sm2)) return string.Empty;

        if (long.TryParse(sm1, out var sml1) && long.TryParse(sm2, out var sml2))
            return Convert.ToString(sml1 + sml2);
        if (double.TryParse(sm1, out var smd1) && double.TryParse(sm2, out var smd2))
            return Convert.ToString(smd1 + smd2);

        if (string.IsNullOrEmpty(sm2)) return sm1;
        if (string.IsNullOrEmpty(sm1)) return sm2;
        return $"{sm1} + {sm2}";

    }

    public static StatModifiers operator +(StatModifiers sm1, StatModifiers sm2)
    {
        sm1.CurrentHp = Combine(sm1.CurrentHp, sm2.CurrentHp);
        sm1.CurrentMp = Combine(sm1.CurrentMp, sm2.CurrentMp);
        sm1.BaseHp = Combine(sm1.BaseHp, sm2.BaseHp);
        sm1.BonusHp = Combine(sm1.BonusHp, sm2.BonusHp);
        sm1.BaseMp = Combine(sm1.BaseMp, sm2.BaseMp);
        sm1.BonusMp = Combine(sm1.BonusMp, sm2.BonusMp);
        sm1.BaseStr = Combine(sm1.BaseStr, sm2.BaseStr);
        sm1.BonusStr = Combine(sm1.BonusStr, sm2.BonusStr);
        sm1.BaseCon = Combine(sm1.BaseCon, sm2.BaseCon);
        sm1.BonusCon = Combine(sm1.BonusCon, sm2.BonusCon);
        sm1.BaseDex = Combine(sm1.BaseDex, sm2.BaseDex);
        sm1.BonusDex = Combine(sm1.BonusDex, sm2.BonusDex);
        sm1.BaseInt = Combine(sm1.BaseInt, sm2.BaseInt);
        sm1.BonusInt = Combine(sm1.BonusInt, sm2.BonusInt);
        sm1.BaseWis = Combine(sm1.BaseWis, sm2.BaseWis);
        sm1.BonusWis = Combine(sm1.BonusWis, sm2.BonusWis);
        sm1.BaseCrit = Combine(sm1.BaseCrit, sm2.BaseCrit);
        sm1.BonusCrit = Combine(sm1.BonusCrit, sm2.BonusCrit);
        sm1.BaseMagicCrit = Combine(sm1.BaseMagicCrit, sm2.BaseMagicCrit);
        sm1.BonusMagicCrit = Combine(sm1.BonusMagicCrit, sm2.BonusMagicCrit);
        sm1.BaseDmg = Combine(sm1.BaseDmg, sm2.BaseDmg);
        sm1.BonusDmg = Combine(sm1.BonusDmg, sm2.BonusDmg);
        sm1.BaseHit = Combine(sm1.BaseHit, sm2.BaseHit);
        sm1.BonusHit = Combine(sm1.BonusHit, sm2.BonusHit);
        sm1.BaseAc = Combine(sm1.BaseAc, sm2.BaseAc);
        sm1.BonusAc = Combine(sm1.BonusAc, sm2.BonusAc);
        sm1.BaseMr = Combine(sm1.BaseMr, sm2.BaseMr);
        sm1.BonusMr = Combine(sm1.BonusMr, sm2.BonusMr);
        sm1.BaseRegen = Combine(sm1.BaseRegen, sm2.BaseRegen);
        sm1.BonusRegen = Combine(sm1.BonusRegen, sm2.BonusRegen);
        sm1.BaseInboundDamageModifier = Combine(sm1.BaseInboundDamageModifier, sm2.BaseInboundDamageModifier);
        sm1.BonusInboundDamageModifier = Combine(sm1.BonusInboundDamageModifier, sm2.BonusInboundDamageModifier);
        sm1.BaseInboundHealModifier = Combine(sm1.BaseInboundHealModifier, sm2.BaseInboundHealModifier);
        sm1.BonusInboundHealModifier = Combine(sm1.BonusInboundHealModifier, sm2.BonusInboundHealModifier);
        sm1.BaseOutboundDamageModifier = Combine(sm1.BaseOutboundDamageModifier, sm2.BaseOutboundDamageModifier);
        sm1.BonusOutboundDamageModifier = Combine(sm1.BonusOutboundDamageModifier, sm2.BonusOutboundDamageModifier);
        sm1.BaseOutboundHealModifier = Combine(sm1.BaseOutboundHealModifier, sm2.BaseOutboundHealModifier);
        sm1.BonusOutboundHealModifier = Combine(sm1.BonusOutboundHealModifier, sm2.BonusOutboundHealModifier);
        sm1.BaseReflectMagical = Combine(sm1.BaseReflectMagical, sm2.BaseReflectMagical);
        sm1.BonusReflectMagical = Combine(sm1.BonusReflectMagical, sm2.BonusReflectMagical);
        sm1.BaseReflectPhysical = Combine(sm1.BaseReflectPhysical, sm2.BaseReflectPhysical);
        sm1.BonusReflectPhysical = Combine(sm1.BonusReflectPhysical, sm2.BonusReflectPhysical);
        sm1.BaseExtraGold = Combine(sm1.BaseExtraGold, sm2.BaseExtraGold);
        sm1.BonusExtraGold = Combine(sm1.BonusExtraGold, sm2.BonusExtraGold);
        sm1.BaseDodge = Combine(sm1.BaseDodge, sm2.BaseDodge);
        sm1.BonusDodge = Combine(sm1.BonusDodge, sm2.BonusDodge);
        sm1.BaseMagicDodge = Combine(sm1.BaseMagicDodge, sm2.BaseMagicDodge);
        sm1.BonusMagicDodge = Combine(sm1.BonusMagicDodge, sm2.BonusMagicDodge);
        sm1.BaseExtraXp = Combine(sm1.BaseExtraXp, sm2.BaseExtraXp);
        sm1.BonusExtraXp = Combine(sm1.BonusExtraXp, sm2.BonusExtraXp);
        sm1.BaseExtraItemFind = Combine(sm1.BaseExtraItemFind, sm2.BaseExtraItemFind);
        sm1.BonusExtraItemFind = Combine(sm1.BonusExtraItemFind, sm2.BonusExtraItemFind);
        sm1.BaseLifeSteal = Combine(sm1.BaseLifeSteal, sm2.BaseLifeSteal);
        sm1.BonusLifeSteal = Combine(sm1.BonusLifeSteal, sm2.BonusLifeSteal);
        sm1.BaseManaSteal = Combine(sm1.BaseManaSteal, sm2.BaseManaSteal);
        sm1.BonusManaSteal = Combine(sm1.BonusManaSteal, sm2.BonusManaSteal);
        sm1.BaseInboundDamageToMp = Combine(sm1.BaseInboundDamageToMp, sm2.BaseInboundDamageToMp);
        sm1.BonusInboundDamageToMp = Combine(sm1.BonusInboundDamageToMp, sm2.BonusInboundDamageToMp);
        return sm1;
    }
}

