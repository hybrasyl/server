using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl;
using Hybrasyl.Xml;
using Xunit;

namespace HybrasylTests
{
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
        public void RestrictionCheckABLevel()
        {
            var item = Fixture.TestEquipment[EquipmentSlot.Armor];
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
            Assert.True(equipment.CheckRequirements(Fixture.TestUser, out string m1), $"Equipment min level is 50, player level is 50, CheckRequirements failed with {m1}");
            Assert.True(Fixture.TestUser.AddEquipment(equipment, (byte)EquipmentSlot.Armor), "Equipment level is 50, player level is 50, AddEquipment failed");
            Assert.True(Fixture.TestUser.RemoveEquipment((byte)EquipmentSlot.Armor), "Failed to unequip equipment");
            Fixture.TestUser.Stats.Level = 49;
            Assert.False(equipment.CheckRequirements(Fixture.TestUser, out string m2), "Equipment min level is 50, player level is 49, CheckRequirements succeeded");
            Fixture.TestUser.Stats.Level = 99;
            Assert.False(equipment.CheckRequirements(Fixture.TestUser, out string m3), "Equipment max level is 90, player level is 99, CheckRequirements succeeded");


        }


    }
}
