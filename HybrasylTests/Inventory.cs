using System;
using Xunit;
using Hybrasyl;
using Hybrasyl.Xml;
using Hybrasyl.Objects;
using System.Collections.Generic;
using System.Collections;

namespace HybrasylTests
{
    public class Inventory
    {
        [Fact]
        public void NewInventorySizeIsCorrect()
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            Assert.Equal(InventoryTestData.InventorySize, f.Size);
        }

        [Fact]
        public void NewInventoryEmptySlotsEqualsSize()
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            Assert.Equal(InventoryTestData.InventorySize, f.EmptySlots);
        }

        [Fact]
        public void NewInventoryFirstEmptySlotIsOne()
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            Assert.Equal(1, f.FindEmptySlot());
        }

        [Fact]
        public void NewInventoryWeightIsZero()
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            Assert.Equal(0, f.Weight);
        }

        [Fact]
        public void NewInventoryWeightIsNotFull()
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            Assert.False(f.IsFull, "new inventory should not be full");
        }

        [Theory]
        [ClassData(typeof(InventoryTestData))]
        public void AddItemToInventory(ItemObject item)
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            f.AddItem(item);
            Assert.True(f.Count == 1, "Inventory with one item should have count of 1");
            Assert.True(f[1].Name == "Test Item", "(First slot client based) Inventory contains an item but it isn't a test item");
            Assert.True(f[0, false].Name == "Test Item", "(First slot server based) Inventory contains an item but it isn't a test item");
            Assert.Equal(f[1], item);
        }

        [Theory]
        [ClassData(typeof(InventoryTestData))]
        public void AddItemsToInventory(ItemObject item)
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            f.AddItem(item);
            f.AddItem(item);
            f.AddItem(item);
            f.AddItem(item);
            f.AddItem(item);
            Assert.True(f.Count == 5, "Inventory with five items should have count of 5");
            Assert.True(f[1].Name == "Test Item", "(First slot client based) Inventory contains an item but it isn't a test item");
            Assert.True(f[5].Name == "Test Item", "(Fifth slot client based) Inventory contains an item but it isn't a test item");
            Assert.True(f[0, false].Name == "Test Item", "(First slot server based) Inventory contains an item but it isn't a test item");
            Assert.True(f[1] == f[5], "Added same item to five slots but slot one is not equivalent to slot five (item wise)");
        }

        [Theory]
        [ClassData(typeof(InventoryTestData))]
        public void MoveItemBetweenSlotsInInventory(ItemObject item)
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            f.AddItem(item);
            Assert.True(f.Count == 1, "Inventory with one item should have count of 1");
            Assert.True(f[1].Name == "Test Item", "(First slot client based) Inventory contains an item but it isn't a test item");
            Assert.True(f[0, false].Name == "Test Item", "(First slot server based) Inventory contains an item but it isn't a test item");
            Assert.Equal(f[1], item);
        }

        [Theory]
        [ClassData(typeof(InventoryTestData))]
        public void FullInventoryShouldBeFull(ItemObject item)
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            for (var x = 0; x < InventoryTestData.InventorySize; x++)
                Assert.True(f.AddItem(item), "Adding item to inventory should succeed");
            Assert.True(f.IsFull, "Inventory that is full should return isFull == true");
            Assert.False(f.AddItem(item), "Adding an item to a full inventory should fail");          
        }

        [Theory]
        [ClassData(typeof(InventoryTestData))]
        public void WeightCalculations(ItemObject item)
        {
            var f = new Hybrasyl.Inventory(InventoryTestData.InventorySize);
            for (var x = 0; x < 10; x++)
                f.AddItem(item);
            Assert.False(f.IsFull, "Non-full inventory should not be full");
            Assert.True(f.Weight == item.Weight * 10, $"Ten items of weight {item.Weight} were added, but total weight is {f.Weight}");
        }

        [Fact]
        public void EquipTestEquipment()
        {
            var u = new User();
      
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                u.AddEquipment(InventoryTestData.TestEquipment[slot], (byte)slot, false);
            }

            // TODO: make slots uniform. haha no really

            Assert.True(u.Equipment.Weapon.Name == $"Equip Test Weapon", $"Weapon should be Equip Test Weapon, is {u.Equipment.Weapon.Name}");
            Assert.True(u.Equipment.Armor.Name == $"Equip Test Armor", $"Armor should be Equip Test Armor, is {u.Equipment.Armor.Name}");
            Assert.True(u.Equipment.Shield.Name == $"Equip Test Shield", $"Shield should be Equip Test Shield, is {u.Equipment.Shield.Name}");
            Assert.True(u.Equipment.Helmet.Name == $"Equip Test Helmet", $"Helmet should be Equip Test Helmet, is {u.Equipment.Helmet.Name}");
            Assert.True(u.Equipment.Earring.Name == $"Equip Test Earring", $"Earring should be Equip Test Earring, is {u.Equipment.Earring.Name}");
            Assert.True(u.Equipment.Necklace.Name == $"Equip Test Necklace", $"Necklace should be Equip Test Necklace, is {u.Equipment.Necklace.Name}");
            Assert.True(u.Equipment.LRing.Name == $"Equip Test LeftHand", $"LRing should be Equip Test LeftHand, is {u.Equipment.LRing.Name}");
            Assert.True(u.Equipment.RRing.Name == $"Equip Test RightHand", $"RRing should be Equip Test RightHand, is {u.Equipment.RRing.Name}");
            Assert.True(u.Equipment.LGauntlet.Name == $"Equip Test LeftArm", $"LGauntlet should be Equip Test LeftArm, is {u.Equipment.LGauntlet.Name}");
            Assert.True(u.Equipment.RGauntlet.Name == $"Equip Test RightArm", $"RGauntlet should be Equip Test RightArm, is {u.Equipment.RGauntlet.Name}");
            Assert.True(u.Equipment.Belt.Name == $"Equip Test Waist", $"Belt should be Equip Test Waist, is {u.Equipment.Belt.Name}");
            Assert.True(u.Equipment.Greaves.Name == $"Equip Test Leg", $"Greaves should be Equip Test Leg, is {u.Equipment.Greaves.Name}");
            Assert.True(u.Equipment.Boots.Name == $"Equip Test Foot", $"Boots should be Equip Test Foot, is {u.Equipment.Boots.Name}");
            Assert.True(u.Equipment.FirstAcc.Name == $"Equip Test FirstAcc", $"FirstAcc should be Equip Test FirstAcc, is {u.Equipment.FirstAcc.Name}");
            Assert.True(u.Equipment.SecondAcc.Name == $"Equip Test SecondAcc", $"SecondAcc should be Equip Test SecondAcc, is {u.Equipment.SecondAcc.Name}");
            Assert.True(u.Equipment.ThirdAcc.Name == $"Equip Test ThirdAcc", $"ThirdAcc should be Equip Test ThirdAcc, is {u.Equipment.ThirdAcc.Name}");
            Assert.True(u.Equipment.Overcoat.Name == $"Equip Test Trousers", $"Overcoat should be Equip Test Trousers, is {u.Equipment.Overcoat.Name}");
            Assert.True(u.Equipment.DisplayHelm.Name == $"Equip Test Coat", $"DisplayHelm should be Equip Test Coat, is {u.Equipment.DisplayHelm.Name}");
            Assert.True(u.Equipment.Weight == 180, $"Test equipment weight is 180 but equipped weight is {u.Equipment.Weight}");
        }

        [Fact]
        public void InventorySerialization()
        {
            Assert.True(Game.World.WorldData.TryGetUser("TestUser", out User u), "Test user should exist but can't be found");
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                Assert.True(u.AddEquipment(InventoryTestData.TestEquipment[slot], (byte)slot, false), $"Adding equipment to {slot} failed");
            }
            for (var x = 0; x < 10; x++)
            {
            }
            u.Save();
            Assert.True(Game.World.WorldData.TryGetUser("TestUser", out User u1), "Test user should exist but can't be found");
            Assert.True(u1.Equipment.Weapon.Name == $"Equip Test Weapon", $"Weapon should be Equip Test Weapon, is {u1.Equipment.Weapon.Name}");
            Assert.True(u1.Equipment.Armor.Name == $"Equip Test Armor", $"Armor should be Equip Test Armor, is {u1.Equipment.Armor.Name}");
            Assert.True(u1.Equipment.Shield.Name == $"Equip Test Shield", $"Shield should be Equip Test Shield, is {u1.Equipment.Shield.Name}");
            Assert.True(u1.Equipment.Helmet.Name == $"Equip Test Helmet", $"Helmet should be Equip Test Helmet, is {u1.Equipment.Helmet.Name}");
            Assert.True(u1.Equipment.Earring.Name == $"Equip Test Earring", $"Earring should be Equip Test Earring, is {u1.Equipment.Earring.Name}");
            Assert.True(u1.Equipment.Necklace.Name == $"Equip Test Necklace", $"Necklace should be Equip Test Necklace, is {u1.Equipment.Necklace.Name}");
            Assert.True(u1.Equipment.LRing.Name == $"Equip Test LeftHand", $"LRing should be Equip Test LeftHand, is {u1.Equipment.LRing.Name}");
            Assert.True(u1.Equipment.RRing.Name == $"Equip Test RightHand", $"RRing should be Equip Test RightHand, is {u1.Equipment.RRing.Name}");
            Assert.True(u1.Equipment.LGauntlet.Name == $"Equip Test LeftArm", $"LGauntlet should be Equip Test LeftArm, is {u1.Equipment.LGauntlet.Name}");
            Assert.True(u1.Equipment.RGauntlet.Name == $"Equip Test RightArm", $"RGauntlet should be Equip Test RightArm, is {u1.Equipment.RGauntlet.Name}");
            Assert.True(u1.Equipment.Belt.Name == $"Equip Test Waist", $"Belt should be Equip Test Waist, is {u1.Equipment.Belt.Name}");
            Assert.True(u1.Equipment.Greaves.Name == $"Equip Test Leg", $"Greaves should be Equip Test Leg, is {u1.Equipment.Greaves.Name}");
            Assert.True(u1.Equipment.Boots.Name == $"Equip Test Foot", $"Boots should be Equip Test Foot, is {u1.Equipment.Boots.Name}");
            Assert.True(u1.Equipment.FirstAcc.Name == $"Equip Test FirstAcc", $"FirstAcc should be Equip Test FirstAcc, is {u1.Equipment.FirstAcc.Name}");
            Assert.True(u1.Equipment.SecondAcc.Name == $"Equip Test SecondAcc", $"SecondAcc should be Equip Test SecondAcc, is {u1.Equipment.SecondAcc.Name}");
            Assert.True(u1.Equipment.ThirdAcc.Name == $"Equip Test ThirdAcc", $"ThirdAcc should be Equip Test ThirdAcc, is {u1.Equipment.ThirdAcc.Name}");
            Assert.True(u1.Equipment.Overcoat.Name == $"Equip Test Trousers", $"Overcoat should be Equip Test Trousers, is {u1.Equipment.Overcoat.Name}");
            Assert.True(u1.Equipment.DisplayHelm.Name == $"Equip Test Coat", $"DisplayHelm should be Equip Test Coat, is {u1.Equipment.DisplayHelm.Name}");
            Assert.True(u1.Equipment.Weight == 180, $"Test equipment weight is 180 but equipped weight is {u1.Equipment.Weight}");

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                Assert.True(u.Equipment[(byte)slot].Durability == 1000, "Durability of item in {slot} is not 1000");
                Assert.True(u.Equipment[(byte)slot].Guid != default, "Guid for {slot} is corrupt / not set");
                Assert.True(u.Equipment[(byte)slot].Id == InventoryTestData.TestEquipment[slot].Id, "{slot}: ID of item in inventory does not match template");

            }
        }
}
}
