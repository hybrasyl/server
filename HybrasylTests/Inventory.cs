using System;
using Xunit;
using Hybrasyl;
using Hybrasyl.Xml;
using Hybrasyl.Objects;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Threading;
using Hybrasyl.Scripting;

namespace HybrasylTests
{
    [Collection("Hybrasyl")]
    public class Inventory
    {
        public static HybrasylFixture Fixture;

        public Inventory(HybrasylFixture fixture)
        {
            Fixture = fixture;
        }

        public static IEnumerable<object[]> XmlItems()
        {
            yield return new object[] { HybrasylFixture.TestItem, HybrasylFixture.StackableTestItem };

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
            HybrasylFixture.TestUser.Inventory.Clear();
            Assert.True(HybrasylFixture.TestUser.Inventory.Count == 0, "Inventory cleared but count is non-zero");
            Assert.True(HybrasylFixture.TestUser.Inventory.ToList().Count == 0, "Inventory cleared but enumerated count is non-zero");
            Assert.True(HybrasylFixture.TestUser.Inventory.Weight == 0, "Inventory cleared but weight is non-zero");
            HybrasylFixture.TestUser.Inventory.RecalculateWeight();
            Assert.True(HybrasylFixture.TestUser.Inventory.Weight == 0, "Inventory cleared but weight is non-zero after recalculation");
        }


        [Theory]
        [MemberData(nameof(XmlItems))]
        public void AddRetrieveAndRemoveItems(params Item[] items)
        {
            HybrasylFixture.TestUser.Inventory.Clear();

            var numItems = 0;
            var startWeight = 0;
            var expectedWeight = 0;
            foreach (var i in items)
            {
                Assert.True(expectedWeight == startWeight, 
                    $"Weight calculation is incorrect (expected {expectedWeight}, got {startWeight}");
                var itemObj = Game.World.CreateItem(i.Id);
                Assert.True(HybrasylFixture.TestUser.AddItem(itemObj), $"Item {itemObj.Name} could not be added to inventory");
                expectedWeight += itemObj.Weight;
                startWeight = expectedWeight;
                Assert.True(HybrasylFixture.TestUser.Inventory.Weight == expectedWeight, 
                    $"Item added to inventory: start weight {startWeight}, item weight {itemObj.Weight}, expected {expectedWeight}, weight is {HybrasylFixture.TestUser.Inventory.Weight}");
                numItems++;
                Assert.True(HybrasylFixture.TestUser.Inventory.TryGetValue(i.Id, out var invObj), 
                    $"Inventory had {i.Name} added but is not found by template ID");
                var objList = HybrasylFixture.TestUser.Inventory.TryGetValueByName(i.Name, out var invObjName);
                Assert.True(objList, $"Inventory had {i.Name} added but TryGetValueByName fails");
                Assert.True(invObjName.Contains(itemObj), $"Inventory had {i.Name} but return list from TryGetValueByName does not contain it");
                
                Assert.True(HybrasylFixture.TestUser.Inventory.Count == numItems, 
                    $"Inventory has had {numItems} items added, but count ({HybrasylFixture.TestUser.Inventory.Count}) is not equivalent");
                Assert.True(invObj.Name == i.Name, 
                    $"TryGetValue: Inventory contains an item {invObj.Name} but it isn't the test item {i.Name}");
                Assert.True(invObjName.Select(x => x.Name == itemObj.Name).Count() == invObjName.Count,
                    $"TryGetValueByName: Inventory contains a singular item {itemObj.Name} but multiple items returned");
                Assert.True(HybrasylFixture.TestUser.Inventory.Contains(i.Id),
                    "Contains: Inventory should contain {i.Name} but returned false");
                Assert.True(HybrasylFixture.TestUser.Inventory.ContainsName(i.Name),
                    "ContainsName: Inventory should contain {i.Name} but returned false");
            }
        }

        [Theory]
        [MemberData(nameof(XmlItems))]
        public void AddItemsToInventory(params Item[] item)
        {
            HybrasylFixture.TestUser.Inventory.Clear();

            for (var x = 1; x < 6; x++)
            {
                HybrasylFixture.TestUser.AddItem(Game.World.CreateItem(item[0].Id));
            }

            Assert.True(HybrasylFixture.TestUser.Inventory.Count == 5, $"Inventory with five items should have count of 5, count is {HybrasylFixture.TestUser.Inventory.Count}");
            Assert.True(HybrasylFixture.TestUser.Inventory[1].Name == "Test Item", "(First slot client based) Inventory contains an item but it isn't a test item");
            Assert.True(HybrasylFixture.TestUser.Inventory[5].Name == "Test Item", "(Fifth slot client based) Inventory contains an item but it isn't a test item");
            Assert.True(HybrasylFixture.TestUser.Inventory[0, false].Name == "Test Item", "(First slot server based) Inventory contains an item but it isn't a test item");
        }


