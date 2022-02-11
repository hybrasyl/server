using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl;
using Hybrasyl.Xml;
using Xunit;

namespace HybrasylTests;

[Collection("Hybrasyl")]
public class Equipment
{
    private static HybrasylFixture Fixture;

    public Equipment(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    public static IEnumerable<object[]> XmlItems()
    {
        yield return new object[] { Fixture.TestItem, Fixture.StackableTestItem };

    }

    public static IEnumerable<object[]> StackableXmlItems()
    {
        yield return new object[] { Fixture.StackableTestItem };

    }

    public static IEnumerable<object[]> EquipmentItems()
    {
        yield return new object[] {Fixture.TestEquipment.Values};
    }

    [Fact]
    public void NewEquipmentSizeIsCorrect()
    {
        var f = new Hybrasyl.Equipment(Hybrasyl.Equipment.DefaultSize);
        Assert.Equal(Hybrasyl.Equipment.DefaultSize, f.Size);
    }

    [Fact]
    public void NewEquipmentEmptySlotsEqualsSize()
    {
        var f = new Hybrasyl.Equipment(Hybrasyl.Equipment.DefaultSize);
        Assert.Equal(Hybrasyl.Equipment.DefaultSize, f.EmptySlots);
    }

    [Fact]
    public void NewEquipmentFirstEmptySlotIsOne()
    {
        var f = new Hybrasyl.Equipment(Hybrasyl.Equipment.DefaultSize);
        Assert.Equal(1, f.FindEmptySlot());
    }

    [Fact]
    public void NewEquipmentWeightIsZero()
    {
        var f = new Hybrasyl.Equipment(Hybrasyl.Equipment.DefaultSize);
        Assert.Equal(0, f.Weight);
    }

    [Fact]
    public void NewEquipmentIsNotFull()
    {
        var f = new Hybrasyl.Equipment(Hybrasyl.Equipment.DefaultSize);
        Assert.False(f.IsFull, "new equipment should not be full");
    }

    [Fact]
    public void ClearEquipment()
    {
        Fixture.TestUser.Equipment.Clear();
        Assert.True(Fixture.TestUser.Equipment.Count == 0, "Equipment cleared but count is non-zero");
        Assert.True(Fixture.TestUser.Equipment.ToList().Count == 0,
            "Equipment cleared but enumerated count is non-zero");
        Assert.True(Fixture.TestUser.Equipment.Weight == 0, "Equipment cleared but weight is non-zero");
        Fixture.TestUser.Equipment.RecalculateWeight();
        Assert.True(Fixture.TestUser.Equipment.Weight == 0,
            "Equipment cleared but weight is non-zero after recalculation");
    }

    [Fact]
    public void EquipRestrictionCheckAbLevel()
    {
        Fixture.TestUser.Equipment.Clear();

        var item = Fixture.TestEquipment[EquipmentSlot.Armor].Clone();
        item.Properties.Restrictions = new ItemRestrictions
        {
            Level = new RestrictionsLevel
            {
                Max = 90,
                Min = 50
            }
        };
        Fixture.TestUser.Stats.Level = 50;
        var equipment = Game.World.CreateItem(item);
        Assert.True(equipment.CheckRequirements(Fixture.TestUser, out var m1),
            $"Equipment min level is 50, player level is 50, CheckRequirements failed with {m1}");
        Assert.True(Fixture.TestUser.AddEquipment(equipment, (byte) EquipmentSlot.Armor),
            "Equipment level is 50, player level is 50, AddEquipment failed");
        Assert.True(Fixture.TestUser.RemoveEquipment((byte) EquipmentSlot.Armor), "Failed to unequip equipment");
        Fixture.TestUser.Stats.Level = 49;
        Assert.False(equipment.CheckRequirements(Fixture.TestUser, out var m2),
            "Equipment min level is 50, player level is 49, CheckRequirements succeeded");
        Assert.True(m2 == Game.World.GetLocalString("item_equip_more_insight"),
            "Failure message incorrect ({m2}), expecting local string item_equip_more_insight");
        Fixture.TestUser.Stats.Level = 99;
        Assert.False(equipment.CheckRequirements(Fixture.TestUser, out var m3),
            "Equipment max level is 90, player level is 99, CheckRequirements succeeded");
        Assert.True(m3 == Game.World.GetLocalString("item_equip_less_insight"),
            $"Failure message incorrect ({m3}), expecting local string item_equip_less_insight");
    }

    [Fact]
    public void EquipRestrictionCheckClass()
    {
        Fixture.TestUser.Equipment.Clear();

        var item = Fixture.TestEquipment[EquipmentSlot.Armor].Clone();
        item.Properties.Restrictions = new ItemRestrictions
        {
            Class = Class.Monk
        };
        Fixture.TestUser.Class = Class.Peasant;
        var equipment = Game.World.CreateItem(item);
        Assert.False(equipment.CheckRequirements(Fixture.TestUser, out var m1),
            $"Equipment class is Monk, user is Peasant, CheckRequirements succeeded");
        Assert.Equal(m1, Game.World.GetLocalString("item_equip_peasant"));
        Fixture.TestUser.Class = Class.Wizard;
        Assert.False(equipment.CheckRequirements(Fixture.TestUser, out var m2),
            $"Equipment class is Monk, user is Wizard, CheckRequirements succeeded");
        Assert.Equal(m2, Game.World.GetLocalString("item_equip_wrong_class"));
        Fixture.TestUser.Class = Class.Monk;
        Assert.True(equipment.CheckRequirements(Fixture.TestUser, out var m3),
            $"Equipment class is Monk, user is Monk, CheckRequirements failed {m3}");
        Assert.True(Fixture.TestUser.AddEquipment(equipment, (byte) EquipmentSlot.Armor),
            "Equipment class is Monk, user is Monk, AddEquipment failed");
        Assert.True(Fixture.TestUser.RemoveEquipment((byte) EquipmentSlot.Armor), "Failed to unequip equipment");
    }

    [Fact]
    public void EquipRestrictionCheckWeight()
    {
        Fixture.TestUser.Equipment.Clear();
        Fixture.ResetUserStats();
        var item = Fixture.TestEquipment[EquipmentSlot.Armor].Clone();
        item.Properties.Physical.Weight = 100;
        var equipment = Game.World.CreateItem(item);
        Assert.False(equipment.CheckRequirements(Fixture.TestUser, out var m1),
            $"Equipment weight is 100, user has base stats but CheckRequirements succeeded");
        Assert.Equal(Game.World.GetLocalString("item_equip_too_heavy"), m1);
        item.Properties.Physical.Weight = 100;
        Fixture.TestUser.Stats.BaseStr = 255;
        Fixture.TestUser.Stats.Level = 99;
        Assert.True(equipment.CheckRequirements(Fixture.TestUser, out var m3),
            $"Equipment weight is 100, user is str 255 / level 99, CheckRequirements failed {m3}");
        Assert.True(Fixture.TestUser.AddEquipment(equipment, (byte) EquipmentSlot.Armor),
            "Equipment weight is 100, user is str 255 / level 99, AddEquipment failed");
        Assert.True(Fixture.TestUser.RemoveEquipment((byte) EquipmentSlot.Armor), "Failed to unequip equipment");
    }

    [Fact]
    public void EquipRestrictionShieldTwoHand()
    {
        Fixture.TestUser.Equipment.Clear();
        Fixture.ResetUserStats();
        var shield = Fixture.TestEquipment[EquipmentSlot.Shield].Clone();
        var twohand = Fixture.TestEquipment[EquipmentSlot.Weapon].Clone();
        twohand.Properties.Equipment.WeaponType = WeaponType.TwoHand;
        var shieldObj = Game.World.CreateItem(shield);
        var twohandObj = Game.World.CreateItem(twohand);
        Assert.True(
            shieldObj.CheckRequirements(Fixture.TestUser, out var m1) &&
            Fixture.TestUser.AddEquipment(shieldObj, (byte) EquipmentSlot.Shield), $"Check & Equip shield failed ({m1})");
        Assert.False(twohandObj.CheckRequirements(Fixture.TestUser, out var m2),
            "Shield equipped, equip two handed weapon,1 CheckRequirements succeeded");
        Assert.Equal(Game.World.GetLocalString("item_equip_2h_shield"), m2);

        Assert.True(Fixture.TestUser.RemoveEquipment((byte) EquipmentSlot.Shield));
        Assert.True(
            twohandObj.CheckRequirements(Fixture.TestUser, out var m3) &&
            Fixture.TestUser.AddEquipment(twohandObj, (byte) EquipmentSlot.Weapon),
            "Check & Equip 2H weapon failed");
        Assert.False(shieldObj.CheckRequirements(Fixture.TestUser, out var m4));
        Assert.Equal(Game.World.GetLocalString("item_equip_shield_2h"), m4);
    }

    [Fact]
    public void EquipRestrictionUniqueEquipped()
    {
        Fixture.TestUser.Equipment.Clear();
        var ring1 = Fixture.TestEquipment[EquipmentSlot.Ring].Clone();
        ring1.Properties.Flags = ItemFlags.UniqueEquipped;
        ring1.Name = "Unique Ring";
        var ring2 = ring1.Clone();
        var ring1Obj = Game.World.CreateItem(ring1);
        var ring2Obj = Game.World.CreateItem(ring2);
        Assert.True(
            ring1Obj.CheckRequirements(Fixture.TestUser, out var m1) &&
            Fixture.TestUser.AddEquipment(ring1Obj, (byte) EquipmentSlot.LeftHand), $"Equip first ring failed ({m1})");
        Assert.True(Fixture.TestUser.Equipment.LRing != null);
        Assert.False(ring2Obj.CheckRequirements(Fixture.TestUser, out var m2),
            "Ring 1 equipped, Equipping duplicate unique-equipped item, CheckRequirements succeeded");
        Assert.Equal(Game.World.GetLocalString("item_equip_unique_equipped"), m2);
    }

    [Fact]
    public void EquipRestrictionSlotRestriction()
    {
        Fixture.TestUser.Equipment.Clear();
        var ring = Fixture.TestEquipment[EquipmentSlot.Ring].Clone();
        ring.Name = "I Prohibit Armor";
        var armor = Fixture.TestEquipment[EquipmentSlot.Armor].Clone();
        armor.Name = "I Prohibit Rings";
        ring.Properties.Restrictions = new ItemRestrictions
        {
            SlotRestrictions = new List<SlotRestriction>
            {
                new()
                {
                    Message = "item_equip_slot_restriction", 
                    Slot = EquipmentSlot.Armor,
                    Type = SlotRestrictionType.ItemProhibited
                }
            }
        };
        armor.Properties.Restrictions = new ItemRestrictions
        {
            SlotRestrictions = new List<SlotRestriction>
            {
                new()
                {
                    Message = "item_equip_slot_restriction",
                    Slot = EquipmentSlot.Ring,
                    Type = SlotRestrictionType.ItemProhibited
                },
            }
        };


        var ringObj = Game.World.CreateItem(ring);
        var armorObj = Game.World.CreateItem(armor);

        Assert.True(
            ringObj.CheckRequirements(Fixture.TestUser, out var m1) &&
            Fixture.TestUser.AddEquipment(ringObj, (byte)EquipmentSlot.LeftHand), $"Equip armor-prohibiting ring failed ({m1})");
        Assert.True(Fixture.TestUser.Equipment.LRing != null, "Ring is missing");
        Assert.False(armorObj.CheckRequirements(Fixture.TestUser, out var m2),
            "Ring equipped, Equipping slot-prohibited armor, CheckRequirements succeeded");
        Assert.Equal(Game.World.GetLocalString("item_equip_slot_restriction"), m2);

        // Try the reverse now
        Assert.True(
            ringObj.CheckRequirements(Fixture.TestUser, out var m3) &&
            Fixture.TestUser.AddEquipment(armorObj, (byte)EquipmentSlot.Armor), $"Equip ring-prohibiting armor failed ({m3})");
        Assert.True(Fixture.TestUser.Equipment.LRing != null, "Armor is missing");
        Assert.False(ringObj.CheckRequirements(Fixture.TestUser, out var m4),
            "Armor equipped, Equipping slot-prohibited ring, CheckRequirements succeeded");
        Assert.Equal(Game.World.GetLocalString("item_equip_slot_restriction"), m4);

    }

    [Fact]
    public void EquipRestrictionCastable()
    {}

    [Fact]
    public void EquipRestrictionMastership()
    {}

}