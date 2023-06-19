using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Xml.Objects;
using Microsoft.Extensions.ObjectPool;

namespace Hybrasyl.Tests
{
    public class XmlItemTestData
    {
        public static Item TestItem { get; set; }
        public static Item StackableTestItem { get; set; }

        static XmlItemTestData()
        {
            TestItem = new Item
            {
                Name = "Test Item"
            };
            TestItem.Properties.Stackable.Max = 1;
            TestItem.Properties.Equipment = new Hybrasyl.Xml.Objects.Equipment
                { WeaponType = WeaponType.None, Slot = EquipmentSlot.None };
            TestItem.Properties.Physical = new Physical { Durability = 1000, Weight = 1 };
            TestItem.Properties.Categories = new List<Category>
            {
                new() { Value = "junk" },
                new() { Value = "xmlitem" }
            };

            StackableTestItem = new Item
            {
                Name = "Stackable Test Item"
            };
            StackableTestItem.Properties.Stackable.Max = 20;
            StackableTestItem.Properties.Equipment = new Hybrasyl.Xml.Objects.Equipment
                { WeaponType = WeaponType.None, Slot = EquipmentSlot.None };
            StackableTestItem.Properties.Physical = new Physical { Durability = 1000, Weight = 1 };
            StackableTestItem.Properties.Categories = new List<Category>
            {
                new() { Value = "nonjunk" },
                new() { Value = "stackable" },
                new() { Value = "xmlitem" }
            };
        }

        public static IEnumerable<object[]> XmlItems()
        {
            yield return new object[] { TestItem, StackableTestItem };
        }

        public static IEnumerable<Object[]> StackableXmlItems()
        {
            yield return new object[] { StackableTestItem };
        }

    }
}