        [Theory]
        [MemberData(nameof(XmlItems))]
        public void MoveItemBetweenSlotsInInventory(params Item[] item)
        {
            HybrasylFixture.TestUser.Inventory.Clear();
            Assert.True(HybrasylFixture.TestUser.AddItem(Game.World.CreateItem(item[0].Id)),
                $"Adding item {item[0].Name} to inventory failed");
            Assert.True(HybrasylFixture.TestUser.Inventory.Count == 1, "Inventory with one item should have count of 1");
            Assert.True(HybrasylFixture.TestUser.Inventory.Swap(1, 5), "Swap {item[0].Name} from slot 0 to slot 5 failed");
            Assert.True(HybrasylFixture.TestUser.Inventory[5] != null, "Swap to slot 5 failed, slot 5 is null");
            Assert.True(HybrasylFixture.TestUser.Inventory[1] == null, "Swap to slot 5 failed, slot 1 still contains an item");
            Assert.True(HybrasylFixture.TestUser.Inventory.Count == 1, "Inventory with one item should have count of 1");
            Assert.True(HybrasylFixture.TestUser.Inventory[5].Name == "Test Item", "Slot 5 contains an item but it isn't a test item");
            Assert.True(HybrasylFixture.TestUser.Inventory[4, false].Name == "Test Item", "(First slot server based) Inventory contains an item but it isn't a test item");
        }

        [Theory]
        [MemberData(nameof(XmlItems))]
        public void FullInventoryShouldBeFull(params Item[] item)
        {
            HybrasylFixture.TestUser.Inventory.Clear();
            for (var x = 0; x < HybrasylFixture.InventorySize; x++)
            {
                Assert.True(HybrasylFixture.TestUser.AddItem(Game.World.CreateItem(item[0].Id)), $"Adding item #{x} to inventory failed");
            }

            Assert.True(HybrasylFixture.TestUser.Inventory.IsFull, "Inventory that is full should return isFull == true");
            Assert.False(HybrasylFixture.TestUser.Inventory.AddItem(Game.World.CreateItem(item[0].Id)), "Adding an item to a full inventory should fail");          
        }

        [Theory]
        [MemberData(nameof(XmlItems))]
        public void WeightCalculations(params Item[] item)
        {
            HybrasylFixture.TestUser.Inventory.Clear();
            for (var x = 0; x < 10; x++)
            {
                Assert.True(HybrasylFixture.TestUser.AddItem(Game.World.CreateItem(item[0].Id)), $"Adding item #{x} to inventory failed");
            }

            Assert.False(HybrasylFixture.TestUser.Inventory.IsFull, "Non-full inventory should not be full");
            Assert.True(HybrasylFixture.TestUser.Inventory.Weight == item[0].Properties.Physical.Weight * 10, 
                $"Ten items of weight {item[0].Properties.Physical.Weight} were added, but total weight is {HybrasylFixture.TestUser.Inventory.Weight}");
        }

        // TODO: theory
        [Fact]
        public void EquipTestEquipment()
        {
            HybrasylFixture.TestUser.Inventory.Clear();
            HybrasylFixture.TestUser.Equipment.Clear();

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (slot == EquipmentSlot.None || slot == EquipmentSlot.Gauntlet ||
                    slot == EquipmentSlot.Ring) continue;
                var itemObject = Game.World.CreateItem(HybrasylFixture.TestEquipment[slot].Id);
                Assert.True(HybrasylFixture.TestUser.AddEquipment(itemObject, (byte) slot, false),
                    $"Adding equipment to {slot} failed");
            }

            // TODO: make slots uniform. haha no really

