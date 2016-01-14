/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using FastMember;
using Hybrasyl.Enums;
using Hybrasyl.Properties;
using log4net;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Hybrasyl.Objects
{
    public class Item : VisibleObject
    {
        public int TemplateId { get; private set; }

        /// <summary>
        /// Check to see if a specified user can equip an item. Returns a boolean indicating whether
        /// the item can be equipped and if not, sets the message reference to contain an appropriate
        /// message to be sent to the user.
        /// </summary>
        /// <param name="userobj">User object to check for meeting this item's requirements.</param>
        /// <param name="message">A reference that will be used in the case of failure to set an appropriate error message.</param>
        /// <returns></returns>
        public bool CheckRequirements(User userobj, out String message)
        {
            // We check a variety of conditions and return the first failure.

            message = string.Empty;

            // Check class

            if (userobj.Class != Class && Class != Class.Peasant)
            {
                message = userobj.Class == Class.Peasant ? "Perhaps one day you'll know how to use such things." : "Your path has forbidden itself from using such vulgar implements.";
            }

            // Check level / AB

            if (userobj.Level < Level || (Ability != 0 && userobj.Ability < Ability))
            {
                message = "You can't even lift it above your head, let alone wield it!";
            }

            // Check gender

            if (Sex != 0 && (Sex != userobj.Sex))
            {
                message = "You conclude this garment would look much better on someone else.";
            }

            // Check if user is equipping a shield while holding a two-handed weapon

            if (EquipmentSlot == ClientItemSlots.Shield && userobj.Equipment.Weapon != null && userobj.Equipment.Weapon.WeaponType == WeaponType.TwoHanded)
            {
                message = "You can't equip a shield with a two-handed weapon.";
            }

            // Check if user is equipping a two-handed weapon while holding a shield

            if (EquipmentSlot == ClientItemSlots.Weapon && WeaponType == WeaponType.TwoHanded && userobj.Equipment.Shield != null)
            {
                message = "You can't equip a two-handed weapon with a shield.";
            }

            // Check mastership

            if (Master && !userobj.IsMaster)
            {
                message = "Perhaps one day you'll know how to use such things.";
            }

            if (UniqueEquipped && userobj.Equipment.Find(Name) != null)
            {
                message = "You can't equip more than one of these.";
            }

            return message == string.Empty; 
        }

        private XML.Items.ItemType Template => World.Items[TemplateId];

        public new string Name => Template.name;

        public new ushort Sprite => Template.properties.appearance.sprite;

        public ushort EquipSprite
        {
            get
            {
                if (Template.properties.appearance.equipsprite == -1)
                    return Template.properties.appearance.sprite;
                return Template.properties.appearance.equipsprite;
            }
        }

        public ItemType ItemType
        {
            get
            {
                if (Template.properties.equipment != null)
                    return ItemType.Equipment;
                return Template.properties.use != null ? ItemType.CanUse : ItemType.CannotUse;
            }
        }

        public WeaponType WeaponType
        {
            get
            {
                if (Template.properties.equipment.weapontypeSpecified)
                    return (WeaponType) Template.properties.equipment.weapontype;
                return WeaponType.None;
            }
        }
        public byte EquipmentSlot => Convert.ToByte(Template.properties.equipment.slot);
        public int Weight => Template.properties.physical.weight;
        public int MaximumStack => Template.properties.stackable.max;
        public bool Stackable => Template.Stackable;

        public uint MaximumDurability => Template.properties.physical.durability;

        public byte Level => Template.properties.restrictions.level.min;
        public byte Ability => (byte)Template.properties.restrictions.ab.min;
        public Class Class => (Class) Template.properties.restrictions.@class;
        public Sex Sex => (Sex)Template.properties.restrictions.gender;

        public int BonusHp => Template.properties.stateffects.@base.hp;
        public int BonusMp => Template.properties.stateffects.@base.mp;
        public sbyte BonusStr => Template.properties.stateffects.@base.str;
        public sbyte BonusInt => Template.properties.stateffects.@base.@int;
        public sbyte BonusWis => Template.properties.stateffects.@base.wis;
        public sbyte BonusCon => Template.properties.stateffects.@base.con;
        public sbyte BonusDex => Template.properties.stateffects.@base.dex;
        public sbyte BonusDmg => Template.properties.stateffects.combat.dmg;
        public sbyte BonusHit => Template.properties.stateffects.combat.hit;
        public sbyte BonusAc => Template.properties.stateffects.combat.ac;
        public sbyte BonusMr => Template.properties.stateffects.combat.mr;
        public sbyte BonusRegen => Template.properties.stateffects.combat.regen;
        public byte Color => Convert.ToByte(Template.properties.appearance.color);

        public byte BodyStyle => Convert.ToByte(Template.properties.appearance.bodystyle);

        public Element Element
        {
            get
            {
                if (WeaponType == WeaponType.None)
                    return (Element) Template.properties.stateffects.element.defense;
                return (Element) Template.properties.stateffects.element.offense;
            }
        }
        public ushort MinLDamage => Template.properties.damage.large.min;
        public ushort MaxLDamage => Template.properties.damage.large.max;
        public ushort MinSDamage => Template.properties.damage.small.min;
        public ushort MaxSDamage => Template.properties.damage.small.max;
        public ushort DisplaySprite => Template.properties.appearance.displaysprite;

        public uint Value => Template.properties.physical.value;

        public sbyte Regen => Template.properties.stateffects.combat.regen;

        public bool Enchantable => Template.properties.flags.HasFlag(XML.Items.ItemFlags.enchantable);

        public bool Consecratable => Template.properties.flags.HasFlag(XML.Items.ItemFlags.consecratable);

        public bool Tailorable => Template.properties.flags.HasFlag(XML.Items.ItemFlags.tailorable);

        public bool Smithable => Template.properties.flags.HasFlag(XML.Items.ItemFlags.smithable);

        public bool Exchangeable => Template.properties.flags.HasFlag(XML.Items.ItemFlags.exchangeable);

        public bool Master => Template.properties.flags.HasFlag(XML.Items.ItemFlags.master);

        public bool Unique => Template.properties.flags.HasFlag(XML.Items.ItemFlags.unique);

        public bool UniqueEquipped => Template.properties.flags.HasFlag(XML.Items.ItemFlags.uniqueequipped);

        public bool IsVariant => Template.IsVariant;

        public XML.Items.ItemType ParentItem => Template.ParentItem;

        public XML.Items.VariantType CurrentVariant => Template.CurrentVariant;

        public XML.Items.ItemType GetVariant(int variantId)
        {
            return Template.Variants[variantId];
        }

        public int Count { get; set; }

        public uint Durability { get; set; }

        public void Invoke(User trigger)
        {
            /*
            try
            {
                World.ScriptMethod(ScriptName, this, trigger);
            }
            catch
            {
                trigger.SendMessage("It doesn't work.", 3);
            }
             * */
            trigger.SendMessage("Not implemented.", 3);
        }

        public Item(int id, World world)
        {
            World = world;
            TemplateId = id;
            Durability = MaximumDurability;
            Count = 1;
        }

        // Simple copy constructor for an item, mostly used when we split a stack and it results
        // in the creation of a new item.
        public Item(Item previousItem)
        {
            World = previousItem.World;
            TemplateId = previousItem.TemplateId;
            Durability = previousItem.Durability;
            Count = previousItem.Count;
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                user.SendVisibleItem(this);
            }
        }
    }
}


