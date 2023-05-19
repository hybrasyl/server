using Hybrasyl;
using Hybrasyl.Enums;
using Hybrasyl.Xml.Objects;
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
    public void ItemSlotRequirement()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Class = Class.Monk;
        Fixture.TestUser.Gender = Gender.Male;
        var error = string.Empty;
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Test Armor Require Boots", out Item armorItem));
        Assert.NotNull(armorItem);
        Fixture.TestUser.Teleport("XUnit Test Realm", 10, 10);
        var armor = Fixture.TestUser.World.CreateItem(armorItem);
        // Check requirement: equip armor with slot restriction (requires boots), should fail
        Assert.False(armor.CheckRequirements(Fixture.TestUser, out error));
        var item2 = Game.World.WorldData.TryGetValueByIndex("Test Boots", out Item bootItem);
        Assert.NotNull(item2);
        // Now equip boots
        var boots = Fixture.TestUser.World.CreateItem(bootItem);
        Assert.True(boots.CheckRequirements(Fixture.TestUser, out error));
        Assert.True(Fixture.TestUser.AddEquipment(boots, boots.EquipmentSlot));
        // Now we should be able to equip the armor
        Assert.True(armor.CheckRequirements(Fixture.TestUser, out error));
        Assert.True(Fixture.TestUser.AddEquipment(armor, armor.EquipmentSlot));
        // Both boots and armor are equipped
        Assert.NotNull(Fixture.TestUser.Equipment.Armor);
        Assert.NotNull(Fixture.TestUser.Equipment.Boots);
        // Removing the boots should not be possible (we should get a system message)
        Assert.False(Fixture.TestUser.RemoveEquipment((byte) ItemSlots.Foot));
        Assert.Equal("Other equipment must be removed first.", Fixture.TestUser.LastSystemMessage);
        Assert.NotNull(Fixture.TestUser.Equipment.Armor);
        Assert.NotNull(Fixture.TestUser.Equipment.Boots);
    }

    [Fact]
    public void ItemSlotRestrictions()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Class = Class.Monk;
        Fixture.TestUser.Gender = Gender.Male;
        var error = string.Empty;
        var item = Game.World.WorldData.TryGetValueByIndex("Test Armor 1 Male Monk", out Item armorItem);
        Assert.NotNull(item);
        Fixture.TestUser.Teleport("XUnit Test Realm", 10, 10);
        var armor = Fixture.TestUser.World.CreateItem(armorItem);
        // Equip armor with slot restriction
        Assert.True(armor.CheckRequirements(Fixture.TestUser, out error));
        Assert.True(Fixture.TestUser.AddEquipment(armor, armor.EquipmentSlot, false));
        var item2 = Game.World.WorldData.TryGetValueByIndex("Test Boots", out Item bootItem);
        Assert.NotNull(item2);
        var boots = Fixture.TestUser.World.CreateItem(bootItem);
        // Above armor has a slot restriction on boot usage, so this should fail
        Assert.False(boots.CheckRequirements(Fixture.TestUser, out error));
        Assert.Equal("monk_equip_fail", error);
        // Now try the other way - put the boots on first and then equip the armor
        Assert.True(Fixture.TestUser.RemoveEquipment((byte) ItemSlots.Armor));
        Assert.Null(Fixture.TestUser.Equipment.Armor);
        Assert.Null(Fixture.TestUser.Equipment.Boots);
        Assert.True(Fixture.TestUser.AddEquipment(boots, boots.EquipmentSlot, false));
        Assert.False(armor.CheckRequirements(Fixture.TestUser, out error));
        Assert.Equal("monk_equip_fail", error);
    }

    [Fact]
    public void UseItemBaseStats()
    {
        Fixture.ResetUserStats();

        var ring = Fixture.TestEquipment[EquipmentSlot.Ring].Clone<Item>();
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

        Assert.True(Fixture.TestUser.Stats.BaseHp == 100,
            $"Hp: after item usage, should be 100, is {Fixture.TestUser.Stats.BaseHp}");
        Assert.True(Fixture.TestUser.Stats.BaseMp == 100,
            $"Mp: after item usage, should be 100, is {Fixture.TestUser.Stats.BaseMp}");
        Assert.True(Fixture.TestUser.Stats.BaseStr == 53,
            $"Str: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseStr}");
        Assert.True(Fixture.TestUser.Stats.BaseCon == 53,
            $"Con: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseCon}");
        Assert.True(Fixture.TestUser.Stats.BaseDex == 53,
            $"Dex: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseDex}");
        Assert.True(Fixture.TestUser.Stats.BaseInt == 53,
            $"Int: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseInt}");
        Assert.True(Fixture.TestUser.Stats.BaseWis == 53,
            $"Wis: after item usage, should be 53, is {Fixture.TestUser.Stats.BaseWis}");
        Assert.True(Fixture.TestUser.Stats.BaseCrit == 10,
            $"Crit: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseCrit}");
        Assert.True(Fixture.TestUser.Stats.BaseMagicCrit == 10,
            $"MagicCrit: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseMagicCrit}");
        Assert.True(Fixture.TestUser.Stats.BaseDmg == 10,
            $"Dmg: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseDmg}");
        Assert.True(Fixture.TestUser.Stats.BaseHit == 10,
            $"Hit: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseHit}");
        Assert.True(Fixture.TestUser.Stats.BaseAc == 110,
            $"Ac: after item usage, should be 110, is {Fixture.TestUser.Stats.BaseAc}");
        Assert.True(Fixture.TestUser.Stats.BaseMr == 10,
            $"Mr: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseMr}");
        Assert.True(Fixture.TestUser.Stats.BaseRegen == 10,
            $"Regen: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseRegen}");
        Assert.True(Fixture.TestUser.Stats.BaseInboundDamageModifier == 10,
            $"InboundDamageModifier: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseInboundDamageModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseInboundHealModifier == 10,
            $"InboundHealModifier: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseInboundHealModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseOutboundDamageModifier == 10,
            $"OutboundDamageModifier: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseOutboundDamageModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseOutboundHealModifier == 10,
            $"OutboundHealModifier: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseOutboundHealModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseReflectMagical == 10,
            $"ReflectMagical: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseReflectMagical}");
        Assert.True(Fixture.TestUser.Stats.BaseReflectPhysical == 10,
            $"ReflectPhysical: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseReflectPhysical}");
        Assert.True(Fixture.TestUser.Stats.BaseExtraGold == 10,
            $"ExtraGold: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseExtraGold}");
        Assert.True(Fixture.TestUser.Stats.BaseDodge == 10,
            $"Dodge: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseDodge}");
        Assert.True(Fixture.TestUser.Stats.BaseMagicDodge == 10,
            $"MagicDodge: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseMagicDodge}");
        Assert.True(Fixture.TestUser.Stats.BaseExtraXp == 10,
            $"ExtraXp: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseExtraXp}");
        Assert.True(Fixture.TestUser.Stats.BaseExtraItemFind == 10,
            $"ExtraItemFind: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseExtraItemFind}");
        Assert.True(Fixture.TestUser.Stats.BaseLifeSteal == 10,
            $"LifeSteal: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseLifeSteal}");
        Assert.True(Fixture.TestUser.Stats.BaseManaSteal == 10,
            $"ManaSteal: after item usage, should be 10, is {Fixture.TestUser.Stats.BaseManaSteal}");
    }
}