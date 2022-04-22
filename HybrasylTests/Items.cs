using System;
using Hybrasyl;
using Hybrasyl.Xml;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HybrasylTests;

[Collection("Hybrasyl")]
public class Items
{
    private static HybrasylFixture Fixture;
    public Items(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void ItemSlotRestrictions()
    {

    }
    [Fact]
    public void UseItemBaseStats()
    {
        Fixture.TestUser.Equipment.Clear();
        Fixture.ResetUserStats();

        var ring = Fixture.TestEquipment[EquipmentSlot.Ring].Clone();
        ring.Name = "I Give Permanent Bonuses";

        ring.Properties.StatModifiers = new StatModifiers
        {
            BaseHp = "50",
            BaseMp = "50",
            BaseStr = "50",
            BaseCon = "50",
            BaseDex = "50",
            BaseInt = "50",
            BaseWis = "50",
            BaseCrit = "10",
            BaseMagicCrit = "10",
            BaseDmg = "10",
            BaseHit = "10",
            BaseAc = "10",
            BaseMr = "10",
            BaseRegen = "10",
            BaseInboundDamageModifier = "10",
            BaseInboundHealModifier = "10",
            BaseOutboundDamageModifier = "10",
            BaseOutboundHealModifier = "10",
            BaseReflectMagical = "10",
            BaseReflectPhysical = "10",
            BaseExtraGold = "10",
            BaseDodge = "10",
            BaseMagicDodge = "10",
            BaseExtraXp = "10",
            BaseExtraItemFind = "10",
            BaseLifeSteal = "10",
            BaseManaSteal = "10",
            BaseInboundDamageToMp = "10"
        };
        ring.Properties.Flags = ItemFlags.Consumable;

        var ringObj = Game.World.CreateItem(ring);
        ringObj.Invoke(Fixture.TestUser);

        Assert.True(Fixture.TestUser.Stats.BaseHp == 100, $"Hp: after item usage, should be 100, is {Fixture.TestUser.Stats.BaseHp}");
        Assert.True(Fixture.TestUser.Stats.BaseMp == 100, $"Mp: after item usage, should be 100, is {Fixture.TestUser.Stats.BaseMp}");
        Assert.True(Fixture.TestUser.Stats.BaseStr == 53, $"Str: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseStr}");
        Assert.True(Fixture.TestUser.Stats.BaseCon == 53, $"Con: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseCon}");
        Assert.True(Fixture.TestUser.Stats.BaseDex == 53, $"Dex: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseDex}");
        Assert.True(Fixture.TestUser.Stats.BaseInt == 53, $"Int: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseInt}");
        Assert.True(Fixture.TestUser.Stats.BaseWis == 53, $"Wis: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseWis}");
        Assert.True(Fixture.TestUser.Stats.BaseCrit == 10, $"Crit: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseCrit}");
        Assert.True(Fixture.TestUser.Stats.BaseMagicCrit == 10, $"MagicCrit: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseMagicCrit}");
        Assert.True(Fixture.TestUser.Stats.BaseDmg == 10, $"Dmg: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseDmg}");
        Assert.True(Fixture.TestUser.Stats.BaseHit == 10, $"Hit: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseHit}");
        Assert.True(Fixture.TestUser.Stats.BaseAc == 110, $"Ac: after item usage, should be 110, is {Fixture.TestUser.Stats.BaseAc}");
        Assert.True(Fixture.TestUser.Stats.BaseMr == 10, $"Mr: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseMr}");
        Assert.True(Fixture.TestUser.Stats.BaseRegen == 10, $"Regen: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseRegen}");
        Assert.True(Fixture.TestUser.Stats.BaseInboundDamageModifier == 10, $"InboundDamageModifier: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseInboundDamageModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseInboundHealModifier == 10, $"InboundHealModifier: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseInboundHealModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseOutboundDamageModifier == 10, $"OutboundDamageModifier: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseOutboundDamageModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseOutboundHealModifier == 10, $"OutboundHealModifier: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseOutboundHealModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseReflectMagical == 10, $"ReflectMagical: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseReflectMagical}");
        Assert.True(Fixture.TestUser.Stats.BaseReflectPhysical == 10, $"ReflectPhysical: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseReflectPhysical}");
        Assert.True(Fixture.TestUser.Stats.BaseExtraGold == 10, $"ExtraGold: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseExtraGold}");
        Assert.True(Fixture.TestUser.Stats.BaseDodge == 10, $"Dodge: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseDodge}");
        Assert.True(Fixture.TestUser.Stats.BaseMagicDodge == 10, $"MagicDodge: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseMagicDodge}");
        Assert.True(Fixture.TestUser.Stats.BaseExtraXp == 10, $"ExtraXp: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseExtraXp}");
        Assert.True(Fixture.TestUser.Stats.BaseExtraItemFind == 10, $"ExtraItemFind: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseExtraItemFind}");
        Assert.True(Fixture.TestUser.Stats.BaseLifeSteal == 10, $"LifeSteal: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseLifeSteal}");
        Assert.True(Fixture.TestUser.Stats.BaseManaSteal == 10, $"ManaSteal: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseManaSteal}");
    }
}