namespace Hybrasyl.XML.Items
{
    
    public partial class ItemType
    {
        [System.Xml.Serialization.XmlIgnore]
        public bool IsVariant { get; set; }

        [System.Xml.Serialization.XmlIgnore]
        public ItemType ParentItem { get; set; }

        [System.Xml.Serialization.XmlIgnore]
        public bool Stackable
        {
            get { return properties.stackable != null; }
        }

        [System.Xml.Serialization.XmlIgnore]
        public VariantType CurrentVariant { get; set; }

        [System.Xml.Serialization.XmlIgnore]
        public Dictionary<int, ItemType> Variants { get; set; }

        public int Id
        {
            get
            {
                unchecked
                {
                    if (properties.appearance.displayspriteSpecified && properties.appearance.displaysprite > 0)
                    {
                        return 31 * name.GetHashCode() * (properties.restrictions.gender.GetHashCode() + 1) *
                        properties.appearance.displaysprite.GetHashCode();
                    }
                    return 31 * name.GetHashCode() * (properties.restrictions.gender.GetHashCode() + 1);
                }
            }
        }
    }
    public partial class VariantType
    {
               
        //[System.Xml.Serialization.XmlIgnore]
        public static ILog Logger =
    LogManager.GetLogger(
        System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public void ResolveVariant(ItemType itemType)
        {
            //Logger.DebugFormat("Logging some variant stuff.");
            if (properties != null)
                Console.WriteLine("hi");
            foreach (var variantObject in properties.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public |
                                                                        BindingFlags.Instance))
            {
                Console.WriteLine("variantobject contains {0}", variantObject);
            }
        }
    }
}

