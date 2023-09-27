using Hybrasyl.ClientPackets;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Inventory
{
    private HybrasylFixture Fixture { get; set; }

    public Inventory(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void NewInventorySizeIsCorrect()
    {
        var f = new Hybrasyl.Inventory(HybrasylFixture.InventorySize);
        Assert.Equal(HybrasylFixture.InventorySize, f.Size);
    }

    [Fact]
    public void NewInventoryEmptySlotsEqualsSize()
    {
        var f = new Hybrasyl.Inventory(HybrasylFixture.InventorySize);
        Assert.Equal(HybrasylFixture.InventorySize, f.EmptySlots);
    }

    [Fact]
    public void NewInventoryFirstEmptySlotIsOne()
    {
        var f = new Hybrasyl.Inventory(HybrasylFixture.InventorySize);
        Assert.Equal(1, f.FindEmptySlot());
    }

    [Fact]
    public void NewInventoryWeightIsZero()
    {
        var f = new Hybrasyl.Inventory(HybrasylFixture.InventorySize);
        Assert.Equal(0, f.Weight);
    }

    [Fact]
    public void NewInventoryIsNotFull()
    {
        var f = new Hybrasyl.Inventory(HybrasylFixture.InventorySize);
        Assert.False(f.IsFull, "new inventory should not be full");
    }

    [Fact]
    public void ClearInventory()
    {
        Fixture.TestUser.Inventory.Clear();
        Assert.True(Fixture.TestUser.Inventory.Count == 0, "Inventory cleared but count is non-zero");
        Assert.True(Fixture.TestUser.Inventory.ToList().Count == 0,
            "Inventory cleared but enumerated count is non-zero");
        Assert.True(Fixture.TestUser.Inventory.Weight == 0, "Inventory cleared but weight is non-zero");
        Fixture.TestUser.Inventory.RecalculateWeight();
        Assert.True(Fixture.TestUser.Inventory.Weight == 0,
            "Inventory cleared but weight is non-zero after recalculation");
    }


    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems),MemberType = typeof(XmlItemTestData))]
    public void AddRetrieveAndRemoveItems(params Item[] items)
    {
        Fixture.TestUser.Inventory.Clear();

        var numItems = 0;
        var startWeight = 0;
        var expectedWeight = 0;
        foreach (var i in items)
        {
            Assert.True(expectedWeight == startWeight,
                $"Weight calculation is incorrect (expected {expectedWeight}, got {startWeight}");
            var itemObj = Game.World.CreateItem(i.Id);
            Assert.True(Fixture.TestUser.AddItem(itemObj),
                $"Item {itemObj.Name} could not be added to inventory");
            expectedWeight += itemObj.Weight;
            startWeight = expectedWeight;
            Assert.True(Fixture.TestUser.Inventory.Weight == expectedWeight,
                $"Item added to inventory: start weight {startWeight}, item weight {itemObj.Weight}, expected {expectedWeight}, weight is {Fixture.TestUser.Inventory.Weight}");
            numItems++;
            Assert.True(Fixture.TestUser.Inventory.TryGetValue(i.Id, out var invObj),
                $"Inventory had {i.Name} added but is not found by template ID");
            var objList = Fixture.TestUser.Inventory.TryGetValueByName(i.Name, out var invObjName);
            Assert.True(objList, $"Inventory had {i.Name} added but TryGetValueByName fails");
            Assert.Contains(invObjName, filter: x => x.obj == itemObj);
            //$"Inventory had {i.Name} but return list from TryGetValueByName does not contain it");

            Assert.True(Fixture.TestUser.Inventory.Count == numItems,
                $"Inventory has had {numItems} items added, but count ({Fixture.TestUser.Inventory.Count}) is not equivalent");
            Assert.True(invObj.Name == i.Name,
                $"TryGetValue: Inventory contains an item {invObj.Name} but it isn't the test item {i.Name}");
            Assert.True(invObjName.Select(selector: x => x.obj.Name == itemObj.Name).Count() == invObjName.Count,
                $"TryGetValueByName: Inventory contains a singular item {itemObj.Name} but multiple items returned");
            Assert.True(Fixture.TestUser.Inventory.ContainsId(i.Id),
                $"Contains: Inventory should contain {i.Name} but returned false");
            Assert.True(Fixture.TestUser.Inventory.ContainsName(i.Name),
                "ContainsName: Inventory should contain {i.Name} but returned false");
        }
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems),MemberType = typeof(XmlItemTestData))]
    public void FindItemsByCategory(params Item[] items)
    {
        Fixture.TestUser.Inventory.Clear();

        foreach (var item in items)
        {
            // Add five of each item to the inventory
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
        }

        // All 10 slots contain xmlitem tagged items
        Assert.Equal(new List<byte> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            Fixture.TestUser.Inventory.GetSlotsByCategory("xmlitem"));
        // Assert first five slots contain junk
        Assert.Equal(new List<byte> { 1, 2, 3, 4, 5 },
            Fixture.TestUser.Inventory.GetSlotsByCategory("junk"));
        // Next five slots should contain "stackable"
        Assert.Equal(new List<byte> { 6, 7, 8, 9, 10 },
            Fixture.TestUser.Inventory.GetSlotsByCategory("stackable"));
        // Categories should be case insensitive
        Assert.Equal(new List<byte> { 6, 7, 8, 9, 10 },
            Fixture.TestUser.Inventory.GetSlotsByCategory("StAcKaBlE"));

        // After a swap, GetSlotsByCategory should return correct results
        for (byte x = 6; x <= 10; x++) Assert.True(Fixture.TestUser.Inventory.Swap(x, (byte)(59 - x)));
        Assert.Equal(new List<byte> { 53, 52, 51, 50, 49 },
            Fixture.TestUser.Inventory.GetSlotsByCategory("stackable"));
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems), MemberType = typeof(XmlItemTestData))]
    public void ContainsGuid(params Item[] items)
    {
        Fixture.TestUser.Inventory.Clear();
        for (var x = 1; x < 6; x++)
        {
            var item = Game.World.CreateItem(items[0].Id);
            Fixture.TestUser.AddItem(item);
            Assert.True(Fixture.TestUser.Inventory.Contains(item));
        }
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems), MemberType = typeof(XmlItemTestData))]
    public void AddItemsToInventory(params Item[] items)
    {
        Fixture.TestUser.Inventory.Clear();

        for (var x = 1; x < 6; x++) Fixture.TestUser.AddItem(Game.World.CreateItem(items[0].Id));

        Assert.True(Fixture.TestUser.Inventory.Count == 5,
            $"Inventory with five items should have count of 5, count is {Fixture.TestUser.Inventory.Count}");
        Assert.True(Fixture.TestUser.Inventory[1].Name == "Test Item",
            "First slot: Inventory contains an item but it isn't a test item");
        Assert.True(Fixture.TestUser.Inventory[5].Name == "Test Item",
            "Fifth slot: Inventory contains an item but it isn't a test item");
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems), MemberType = typeof(XmlItemTestData))]
    public void DropItemPacket(params Item[] items)
    {
        Fixture.TestUser.Inventory.Clear();
        foreach (var item in items) Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));

        var guid = Fixture.TestUser.Inventory[1].Guid;
        var testPacket = new DropItem(1, Fixture.TestUser.X, Fixture.TestUser.Y,
            (uint)Fixture.TestUser.Inventory[1].Count);

        var handler = Game.World.WorldPacketHandlers[0x08];
        Assert.NotNull(handler);
        handler(Fixture.TestUser, (ClientPacket)testPacket);

        // Assert X,Y contains the item we just dropped

        var tileContents = Fixture.TestUser.Map.GetTileContents(Fixture.TestUser.X, Fixture.TestUser.Y);
        var tileGuids = tileContents.Select(selector: x => x.Guid);
        // Tile should contain the user and the dropped object
        Assert.True(tileContents.Count == 2);
        // Tile should contain the test user
        Assert.Contains(Fixture.TestUser, tileContents);
        // Inventory slot should be empty
        Assert.Null(Fixture.TestUser.Inventory[1]);
        // Tile should contain objects with same guid as the user and the item
        Assert.Contains(Fixture.TestUser.Guid, tileGuids);
        Assert.Contains(guid, tileGuids);
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems), MemberType = typeof(XmlItemTestData))]
    public void SwapItems(params Item[] items)
    {
        Fixture.TestUser.Inventory.Clear();

        foreach (var item in items)
        {
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item.Id)));
        }

        for (byte x = 1; x <= 10; x++)
            Assert.True(Fixture.TestUser.Inventory[x] != null,
                "SwapItems: slot {x} is null but should not be");
        // Swap with inventory between two filled slots
        Fixture.TestUser.Inventory.Swap(1, 6);
        Assert.True(Fixture.TestUser.Inventory[1].Name == "Stackable Test Item");
        Assert.True(Fixture.TestUser.Inventory[6].Name == "Test Item");

        // Swap with inventory between a filled slot and an empty slot
        Fixture.TestUser.Inventory.Swap(2, 59);
        Assert.True(Fixture.TestUser.Inventory[2] is null);
        Assert.True(Fixture.TestUser.Inventory[59].Name == "Test Item");

        // Swap with user function between two filled slots
        Fixture.TestUser.SwapItem(3, 7);
        Assert.True(Fixture.TestUser.Inventory[3].Name == "Stackable Test Item");
        Assert.True(Fixture.TestUser.Inventory[7].Name == "Test Item");

        // Swap with user function between a filled slot and an empty slot
        Fixture.TestUser.SwapItem(4, 58);
        Assert.True(Fixture.TestUser.Inventory[4] is null);
        Assert.True(Fixture.TestUser.Inventory[58].Name == "Test Item");
    }

    [Fact]
    public void RemoveEquipmentFailIfInventoryFull()
    {
        Fixture.TestUser.Equipment.Clear();
        Fixture.ResetUserStats();
        Fixture.TestUser.Stats.BaseStr = 255;
        var ring = Fixture.TestEquipment[EquipmentSlot.Ring].Clone<Item>();
        var ringObj = Game.World.CreateItem(ring);
        Assert.NotNull(ringObj);
        Fixture.TestUser.AddEquipment(ringObj, (byte)EquipmentSlot.RightHand);
        while (!Fixture.TestUser.Inventory.IsFull)
        {
            var anotherRingObj = Game.World.CreateItem(ring);
            Fixture.TestUser.AddItem(anotherRingObj);
        }
        Assert.True(Fixture.TestUser.Inventory.IsFull);
        var guid = Fixture.TestUser.Inventory[1].Guid;
        var testPacket = new EquipItemClick((byte)EquipmentSlot.RightHand);

        var handler = Game.World.WorldPacketHandlers[testPacket.Opcode];
        Assert.NotNull(handler);
        handler(Fixture.TestUser, (ClientPacket)testPacket);
        Assert.Equal("You can't carry anything else.", Fixture.TestUser.LastSystemMessage);

    }


    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems), MemberType = typeof(XmlItemTestData))]
    public void MoveItemBetweenSlotsInInventory(params Item[] item)
    {
        Fixture.TestUser.Inventory.Clear();
        Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item[0].Id)),
            $"Adding item {item[0].Name} to inventory failed");
        Assert.True(Fixture.TestUser.Inventory.Count == 1,
            "Inventory with one item should have count of 1");
        Assert.True(Fixture.TestUser.Inventory.Swap(1, 5),
            "Swap {item[0].Name} from slot 1 to slot 5 failed");
        Assert.False(Fixture.TestUser.Inventory[5] == null, "Swap to slot 5 failed, slot 5 is null");
        Assert.True(Fixture.TestUser.Inventory[1] == null,
            "Swap to slot 5 failed, slot 1 still contains an item");
        Assert.True(Fixture.TestUser.Inventory.Count == 1,
            "Inventory with one item should have count of 1");
        Assert.True(Fixture.TestUser.Inventory[5].Name == "Test Item",
            "Slot 5 contains an item but it isn't a test item");
        Assert.True(Fixture.TestUser.Inventory[4] == null,
            "Item fencepost error? Inventory slot 4 is not null");
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems), MemberType = typeof(XmlItemTestData))]
    public void FullInventoryShouldBeFull(params Item[] item)
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Stats.BaseStr = 255;
        Fixture.TestUser.Inventory.Clear();
        for (var x = 1; x <= HybrasylFixture.InventorySize; x++)
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item[0].Id)),
                $"Adding item #{x} to inventory failed, sys msg {Fixture.TestUser.LastSystemMessage}");

        Assert.True(Fixture.TestUser.Inventory.IsFull,
            $"Inventory that is full should return isFull == true, {Fixture.TestUser.LastSystemMessage}");
        Assert.False(Fixture.TestUser.Inventory.AddItem(Game.World.CreateItem(item[0].Id)),
            "Adding an item to a full inventory should fail");
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems), MemberType = typeof(XmlItemTestData))]
    public void WeightCalculations(params Item[] item)
    {
        Fixture.TestUser.Inventory.Clear();
        for (var x = 0; x < 10; x++)
            Assert.True(Fixture.TestUser.AddItem(Game.World.CreateItem(item[0].Id)),
                $"Adding item #{x} to inventory failed");

        Assert.False(Fixture.TestUser.Inventory.IsFull, "Non-full inventory should not be full");
        Assert.True(Fixture.TestUser.Inventory.Weight == item[0].Properties.Physical.Weight * 10,
            $"Ten items of weight {item[0].Properties.Physical.Weight} were added, but total weight is {Fixture.TestUser.Inventory.Weight}");
    }

    // TODO: theory
    [Fact]
    public void EquipTestEquipment()
    {
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
        {
            if (slot == EquipmentSlot.None || slot == EquipmentSlot.Gauntlet ||
                slot == EquipmentSlot.Ring) continue;
            var itemObject = Game.World.CreateItem(Fixture.TestEquipment[slot].Id);
            Assert.True(Fixture.TestUser.AddEquipment(itemObject, (byte)slot, false),
                $"Adding equipment to {slot} failed, last sys message {Fixture.TestUser.LastSystemMessage}");
        }

        // TODO: make slots uniform. haha no really

        Assert.True(Fixture.TestUser.Equipment.Weapon.Name == "Equip Test Weapon",
            $"Weapon should be Equip Test Weapon, is {Fixture.TestUser.Equipment.Weapon.Name}");
        Assert.True(Fixture.TestUser.Equipment.Armor.Name == "Equip Test Armor",
            $"Armor should be Equip Test Armor, is {Fixture.TestUser.Equipment.Armor.Name}");
        Assert.True(Fixture.TestUser.Equipment.Shield.Name == "Equip Test Shield",
            $"Shield should be Equip Test Shield, is {Fixture.TestUser.Equipment.Shield.Name}");
        Assert.True(Fixture.TestUser.Equipment.Helmet.Name == "Equip Test Helmet",
            $"Helmet should be Equip Test Helmet, is {Fixture.TestUser.Equipment.Helmet.Name}");
        Assert.True(Fixture.TestUser.Equipment.Earring.Name == "Equip Test Earring",
            $"Earring should be Equip Test Earring, is {Fixture.TestUser.Equipment.Earring.Name}");
        Assert.True(Fixture.TestUser.Equipment.Necklace.Name == "Equip Test Necklace",
            $"Necklace should be Equip Test Necklace, is {Fixture.TestUser.Equipment.Necklace.Name}");
        Assert.True(Fixture.TestUser.Equipment.LRing.Name == "Equip Test LeftHand",
            $"LRing should be Equip Test LeftHand, is {Fixture.TestUser.Equipment.LRing.Name}");
        Assert.True(Fixture.TestUser.Equipment.RRing.Name == "Equip Test RightHand",
            $"RRing should be Equip Test RightHand, is {Fixture.TestUser.Equipment.RRing.Name}");
        Assert.True(Fixture.TestUser.Equipment.LGauntlet.Name == "Equip Test LeftArm",
            $"LGauntlet should be Equip Test LeftArm, is {Fixture.TestUser.Equipment.LGauntlet.Name}");
        Assert.True(Fixture.TestUser.Equipment.RGauntlet.Name == "Equip Test RightArm",
            $"RGauntlet should be Equip Test RightArm, is {Fixture.TestUser.Equipment.RGauntlet.Name}");
        Assert.True(Fixture.TestUser.Equipment.Belt.Name == "Equip Test Waist",
            $"Belt should be Equip Test Waist, is {Fixture.TestUser.Equipment.Belt.Name}");
        Assert.True(Fixture.TestUser.Equipment.Greaves.Name == "Equip Test Leg",
            $"Greaves should be Equip Test Leg, is {Fixture.TestUser.Equipment.Greaves.Name}");
        Assert.True(Fixture.TestUser.Equipment.Boots.Name == "Equip Test Foot",
            $"Boots should be Equip Test Foot, is {Fixture.TestUser.Equipment.Boots.Name}");
        Assert.True(Fixture.TestUser.Equipment.FirstAcc.Name == "Equip Test FirstAcc",
            $"FirstAcc should be Equip Test FirstAcc, is {Fixture.TestUser.Equipment.FirstAcc.Name}");
        Assert.True(Fixture.TestUser.Equipment.SecondAcc.Name == "Equip Test SecondAcc",
            $"SecondAcc should be Equip Test SecondAcc, is {Fixture.TestUser.Equipment.SecondAcc.Name}");
        Assert.True(Fixture.TestUser.Equipment.ThirdAcc.Name == "Equip Test ThirdAcc",
            $"ThirdAcc should be Equip Test ThirdAcc, is {Fixture.TestUser.Equipment.ThirdAcc.Name}");
        Assert.True(Fixture.TestUser.Equipment.Overcoat.Name == "Equip Test Trousers",
            $"Overcoat should be Equip Test Trousers, is {Fixture.TestUser.Equipment.Overcoat.Name}");
        Assert.True(Fixture.TestUser.Equipment.DisplayHelm.Name == "Equip Test Coat",
            $"DisplayHelm should be Equip Test Coat, is {Fixture.TestUser.Equipment.DisplayHelm.Name}");
        Assert.True(Fixture.TestUser.Equipment.Weight == 18,
            $"Test equipment weight is 18 but equipped weight is {Fixture.TestUser.Equipment.Weight}");
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems), MemberType = typeof(XmlItemTestData))]
    public void InventorySerialization(params Item[] items)
    {
        Fixture.TestUser.Inventory.Clear();

        foreach (var item in items)
        {
            var itemObj = Game.World.CreateItem(item.Id);
            itemObj.Count = item.Properties.Stackable.Max;
            Assert.True(Fixture.TestUser.Inventory.AddItem(itemObj),
                $"Adding {item.Name} to inventory failed");
        }

        Fixture.TestUser.Save();

        Assert.True(Game.World.WorldState.TryGetUser("TestUser", out var u1),
            "Test user should exist after save but can't be found");

        Assert.True(u1.Inventory[1] != null, "Inventory slot 1 should be non-null after deserialization");
        Assert.True(u1.Inventory[2] != null, "Inventory slot 2 should be non-null after deserialization");
        Assert.True(u1.Inventory.TryGetValueByName("Test Item", out _),
            "Deserialized inventory failed TryGetValueByName");
    }

    [Fact]
    public void EquipmentSerialization()
    {
        Fixture.TestUser.Inventory.Clear();
        Fixture.TestUser.Equipment.Clear();

        foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
        {
            if (slot == EquipmentSlot.None || slot == EquipmentSlot.Gauntlet ||
                slot == EquipmentSlot.Ring) continue;
            var itemObject = Game.World.CreateItem(Fixture.TestEquipment[slot].Id);
            Assert.True(Fixture.TestUser.AddEquipment(itemObject, (byte)slot, false),
                $"Adding equipment to {slot} failed");
        }

        Fixture.TestUser.Save();

        Assert.True(Game.World.WorldState.TryGetUser("TestUser", out var u1),
            "Test user should exist after save but can't be found");

        Assert.True(u1.Equipment.Weapon.Name == "Equip Test Weapon",
            $"Weapon should be Equip Test Weapon, is {u1.Equipment.Weapon.Name}");
        Assert.True(u1.Equipment.Armor.Name == "Equip Test Armor",
            $"Armor should be Equip Test Armor, is {u1.Equipment.Armor.Name}");
        Assert.True(u1.Equipment.Shield.Name == "Equip Test Shield",
            $"Shield should be Equip Test Shield, is {u1.Equipment.Shield.Name}");
        Assert.True(u1.Equipment.Helmet.Name == "Equip Test Helmet",
            $"Helmet should be Equip Test Helmet, is {u1.Equipment.Helmet.Name}");
        Assert.True(u1.Equipment.Earring.Name == "Equip Test Earring",
            $"Earring should be Equip Test Earring, is {u1.Equipment.Earring.Name}");
        Assert.True(u1.Equipment.Necklace.Name == "Equip Test Necklace",
            $"Necklace should be Equip Test Necklace, is {u1.Equipment.Necklace.Name}");
        Assert.True(u1.Equipment.LRing.Name == "Equip Test LeftHand",
            $"LRing should be Equip Test LeftHand, is {u1.Equipment.LRing.Name}");
        Assert.True(u1.Equipment.RRing.Name == "Equip Test RightHand",
            $"RRing should be Equip Test RightHand, is {u1.Equipment.RRing.Name}");
        Assert.True(u1.Equipment.LGauntlet.Name == "Equip Test LeftArm",
            $"LGauntlet should be Equip Test LeftArm, is {u1.Equipment.LGauntlet.Name}");
        Assert.True(u1.Equipment.RGauntlet.Name == "Equip Test RightArm",
            $"RGauntlet should be Equip Test RightArm, is {u1.Equipment.RGauntlet.Name}");
        Assert.True(u1.Equipment.Belt.Name == "Equip Test Waist",
            $"Belt should be Equip Test Waist, is {u1.Equipment.Belt.Name}");
        Assert.True(u1.Equipment.Greaves.Name == "Equip Test Leg",
            $"Greaves should be Equip Test Leg, is {u1.Equipment.Greaves.Name}");
        Assert.True(u1.Equipment.Boots.Name == "Equip Test Foot",
            $"Boots should be Equip Test Foot, is {u1.Equipment.Boots.Name}");
        Assert.True(u1.Equipment.FirstAcc.Name == "Equip Test FirstAcc",
            $"FirstAcc should be Equip Test FirstAcc, is {u1.Equipment.FirstAcc.Name}");
        Assert.True(u1.Equipment.SecondAcc.Name == "Equip Test SecondAcc",
            $"SecondAcc should be Equip Test SecondAcc, is {u1.Equipment.SecondAcc.Name}");
        Assert.True(u1.Equipment.ThirdAcc.Name == "Equip Test ThirdAcc",
            $"ThirdAcc should be Equip Test ThirdAcc, is {u1.Equipment.ThirdAcc.Name}");
        Assert.True(u1.Equipment.Overcoat.Name == "Equip Test Trousers",
            $"Overcoat should be Equip Test Trousers, is {u1.Equipment.Overcoat.Name}");
        Assert.True(u1.Equipment.DisplayHelm.Name == "Equip Test Coat",
            $"DisplayHelm should be Equip Test Coat, is {u1.Equipment.DisplayHelm.Name}");

        u1.Equipment.RecalculateWeight();
        Assert.True(u1.Equipment.Weight == 18,
            $"Test equipment weight is 18 but equipped weight is {u1.Equipment.Weight}");

        foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
        {
            if (slot == EquipmentSlot.None || slot == EquipmentSlot.Gauntlet ||
                slot == EquipmentSlot.Ring) continue;
            Assert.True(u1.Equipment[(byte)slot].Durability == 1000, "Durability of item in {slot} is not 1000");
            Assert.True(u1.Equipment[(byte)slot].Guid != default, "Guid for {slot} is corrupt / not set");
            Assert.True(u1.Equipment[(byte)slot].TemplateId == Fixture.TestEquipment[slot].Id,
                "{slot}: ID of item in inventory does not match template");
        }
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.XmlItems), MemberType = typeof(XmlItemTestData))]
    public void InventoryContains(params Item[] items)
    {
        Fixture.TestUser.Inventory.Clear();
        foreach (var item in items)
        {
            var itemObj = Game.World.CreateItem(item.Id);
            itemObj.Count = item.Properties.Stackable.Max;
            Assert.True(Fixture.TestUser.Inventory.AddItem(itemObj),
                $"Adding {item.Name} to inventory failed");
            Assert.True(Fixture.TestUser.Inventory.ContainsId(item.Id),
                $"Inventory should contain {item.Name}, Contains failed");
            Assert.True(Fixture.TestUser.Inventory.ContainsName(item.Name),
                $"Inventory should contain {item.Name}, ContainsName failed");
            for (var x = 1; x <= item.Properties.Stackable.Max; x++)
                Assert.True(Fixture.TestUser.Inventory.ContainsName(item.Name, x),
                    $"Inventory should contain {x} of {item.Name}, Contains with qty check failed");
        }
    }

    [Theory]
    [MemberData(nameof(XmlItemTestData.StackableXmlItems),MemberType = typeof(XmlItemTestData))]
    public void RemoveQuantity(params Item[] items)
    {
        Fixture.TestUser.Inventory.Clear();

        foreach (var item in items)
        {
            var itemObj = Game.World.CreateItem(item.Id);
            itemObj.Count = 1;
            Assert.True(Fixture.TestUser.Inventory.AddItem(itemObj),
                $"Adding {item.Name} to inventory failed");

            Assert.False(Fixture.TestUser.Inventory.TryRemoveQuantity(item.Id, out var removed1, 5),
                $"TryRemoveQuantity: attempted to remove 5, removed {removed1} {itemObj.Name} but only 1 should exist");
            Assert.True(Fixture.TestUser.Inventory[1].Count == 1,
                $"TryRemoveQuantity failed but count is now {Fixture.TestUser.Inventory[1].Count}");
            Assert.False(Fixture.TestUser.Inventory.TryRemoveQuantity(item.Id, out var removed2, -5),
                $"TryRemoveQuantity: attempted to remove -5 {itemObj.Name}, removed {removed2} - should never succeed");
            Assert.True(Fixture.TestUser.Inventory[1].Count == 1,
                $"TryRemoveQuantity failed but count is now {Fixture.TestUser.Inventory[1].Count}");

            Assert.True(Fixture.TestUser.Inventory.TryRemoveQuantity(item.Id, out var removed3),
                $"TryRemoveQuantity: failed, removed {removed3}");
            Assert.False(Fixture.TestUser.Inventory.ContainsId(item.Id),
                "TryRemoveQuantity: count 1, removed 1, but item still exists in inventory");
        }

        foreach (var item in items)
        {
            var itemObj = Game.World.CreateItem(item.Id);
            itemObj.Count = 6;
            var itemObj2 = Game.World.CreateItem(item.Id);
            itemObj2.Count = 4;
            var itemObj3 = Game.World.CreateItem(item.Id);
            itemObj3.Count = 10;
            Assert.True(Fixture.TestUser.Inventory.AddItem(itemObj),
                $"Adding {item.Name} to inventory failed");
            Assert.True(Fixture.TestUser.Inventory.AddItem(itemObj2),
                $"Adding {item.Name} to inventory failed");
            Assert.True(Fixture.TestUser.Inventory.AddItem(itemObj3),
                $"Adding {item.Name} to inventory failed");

            Fixture.TestUser.Inventory.TryRemoveQuantity(item.Id, out var removed1, 5);

            Assert.True(removed1.Sum(selector: x => x.Quantity) == 5,
                $"TryRemoveQuantity: removed 5 from 6 4 10 but removed {removed1.Sum(selector: x => x.Quantity)} instead");
            Assert.True(removed1[0].Slot == 1 && removed1[0].Quantity == 5,
                $"TryRemoveQuantity: should have removed 5 from slot 1, return indicates removed {removed1[0].Quantity} from slot {removed1[0].Slot}");
            Assert.True(removed1.Count == 1,
                "TryRemoveQuantity: should have removed 5 from slot 1, but return indicates multiple slots were modified");

            var remaining = Fixture.TestUser.Inventory[1].Count +
                            Fixture.TestUser.Inventory[2].Count +
                            Fixture.TestUser.Inventory[3].Count;

            Assert.True(remaining == 15,
                $"TryRemoveQuantity: attempted to remove 5 from 20, removed {removed1.Count} {itemObj.Name}, but {remaining} remaining");
            // Should now be 1 4 10
            Fixture.TestUser.Inventory.TryRemoveQuantity(item.Id, out var removed2, 7);
            var remaining2 = Fixture.TestUser.Inventory[1]?.Count ?? 0 +
                Fixture.TestUser.Inventory[2]?.Count ?? 0 +
                Fixture.TestUser.Inventory[3]?.Count ?? 0;
            // Should now be <null> <null> 8
            Assert.True(removed2.Sum(selector: x => x.Quantity) == 7,
                $"TryRemoveQuantity: Removed 7 from 1 4 10 but removed is {removed2.Sum(selector: x => x.Quantity)} instead");
            Assert.True(removed2[0].Slot == 1 && removed2[0].Quantity == 1,
                $"TryRemoveQuantity: should have removed 1 from slot 1, return indicates removed {removed2[0].Quantity} from slot {removed2[0].Slot}");
            Assert.True(removed2[1].Slot == 2 && removed2[1].Quantity == 4,
                $"TryRemoveQuantity: should have removed 4 from slot 2, return indicates removed {removed2[1].Quantity} from slot {removed2[1].Slot}");
            Assert.True(removed2[2].Slot == 3 && removed2[2].Quantity == 2,
                $"TryRemoveQuantity: should have removed 2 from slot 3, return indicates removed {removed2[2].Quantity} from slot {removed2[2].Slot}");
            Assert.True(Fixture.TestUser.Inventory[1] == null,
                "TryRemoveQuantity: slot 1 is not null after removal, but should be");
            Assert.True(Fixture.TestUser.Inventory[2] == null,
                "TryRemoveQuantity: slot 2 is not null after removal, but should be");
            Assert.True(Fixture.TestUser.Inventory[3].Count == 8,
                $"TryRemoveQuantity: slot 3 count should be 8, but is {Fixture.TestUser.Inventory[3].Count}");
        }
    }
}