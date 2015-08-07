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

            message = String.Empty;

            // Check class

            if (userobj.Class != Class && Class != Class.Peasant)
            {
                if (userobj.Class == Class.Peasant)
                    message = "Perhaps one day you'll know how to use such things.";
                else
                    message = "Your path has forbidden itself from using such vulgar implements.";
                return false;
            }

            // Check level / AB

            if (userobj.Level < Level || (Ability != 0 && userobj.Ability < Ability))
            {
                message = "You can't even lift it above your head, let alone wield it!";
                return false;
            }

            // Check gender

            if (Sex != 0 && (Sex != userobj.Sex))
            {
                message = "You conclude this garment would look much better on someone else.";
                return false;
            }

            return true;
        }

        private XML.Items.ItemType Template
        {
            get { return World.Items[TemplateId]; }
        }

        new public string Name
        {
            get { return Template.name; }
        }

        new public ushort Sprite
        {
            get { return Template.properties.appearance.sprite; }
        }

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
        public byte EquipmentSlot
        {
            get { return Convert.ToByte(Template.properties.equipment.slot); }
        }
        public int Weight
        {
            get { return Template.properties.physical.weight; }
        }
        public int MaximumStack
        {
            get { return Template.properties.stackable.max; }
        }
        public bool Stackable
        {
            get { return Template.Stackable; }
        }

        public uint MaximumDurability
        {
            get { return Template.properties.physical.durability; }
        }

        public byte Level
        {
            get { return Template.properties.restrictions.level.min; }
        }
        public byte Ability
        {
            get { return (byte)Template.properties.restrictions.ab.min; }
        }
        public Class Class
        {
            get { return (Class) Template.properties.restrictions.@class; }
        }
        public Sex Sex
        {
            get { return (Sex)Template.properties.restrictions.gender; }
        }

        public int BonusHp
        {
            get { return Template.properties.stateffects.@base.hp; }
        }
        public int BonusMp
        {
            get { return Template.properties.stateffects.@base.mp; }
        }
        public sbyte BonusStr
        {
            get { return Template.properties.stateffects.@base.str; }
        }
        public sbyte BonusInt
        {
            get { return Template.properties.stateffects.@base.@int; }
        }
        public sbyte BonusWis
        {
            get { return Template.properties.stateffects.@base.wis; }
        }
        public sbyte BonusCon
        {
            get { return Template.properties.stateffects.@base.con; }
        }
        public sbyte BonusDex
        {
            get { return Template.properties.stateffects.@base.dex; }
        }
        public sbyte BonusDmg
        {
            get { return Template.properties.stateffects.combat.dmg; }
        }
        public sbyte BonusHit
        {
            get { return Template.properties.stateffects.combat.hit; }
        }
        public sbyte BonusAc
        {
            get { return Template.properties.stateffects.combat.ac; }
        }
        public sbyte BonusMr
        {
            get { return Template.properties.stateffects.combat.mr; }
        }
        public sbyte BonusRegen
        {
            get { return Template.properties.stateffects.combat.regen; }
        }
        public byte Color
        {
            get { return Convert.ToByte(Template.properties.appearance.color); }
        }

        public byte BodyStyle
        {
            get { return Convert.ToByte(Template.properties.appearance.bodystyle); }
        }

        public Element Element
        {
            get
            {
                if (WeaponType == WeaponType.None)
                    return (Element) Template.properties.stateffects.element.defense;
                return (Element) Template.properties.stateffects.element.offense;
            }
        }
        public ushort MinLDamage
        {
            get { return Template.properties.damage.large.min; }
        }
        public ushort MaxLDamage
        {
            get { return Template.properties.damage.large.max; }
        }
        public ushort MinSDamage
        {
            get { return Template.properties.damage.small.min; }
        }
        public ushort MaxSDamage
        {
            get { return Template.properties.damage.small.max; }
        }
        public ushort DisplaySprite
        {
            get { return Template.properties.appearance.displaysprite; }
        }

        public uint Value
        {
            get { return Template.properties.physical.value; }
        }

        public sbyte Regen
        {
            get { return Template.properties.stateffects.combat.regen; }
        }

        public bool Enchantable
        {
            get { return Template.properties.flags.HasFlag(XML.Items.ItemFlags.enchantable); }
        }

        public bool Consecratable
        {
            get { return Template.properties.flags.HasFlag(XML.Items.ItemFlags.consecratable); }
        }

        public bool Tailorable
        {
            get { return Template.properties.flags.HasFlag(XML.Items.ItemFlags.tailorable);  }
        }

        public bool Smithable
        {
            get { return Template.properties.flags.HasFlag(XML.Items.ItemFlags.smithable); }
        }

        public bool Exchangeable
        {
            get { return Template.properties.flags.HasFlag(XML.Items.ItemFlags.exchangeable); }
        }

        public bool IsVariant
        {
            get { return Template.IsVariant; }
        }

        public XML.Items.ItemType ParentItem
        {
            get { return Template.ParentItem; }
        }

        public XML.Items.VariantType CurrentVariant
        {
            get { return Template.CurrentVariant; }
        }

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
                    return 31 * name.GetHashCode() * properties.restrictions.gender.GetHashCode() *
                           properties.appearance.GetHashCode();                  
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
            Logger.DebugFormat("Logging some variant stuff.");
            foreach (var variantObject in properties.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public |
                                                                        BindingFlags.Instance))
            {
                Console.WriteLine("variantobject contains {0}", variantObject);
            }
        }
    }
}