            Assert.True(HybrasylFixture.TestUser.Equipment.Weapon.Name == $"Equip Test Weapon",
                $"Weapon should be Equip Test Weapon, is {HybrasylFixture.TestUser.Equipment.Weapon.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Armor.Name == $"Equip Test Armor",
                $"Armor should be Equip Test Armor, is {HybrasylFixture.TestUser.Equipment.Armor.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Shield.Name == $"Equip Test Shield",
                $"Shield should be Equip Test Shield, is {HybrasylFixture.TestUser.Equipment.Shield.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Helmet.Name == $"Equip Test Helmet",
                $"Helmet should be Equip Test Helmet, is {HybrasylFixture.TestUser.Equipment.Helmet.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Earring.Name == $"Equip Test Earring",
                $"Earring should be Equip Test Earring, is {HybrasylFixture.TestUser.Equipment.Earring.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Necklace.Name == $"Equip Test Necklace",
                $"Necklace should be Equip Test Necklace, is {HybrasylFixture.TestUser.Equipment.Necklace.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.LRing.Name == $"Equip Test LeftHand",
                $"LRing should be Equip Test LeftHand, is {HybrasylFixture.TestUser.Equipment.LRing.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.RRing.Name == $"Equip Test RightHand",
                $"RRing should be Equip Test RightHand, is {HybrasylFixture.TestUser.Equipment.RRing.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.LGauntlet.Name == $"Equip Test LeftArm",
                $"LGauntlet should be Equip Test LeftArm, is {HybrasylFixture.TestUser.Equipment.LGauntlet.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.RGauntlet.Name == $"Equip Test RightArm",
                $"RGauntlet should be Equip Test RightArm, is {HybrasylFixture.TestUser.Equipment.RGauntlet.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Belt.Name == $"Equip Test Waist",
                $"Belt should be Equip Test Waist, is {HybrasylFixture.TestUser.Equipment.Belt.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Greaves.Name == $"Equip Test Leg",
                $"Greaves should be Equip Test Leg, is {HybrasylFixture.TestUser.Equipment.Greaves.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Boots.Name == $"Equip Test Foot",
                $"Boots should be Equip Test Foot, is {HybrasylFixture.TestUser.Equipment.Boots.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.FirstAcc.Name == $"Equip Test FirstAcc",
                $"FirstAcc should be Equip Test FirstAcc, is {HybrasylFixture.TestUser.Equipment.FirstAcc.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.SecondAcc.Name == $"Equip Test SecondAcc",
                $"SecondAcc should be Equip Test SecondAcc, is {HybrasylFixture.TestUser.Equipment.SecondAcc.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.ThirdAcc.Name == $"Equip Test ThirdAcc",
                $"ThirdAcc should be Equip Test ThirdAcc, is {HybrasylFixture.TestUser.Equipment.ThirdAcc.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Overcoat.Name == $"Equip Test Trousers",
                $"Overcoat should be Equip Test Trousers, is {HybrasylFixture.TestUser.Equipment.Overcoat.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.DisplayHelm.Name == $"Equip Test Coat",
                $"DisplayHelm should be Equip Test Coat, is {HybrasylFixture.TestUser.Equipment.DisplayHelm.Name}");
            Assert.True(HybrasylFixture.TestUser.Equipment.Weight == 18,
                $"Test equipment weight is 18 but equipped weight is {HybrasylFixture.TestUser.Equipment.Weight}");
        }

        [Fact]
        public void InventorySerialization()
        {
            HybrasylFixture.TestUser.Inventory.Clear();
            HybrasylFixture.TestUser.Equipment.Clear();

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (slot == EquipmentSlot.None || slot == EquipmentSlot.Gauntlet || slot == EquipmentSlot.Ring) continue;
                var itemObject = Game.World.CreateItem(HybrasylFixture.TestEquipment[slot].Id);
                Assert.True(HybrasylFixture.TestUser.AddEquipment(itemObject, (byte)slot, false), $"Adding equipment to {slot} failed");
            }

            HybrasylFixture.TestUser.Save();

            Assert.True(Game.World.WorldData.TryGetUser("TestUser", out User u1), "Test user should exist after save but can't be found");

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

            u1.Equipment.RecalculateWeight();
            Assert.True(u1.Equipment.Weight == 18, $"Test equipment weight is 18 but equipped weight is {u1.Equipment.Weight}");

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (slot == EquipmentSlot.None || slot == EquipmentSlot.Gauntlet || slot == EquipmentSlot.Ring) continue;
                Assert.True(u1.Equipment[(byte)slot].Durability == 1000, "Durability of item in {slot} is not 1000");
                Assert.True(u1.Equipment[(byte)slot].Guid != default, "Guid for {slot} is corrupt / not set");
                Assert.True(u1.Equipment[(byte)slot].TemplateId == HybrasylFixture.TestEquipment[slot].Id, "{slot}: ID of item in inventory does not match template");
            }
        }

        [Theory]
        [MemberData(nameof(XmlItems))]
        public void InventoryContains(params Item[] items)
        {
            HybrasylFixture.TestUser.Inventory.Clear();
            foreach (var item in items)
            {
                var itemObj = Game.World.CreateItem(item.Id);
                itemObj.Count = item.Properties.Stackable.Max;
                Assert.True(HybrasylFixture.TestUser.Inventory.AddItem(itemObj), "Adding {item.Name} to inventory failed");
                Assert.True(HybrasylFixture.TestUser.Inventory.Contains(item.Id), "Inventory should contain {item.Name}, Contains failed");
                Assert.True(HybrasylFixture.TestUser.Inventory.ContainsName(item.Name), "Inventory should contain {item.Name}, ContainsName failed");
                for (var x = 0; x < item.Properties.Stackable.Max; x++)
                {
                     Assert.True(HybrasylFixture.TestUser.Inventory.Contains(item.Name, x), $"Inventory should contain {x} of {item.Name}, Contains with qty check failed");
                }

            }
        }
    }
}
