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

using System.Collections.Generic;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Tests;

public class XmlItemTestData
{
    static XmlItemTestData()
    {
        TestItem = new Item
        {
            Name = "Test Item"
        };
        TestItem.Properties.Stackable.Max = 1;
        TestItem.Properties.Equipment = new Xml.Objects.Equipment
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
        StackableTestItem.Properties.Equipment = new Xml.Objects.Equipment
            { WeaponType = WeaponType.None, Slot = EquipmentSlot.None };
        StackableTestItem.Properties.Physical = new Physical { Durability = 1000, Weight = 1 };
        StackableTestItem.Properties.Categories = new List<Category>
        {
            new() { Value = "nonjunk" },
            new() { Value = "stackable" },
            new() { Value = "xmlitem" }
        };
    }

    public static Item TestItem { get; set; }
    public static Item StackableTestItem { get; set; }

    public static IEnumerable<object[]> XmlItems()
    {
        yield return new object[] { TestItem, StackableTestItem };
    }

    public static IEnumerable<object[]> StackableXmlItems()
    {
        yield return new object[] { StackableTestItem };
    }
}