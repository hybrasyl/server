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
public class CastableRestrictions
{
    private static HybrasylFixture Fixture;

    public CastableRestrictions(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private Castable GetCastable(string name)
    {
        var castable = Game.World.WorldData.GetByIndex<Castable>(name);
        Assert.NotNull(castable);
        return castable;
    }

    private void GiveItem(string name)
    {
        var i = Game.World.WorldData.GetByIndex<Item>(name);
        Assert.NotNull(i);
        var io = Game.World.CreateItem(i);
        Assert.NotNull(io);
        Assert.True(Fixture.TestUser.AddItem(io));
    }

    private void GiveEquipment(string name)
    {
        var i = Game.World.WorldData.GetByIndex<Item>(name);
        Assert.NotNull(i);
        var io = Game.World.CreateItem(i);
        Assert.NotNull(io);
        Assert.True(Fixture.TestUser.AddEquipment(io, io.EquipmentSlot));
    }

    [Fact]
    public void RequireItem()
    {
        // <Item RestrictionType="InInventory">Slice of Ham</Item> <!-- Must have slice of ham in inventory-->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireItem");
        // Test requirement missing
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_item", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        GiveItem(castable.Restrictions.First().Value);
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void RequireInventory()
    {
        // <Item RestrictionType="InInventory"/> <!-- Nonsensical - return true if inventory is not empty -->

        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireInventory");

        // Test requirement missing
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_inventory", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        GiveItem("fithfath deum");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void ForbidItem()
    {
        // <Item RestrictionType="NotInInventory">Slice of Ham</Item> <!-- Must not have slice of ham in inventory-->

        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidItem");

        // Test requirement missing
        GiveItem("fithfath deum");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_item", Fixture.TestUser.LastSystemMessage);
        GiveItem("nochdaidh deum");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_item", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Inventory.Clear();
        Assert.True(Fixture.TestUser.UseCastable(castable));
        GiveItem("nochdaidh deum");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void ForbidInventory()
    {
        // <Item RestrictionType="NotInInventory"/> <!-- Nonsensical - return true if inventory is empty -->

        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidInventory");

        // Test requirement missing
        GiveItem("fithfath deum");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_inventory", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Inventory.Clear();
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void RequireWeaponTypeAny()
    {
        // <Item Slot="Weapon" WeaponType="Claw" RestrictionType="Equipped"/> <!-- Claw must be equipped -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireWeaponTypeAny");

        // Test requirement missing / incorrect
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_weapontype_any", Fixture.TestUser.LastSystemMessage);
        GiveEquipment("Smol Sword");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_weapontype_any", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Testinium Claw");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void RequireWeaponAny()
    {
        // <Item Slot="Weapon" RestrictionType="Equipped"/> <!-- Any weapon must be equipped (None is default for WeaponType field) -->

        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireWeaponAny");

        // Test requirement missing
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_weapon_any", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        GiveEquipment("Testinium Claw");
        Assert.True(Fixture.TestUser.UseCastable(castable));
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Smol Sword");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void RequireWeaponType()
    {
        // <Item Slot="Weapon" WeaponType="Claw" RestrictionType="Equipped">Ham Slicer</Item> <!-- A claw weapon named Ham Slicer must be equipped -->

        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireWeaponType");

        // Test requirement missing
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_weapontype", Fixture.TestUser.LastSystemMessage);
        GiveEquipment("Smol Sword");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_weapontype", Fixture.TestUser.LastSystemMessage);
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Testinium Claw2");
        Assert.False(Fixture.TestUser.UseCastable(castable));

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Testinium Claw");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void RequireEquippedAny()
    {
        // <Item Slot="None" RestrictionType="Equipped"/> <!-- Somewhat nonsensical, we interpret as "player has anything equipped" -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireEquippedAny");

        // Test requirement missing
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_equipped_any", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        GiveEquipment("Testinium Claw");
        Assert.True(Fixture.TestUser.UseCastable(castable));
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Testinium Necklace");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestRequireEquipped()
    {
        //<Item Slot="None" RestrictionType="Equipped">Ham Slapper</Item> <!-- Player has any item equipped named "Ham Slapper" -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireEquipped");

        // Test requirement missing
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_equipped", Fixture.TestUser.LastSystemMessage);
        GiveEquipment("Testinium Foot");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_equipped", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        GiveEquipment("Testinium Necklace");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestRequireSlot()
    {
        // <Item Slot="Foot" RestrictionType="Equipped">Ham Boots</Item> <!-- Player must have Ham Boots equipped in Foot slot -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireSlot");

        // Test requirement missing
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_slot", Fixture.TestUser.LastSystemMessage);
        GiveEquipment("Testinium Necklace");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_slot", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        GiveEquipment("Testinium Foot");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestRequireSlotAny()
    {
        // <Item Slot="Foot" RestrictionType="Equipped"> <!-- Player must have something equipped in Foot slot -->    
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireSlotAny");

        // Test requirement missing
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_slot_any", Fixture.TestUser.LastSystemMessage);
        GiveEquipment("Testinium Claw");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_slot_any", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        GiveEquipment("Testinium Foot");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestForbidWeaponAny()
    {
        // <Item Slot="Weapon" RestrictionType="NotEquipped"/> <!-- No weapon must be equipped (None is default for WeaponType field) -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidWeaponAny");

        // Test requirement missing
        GiveEquipment("Testinium Claw");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_weapon_any", Fixture.TestUser.LastSystemMessage);
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Smol Sword");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_weapon_any", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Testinium Foot");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestForbidWeaponTypeAny()
    {
        // <Item Slot="Weapon" WeaponType="Claw" RestrictionType="NotEquipped"/> <!-- Claw must not be equipped -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidWeaponTypeAny");

        // Test requirement missing
        GiveEquipment("Testinium Claw");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_weapontype_any", Fixture.TestUser.LastSystemMessage);
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Testinium Claw2");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_weapontype_any", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Smol Sword");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestForbidWeaponType()
    {
        // <Item Slot="Weapon" WeaponType="Claw" RestrictionType="NotEquipped">Ham Slicer</Item> <!-- A claw named Ham Slicer must not be equipped -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidWeaponType");

        // Test requirement missing
        GiveEquipment("Testinium Claw");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_weapontype", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Testinium Claw2");
        Assert.True(Fixture.TestUser.UseCastable(castable));
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Smol Sword");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestForbidEquippedAny()
    {
        // <Item Slot="None" RestrictionType="NotEquipped"/> <!-- Somewhat nonsensical, we interpret as "player has nothing equipped" -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidEquippedAny");

        // Test requirement missing
        GiveEquipment("Testinium Claw");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_equipped_any", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        Assert.True(Fixture.TestUser.UseCastable(castable));
        GiveItem("Testinium Necklace");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestForbidEquipped()
    {
        // <Item Slot="None" RestrictionType="NotEquipped">Ham Slapper</Item> <!-- Player has no item equipped named "Ham Slapper" -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidEquipped");

        // Test requirement missing
        GiveEquipment("Testinium Necklace");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_equipped", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        Assert.True(Fixture.TestUser.UseCastable(castable));
        GiveEquipment("Testinium Necklace2");
        Assert.True(Fixture.TestUser.UseCastable(castable));
        GiveEquipment("Testinium Claw");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestForbidSlotAny()
    {
        // <Item Slot="Foot" RestrictionType="NotEquipped"> <!-- Player must not have something equipped in Foot slot -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidSlotAny");

        // Test requirement missing
        GiveEquipment("Testinium Foot");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_slot_any", Fixture.TestUser.LastSystemMessage);
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Testinium Foot2");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_slot_any", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        Assert.True(Fixture.TestUser.UseCastable(castable));
        GiveEquipment("Testinium Necklace");
        Assert.True(Fixture.TestUser.UseCastable(castable));
        GiveEquipment("Testinium Claw");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestForbidSlot()
    {
        // <Item Slot="Foot" RestrictionType="NotEquipped" Message="err_forbid_slot">Testinium Foot</Item>
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidSlot");

        // Test requirement missing
        GiveEquipment("Testinium Foot");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_slot", Fixture.TestUser.LastSystemMessage);
        Fixture.TestUser.Equipment.Clear();

        // Test requirement succeeding
        GiveEquipment("Testinium Foot2");
        Assert.True(Fixture.TestUser.UseCastable(castable));
        GiveEquipment("Testinium Claw");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestRequireWeapon()
    {
        // <Item Slot="Weapon" RestrictionType="Equipped"/>Ham Slicer</Item> <!-- A weapon named Ham Slicer must be equipped -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestRequireWeapon");

        // Test requirement missing
        GiveEquipment("Testinium Foot");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_weapon", Fixture.TestUser.LastSystemMessage);
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Smol Sword");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_require_weapon", Fixture.TestUser.LastSystemMessage);
        Fixture.TestUser.Equipment.Clear();

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Testinium Dagger");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }

    [Fact]
    public void TestForbidWeapon()
    {
        // <Item Slot="Weapon" RestrictionType="NotEquipped"/>Ham Slicer</Item> <!-- A weapon named Ham Slicer must not be equipped -->
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        var castable = GetCastable("TestForbidWeapon");

        // Test requirement missing
        GiveEquipment("Testinium Dagger");
        Assert.False(Fixture.TestUser.UseCastable(castable));
        Assert.Equal("err_forbid_weapon", Fixture.TestUser.LastSystemMessage);

        // Test requirement succeeding
        Fixture.TestUser.Equipment.Clear();
        Assert.True(Fixture.TestUser.UseCastable(castable));
        GiveEquipment("Testinium Claw2");
        Assert.True(Fixture.TestUser.UseCastable(castable));
        Fixture.TestUser.Equipment.Clear();
        GiveEquipment("Smol Sword");
        Assert.True(Fixture.TestUser.UseCastable(castable));
    }
}