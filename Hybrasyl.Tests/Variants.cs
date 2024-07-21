// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System.Linq;
using Hybrasyl.Xml.Objects;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Variant
{
    private static HybrasylFixture Fixture;

    public Variant(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void CreateVariant()
    {
        Assert.True(Game.World.WorldData.TryGetValueByIndex<Item>("Abundance Variant All Belt", out var variant));
        Assert.True(Game.World.WorldData.TryGetValueByIndex<Item>("Variant All Belt", out var baseItem));
        Assert.True(Game.World.WorldData.TryGetValue<VariantGroup>("enchant1", out var variantGroup));

        if (baseItem.Properties.StatModifiers == null)
            baseItem.Properties.StatModifiers = new StatModifiers();

        var variantModifier = variantGroup.Variant.First().Properties.StatModifiers;

        Assert.NotNull(variantModifier);


        Assert.True(
            variant.Properties.StatModifiers.BonusHp ==
            baseItem.Properties.StatModifiers.BonusHp + variantModifier.BonusHp,
            $"Variant: bonus is {variantModifier.BonusHp}, but variant property is {variant.Properties.StatModifiers.BonusHp}");
        Assert.True(
            variant.Properties.StatModifiers.BonusMp ==
            baseItem.Properties.StatModifiers.BonusMp + variantModifier.BonusMp,
            $"Variant: bonus is {variantModifier.BonusMp}, but variant property is {variant.Properties.StatModifiers.BonusMp}");
        Assert.True(
            variant.Properties.StatModifiers.BonusStr ==
            baseItem.Properties.StatModifiers.BonusStr + variantModifier.BonusStr,
            $"Variant: bonus is {variantModifier.BonusStr}, but variant property is {variant.Properties.StatModifiers.BonusStr}");
        Assert.True(
            variant.Properties.StatModifiers.BonusCon ==
            baseItem.Properties.StatModifiers.BonusCon + variantModifier.BonusCon,
            $"Variant: bonus is {variantModifier.BonusCon}, but variant property is {variant.Properties.StatModifiers.BonusCon}");
        Assert.True(
            variant.Properties.StatModifiers.BonusDex ==
            baseItem.Properties.StatModifiers.BonusDex + variantModifier.BonusDex,
            $"Variant: bonus is {variantModifier.BonusDex}, but variant property is {variant.Properties.StatModifiers.BonusDex}");
        Assert.True(
            variant.Properties.StatModifiers.BonusInt ==
            baseItem.Properties.StatModifiers.BonusInt + variantModifier.BonusInt,
            $"Variant: bonus is {variantModifier.BonusInt}, but variant property is {variant.Properties.StatModifiers.BonusInt}");
        Assert.True(
            variant.Properties.StatModifiers.BonusWis ==
            baseItem.Properties.StatModifiers.BonusWis + variantModifier.BonusWis,
            $"Variant: bonus is {variantModifier.BonusWis}, but variant property is {variant.Properties.StatModifiers.BonusWis}");
        Assert.True(
            variant.Properties.StatModifiers.BonusCrit ==
            baseItem.Properties.StatModifiers.BonusCrit + variantModifier.BonusCrit,
            $"Variant: bonus is {variantModifier.BonusCrit}, but variant property is {variant.Properties.StatModifiers.BonusCrit}");
        Assert.True(
            variant.Properties.StatModifiers.BonusMagicCrit ==
            baseItem.Properties.StatModifiers.BonusMagicCrit + variantModifier.BonusMagicCrit,
            $"Variant: bonus is {variantModifier.BonusMagicCrit}, but variant property is {variant.Properties.StatModifiers.BonusMagicCrit}");
        Assert.True(
            variant.Properties.StatModifiers.BonusDmg ==
            baseItem.Properties.StatModifiers.BonusDmg + variantModifier.BonusDmg,
            $"Variant: bonus is {variantModifier.BonusDmg}, but variant property is {variant.Properties.StatModifiers.BonusDmg}");
        Assert.True(
            variant.Properties.StatModifiers.BonusHit ==
            baseItem.Properties.StatModifiers.BonusHit + variantModifier.BonusHit,
            $"Variant: bonus is {variantModifier.BonusHit}, but variant property is {variant.Properties.StatModifiers.BonusHit}");
        Assert.True(
            variant.Properties.StatModifiers.BonusAc ==
            baseItem.Properties.StatModifiers.BonusAc + variantModifier.BonusAc,
            $"Variant: bonus is {variantModifier.BonusAc}, but variant property is {variant.Properties.StatModifiers.BonusAc}");
        Assert.True(
            variant.Properties.StatModifiers.BonusMr ==
            baseItem.Properties.StatModifiers.BonusMr + variantModifier.BonusMr,
            $"Variant: bonus is {variantModifier.BonusMr}, but variant property is {variant.Properties.StatModifiers.BonusMr}");
        Assert.True(
            variant.Properties.StatModifiers.BonusRegen ==
            baseItem.Properties.StatModifiers.BonusRegen + variantModifier.BonusRegen,
            $"Variant: bonus is {variantModifier.BonusRegen}, but variant property is {variant.Properties.StatModifiers.BonusRegen}");
        Assert.True(
            variant.Properties.StatModifiers.BonusInboundDamageModifier ==
            baseItem.Properties.StatModifiers.BonusInboundDamageModifier + variantModifier.BonusInboundDamageModifier,
            $"Variant: bonus is {variantModifier.BonusInboundDamageModifier}, but variant property is {variant.Properties.StatModifiers.BonusInboundDamageModifier}");
        Assert.True(
            variant.Properties.StatModifiers.BonusInboundHealModifier ==
            baseItem.Properties.StatModifiers.BonusInboundHealModifier + variantModifier.BonusInboundHealModifier,
            $"Variant: bonus is {variantModifier.BonusInboundHealModifier}, but variant property is {variant.Properties.StatModifiers.BonusInboundHealModifier}");
        Assert.True(
            variant.Properties.StatModifiers.BonusOutboundDamageModifier ==
            baseItem.Properties.StatModifiers.BonusOutboundDamageModifier + variantModifier.BonusOutboundDamageModifier,
            $"Variant: bonus is {variantModifier.BonusOutboundDamageModifier}, but variant property is {variant.Properties.StatModifiers.BonusOutboundDamageModifier}");
        Assert.True(
            variant.Properties.StatModifiers.BonusOutboundHealModifier ==
            baseItem.Properties.StatModifiers.BonusOutboundHealModifier + variantModifier.BonusOutboundHealModifier,
            $"Variant: bonus is {variantModifier.BonusOutboundHealModifier}, but variant property is {variant.Properties.StatModifiers.BonusOutboundHealModifier}");
        Assert.True(
            variant.Properties.StatModifiers.BonusReflectMagical ==
            baseItem.Properties.StatModifiers.BonusReflectMagical + variantModifier.BonusReflectMagical,
            $"Variant: bonus is {variantModifier.BonusReflectMagical}, but variant property is {variant.Properties.StatModifiers.BonusReflectMagical}");
        Assert.True(
            variant.Properties.StatModifiers.BonusReflectPhysical ==
            baseItem.Properties.StatModifiers.BonusReflectPhysical + variantModifier.BonusReflectPhysical,
            $"Variant: bonus is {variantModifier.BonusReflectPhysical}, but variant property is {variant.Properties.StatModifiers.BonusReflectPhysical}");
        Assert.True(
            variant.Properties.StatModifiers.BonusExtraGold ==
            baseItem.Properties.StatModifiers.BonusExtraGold + variantModifier.BonusExtraGold,
            $"Variant: bonus is {variantModifier.BonusExtraGold}, but variant property is {variant.Properties.StatModifiers.BonusExtraGold}");
        Assert.True(
            variant.Properties.StatModifiers.BonusDodge ==
            baseItem.Properties.StatModifiers.BonusDodge + variantModifier.BonusDodge,
            $"Variant: bonus is {variantModifier.BonusDodge}, but variant property is {variant.Properties.StatModifiers.BonusDodge}");
        Assert.True(
            variant.Properties.StatModifiers.BonusMagicDodge == baseItem.Properties.StatModifiers.BonusMagicDodge +
            variantModifier.BonusMagicDodge,
            $"Variant: bonus is {variantModifier.BonusMagicDodge}, but variant property is {variant.Properties.StatModifiers.BonusMagicDodge}");
        Assert.True(
            variant.Properties.StatModifiers.BonusExtraXp ==
            baseItem.Properties.StatModifiers.BonusExtraXp + variantModifier.BonusExtraXp,
            $"Variant: bonus is {variantModifier.BonusExtraXp}, but variant property is {variant.Properties.StatModifiers.BonusExtraXp}");
        Assert.True(
            variant.Properties.StatModifiers.BonusExtraItemFind ==
            baseItem.Properties.StatModifiers.BonusExtraItemFind + variantModifier.BonusExtraItemFind,
            $"Variant: bonus is {variantModifier.BonusExtraItemFind}, but variant property is {variant.Properties.StatModifiers.BonusExtraItemFind}");
        Assert.True(
            variant.Properties.StatModifiers.BonusLifeSteal ==
            baseItem.Properties.StatModifiers.BonusLifeSteal + variantModifier.BonusLifeSteal,
            $"Variant: bonus is {variantModifier.BonusLifeSteal}, but variant property is {variant.Properties.StatModifiers.BonusLifeSteal}");
        Assert.True(
            variant.Properties.StatModifiers.BonusManaSteal ==
            baseItem.Properties.StatModifiers.BonusManaSteal + variantModifier.BonusManaSteal,
            $"Variant: bonus is {variantModifier.BonusManaSteal}, but variant property is {variant.Properties.StatModifiers.BonusManaSteal}");
        Assert.True(
            variant.Properties.StatModifiers.BonusInboundDamageToMp ==
            baseItem.Properties.StatModifiers.BonusInboundDamageToMp + variantModifier.BonusInboundDamageToMp,
            $"Variant: bonus is {variantModifier.BonusInboundDamageToMp}, but variant property is {variant.Properties.StatModifiers.BonusInboundDamageToMp}");
    }

    //[Fact]
    //public void EquipVariant()
    //{
    //    Fixture.ResetTestUserStats();
    //    Fixture.TestUser.Equipment.Clear();
    //    Assert.True(Game.World.WorldData.TryGetValueByIndex<Item>("Abundance Variant All Belt", out var variant));
    //    var itemObj = Game.World.CreateItem(variant);
    //    Assert.True(Fixture.TestUser.AddEquipment(itemObj, (byte)ItemSlots.Foot), "Equipping variant failed");
    //    Assert.True(Game.World.WorldData.TryGetValue<VariantGroup>("enchant1", out var variantGroup));

    //    Assert.True(Fixture.TestUser.Stats.MaximumHp ==
    //                Fixture.TestUser.Stats.BaseHp + long.Parse(variant.Properties.StatModifiers.BonusHp));
    //    Assert.True(Fixture.TestUser.Stats.MaximumMp ==
    //                Fixture.TestUser.Stats.BaseMp + long.Parse(variant.Properties.StatModifiers.BonusMp));
    //    Assert.True(Fixture.TestUser.Stats.Str ==
    //                Fixture.TestUser.Stats.BaseStr + long.Parse(variant.Properties.StatModifiers.BonusStr));
    //    Assert.True(Fixture.TestUser.Stats.Con ==
    //                Fixture.TestUser.Stats.BaseCon + long.Parse(variant.Properties.StatModifiers.BonusCon));
    //    Assert.True(Fixture.TestUser.Stats.Dex ==
    //                Fixture.TestUser.Stats.BaseDex + long.Parse(variant.Properties.StatModifiers.BonusDex));
    //    Assert.True(Fixture.TestUser.Stats.Int ==
    //                Fixture.TestUser.Stats.BaseInt + long.Parse(variant.Properties.StatModifiers.BonusInt));
    //    Assert.True(Fixture.TestUser.Stats.Wis ==
    //                Fixture.TestUser.Stats.BaseWis + long.Parse(variant.Properties.StatModifiers.BonusWis));
    //    Assert.True(Fixture.TestUser.Stats.Crit ==
    //                Fixture.TestUser.Stats.BaseCrit + long.Parse(variant.Properties.StatModifiers.BonusCrit));
    //    Assert.True(Fixture.TestUser.Stats.MagicCrit == Fixture.TestUser.Stats.BaseMagicCrit +
    //        long.Parse(variant.Properties.StatModifiers.BonusMagicCrit));
    //    Assert.True(Fixture.TestUser.Stats.Dmg ==
    //                Fixture.TestUser.Stats.BaseDmg + long.Parse(variant.Properties.StatModifiers.BonusDmg));
    //    Assert.True(Fixture.TestUser.Stats.Hit ==
    //                Fixture.TestUser.Stats.BaseHit + long.Parse(variant.Properties.StatModifiers.BonusHit));
    //    //Assert.True(Fixture.TestUser.Stats.Ac == Fixture.TestUser.Stats.BaseAc + long.Parse(variant.Properties.StatModifiers.BonusAc));
    //    Assert.True(Fixture.TestUser.Stats.Mr ==
    //                Fixture.TestUser.Stats.BaseMr + long.Parse(variant.Properties.StatModifiers.BonusMr));
    //    Assert.True(Fixture.TestUser.Stats.Regen ==
    //                Fixture.TestUser.Stats.BaseRegen + long.Parse(variant.Properties.StatModifiers.BonusRegen));
    //    Assert.True(Fixture.TestUser.Stats.InboundDamageModifier == Fixture.TestUser.Stats.BaseInboundDamageModifier +
    //        long.Parse(variant.Properties.StatModifiers.BonusInboundDamageModifier));
    //    Assert.True(Fixture.TestUser.Stats.InboundHealModifier == Fixture.TestUser.Stats.BaseInboundHealModifier +
    //        long.Parse(variant.Properties.StatModifiers.BonusInboundHealModifier));
    //    Assert.True(Fixture.TestUser.Stats.OutboundDamageModifier == Fixture.TestUser.Stats.BaseOutboundDamageModifier +
    //        long.Parse(variant.Properties.StatModifiers.BonusOutboundDamageModifier));
    //    Assert.True(Fixture.TestUser.Stats.OutboundHealModifier == Fixture.TestUser.Stats.BaseOutboundHealModifier +
    //        long.Parse(variant.Properties.StatModifiers.BonusOutboundHealModifier));
    //    Assert.True(Fixture.TestUser.Stats.ReflectMagical == Fixture.TestUser.Stats.BaseReflectMagical +
    //        long.Parse(variant.Properties.StatModifiers.BonusReflectMagical));
    //    Assert.True(Fixture.TestUser.Stats.ReflectPhysical == Fixture.TestUser.Stats.BaseReflectPhysical +
    //        long.Parse(variant.Properties.StatModifiers.BonusReflectPhysical));
    //    Assert.True(Fixture.TestUser.Stats.ExtraGold == Fixture.TestUser.Stats.BaseExtraGold +
    //        long.Parse(variant.Properties.StatModifiers.BonusExtraGold));
    //    Assert.True(Fixture.TestUser.Stats.Dodge ==
    //                Fixture.TestUser.Stats.BaseDodge + long.Parse(variant.Properties.StatModifiers.BonusDodge));
    //    Assert.True(Fixture.TestUser.Stats.MagicDodge == Fixture.TestUser.Stats.BaseMagicDodge +
    //        long.Parse(variant.Properties.StatModifiers.BonusMagicDodge));
    //    Assert.True(Fixture.TestUser.Stats.ExtraXp == Fixture.TestUser.Stats.BaseExtraXp +
    //        long.Parse(variant.Properties.StatModifiers.BonusExtraXp));
    //    Assert.True(Fixture.TestUser.Stats.ExtraItemFind == Fixture.TestUser.Stats.BaseExtraItemFind +
    //        long.Parse(variant.Properties.StatModifiers.BonusExtraItemFind));
    //    Assert.True(Fixture.TestUser.Stats.LifeSteal == Fixture.TestUser.Stats.BaseLifeSteal +
    //        long.Parse(variant.Properties.StatModifiers.BonusLifeSteal));
    //    Assert.True(Fixture.TestUser.Stats.ManaSteal == Fixture.TestUser.Stats.BaseManaSteal +
    //        long.Parse(variant.Properties.StatModifiers.BonusManaSteal));
    //    Assert.True(Fixture.TestUser.Stats.InboundDamageToMp == Fixture.TestUser.Stats.BaseInboundDamageToMp +
    //        long.Parse(variant.Properties.StatModifiers.BonusInboundDamageToMp));
    //}
}