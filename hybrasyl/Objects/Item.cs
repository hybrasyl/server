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

        private item Template
        {
            get { return World.Items[TemplateId]; }
        }

        new public string Name
        {
            get { return Template.name; }
        }

        new public ushort Sprite
        {
            get { return (ushort)Template.sprite; }
        }

        public ushort EquipSprite
        {
            get
            {
                if (Template.equip_sprite == -1)
                    return (ushort)Template.sprite;
                return (ushort)Template.equip_sprite;
            }
        }

        public ItemType ItemType
        {
            get { return (ItemType)Template.item_type; }
        }
        public WeaponType WeaponType
        {
            get { return (WeaponType)Template.weapon_type; }
        }
        public byte EquipmentSlot
        {
            get { return (byte)Template.equip_slot; }
        }
        public int Weight
        {
            get { return Template.weight; }
        }
        public int MaximumStack
        {
            get { return Template.max_stack; }
        }
        public bool Stackable
        {
            get { return MaximumStack > 1; }
        }

        public uint MaximumDurability
        {
            get { return (uint)Template.max_durability; }
        }

        public string ScriptName
        {
            get { return Template.script_name; }
        }

        public byte Level
        {
            get { return (byte)Template.level; }
        }
        public byte Ability
        {
            get { return (byte)Template.ab; }
        }
        public Class Class
        {
            get { return (Class)Template.class_type; }
        }
        public Sex Sex
        {
            get { return (Sex)Template.sex; }
        }

        public int BonusHp
        {
            get { return (int)Template.hp; }
        }
        public int BonusMp
        {
            get { return (int)Template.mp; }
        }
        public sbyte BonusStr
        {
            get { return (sbyte)Template.str; }
        }
        public sbyte BonusInt
        {
            get { return (sbyte)Template.@int; }
        }
        public sbyte BonusWis
        {
            get { return (sbyte)Template.wis; }
        }
        public sbyte BonusCon
        {
            get { return (sbyte)Template.con; }
        }
        public sbyte BonusDex
        {
            get { return (sbyte)Template.dex; }
        }
        public sbyte BonusDmg
        {
            get { return (sbyte)Template.dmg; }
        }
        public sbyte BonusHit
        {
            get { return (sbyte)Template.hit; }
        }
        public sbyte BonusAc
        {
            get { return (sbyte)Template.ac; }
        }
        public sbyte BonusMr
        {
            get { return (sbyte)Template.mr; }
        }
        public sbyte BonusRegen
        {
            get { return (sbyte)Template.regen; }
        }
        public byte Color
        {
            get { return (byte)Template.color; }
        }

        public byte BodyStyle
        {
            get { return (byte)Template.bodystyle; }
        }

        public Element Element
        {
            get { return (Element)Template.element; }
        }
        public ushort MinLDamage
        {
            get { return (ushort)Template.min_l_dmg; }
        }
        public ushort MaxLDamage
        {
            get { return (ushort)Template.max_l_dmg; }
        }
        public ushort MinSDamage
        {
            get { return (ushort)Template.min_s_dmg; }
        }
        public ushort MaxSDamage
        {
            get { return (ushort)Template.max_s_dmg; }
        }
        public ushort DisplaySprite
        {
            get { return (ushort)Template.display_sprite; }
        }

        public uint Value
        {
            get { return (uint)Template.value; }
        }

        public sbyte Regen
        {
            get { return (sbyte)Template.regen; }
        }

        public bool Enchantable
        {
            get { return Template.enchantable; }
        }

        public bool Consecratable
        {
            get { return Template.consecratable; }
        }

        public bool Tailorable
        {
            get { return Template.tailorable; }
        }

        public bool Smithable
        {
            get { return Template.smithable; }
        }

        public bool Exchangeable
        {
            get { return Template.exchangeable; }
        }

        public bool IsVariant
        {
            get { return Template.IsVariant; }
        }

        public item ParentItem
        {
            get { return Template.ParentItem; }
        }

        public item_variant CurrentVariant
        {
            get { return Template.CurrentVariant; }
        }

        public item GetVariant(int variantId)
        {
            return Template.Variants[variantId];
        }

        public int Count { get; set; }

        public uint Durability { get; set; }

        public void Invoke(User trigger)
        {
            try
            {
                World.ScriptMethod(ScriptName, this, trigger);
            }
            catch
            {
                trigger.SendMessage("It doesn't work.", 3);
            }
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
        public item ParentItem { get; set; }

        [System.Xml.Serialization.XmlIgnore]
        public bool Stackable
        {
            get { return properties.stackable != null; }
        }

        [System.Xml.Serialization.XmlIgnore]
        public item_variant CurrentVariant { get; set; }

        [System.Xml.Serialization.XmlIgnore]
        public Dictionary<int, item> Variants { get; set; }        
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

        public void Copy(item source)
        {
            FieldInfo[] fields = GetType().GetFields(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                field.SetValue(this, field.GetValue(source));
            }
        }

    }
}
// Extend the item EF class with a marker noting whether or not it's a variant
// This is all pretty ugly and uh, it probably needs to be redone later

namespace Hybrasyl.Properties
{
    public partial class item
    {
        public bool IsVariant { get; set; }
        public item ParentItem { get; set; }

        public bool Stackable
        {
            get { return max_stack > 1; }
        }

        public item_variant CurrentVariant { get; set; }

        public Dictionary<int, item> Variants { get; set; }

        public void Copy(item source)
        {
            FieldInfo[] fields = GetType().GetFields(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                field.SetValue(this, field.GetValue(source));
            }
        }
    }
   
    public partial class item_variant
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly List<String> VariantPropertyResolutions = new List<String>
        {
            // These values MUST be numeric!
            "weight",
            "max_stack",
            "max_durability",
            "hp",
            "mp",
            "str",
            "int",
            "wis",
            "con",
            "dex",
            "hit",
            "ac",
            "dmg",
            "mr",
            "max_s_dmg",
            "min_s_dmg",
            "max_l_dmg",
            "min_l_dmg",
            "value",
            "regen"
        };

        public static readonly List<String> VariantPropertyReplacements = new List<String>
        {
            "level",
            "ab",
            "element",
            "bodystyle",
            "color",
            "enchantable",
            "depositable",
            "bound",
            "vendorable",
            "tailorable",
            "smithable",
            "consecratable",
            "perishable",
            "exchangeable"
        };

        public int ResolvePercentage(int basevalue, string percentage)
        {
            Match match = Constants.PercentageRegex.Match(percentage);

            int pct = Convert.ToInt32(match.Groups[2].Value);

            if (match.Groups[1].Value == "-")
            {
                if (pct > 99)
                {
                    return basevalue;
                }
            }

            return Convert.ToInt32(basevalue * (pct / 100));
        }

        public item ResolveVariant(item baseitem)
        {
            // Given a base item, return a new variant item with all our
            // modifications applied. We resolve the properties in VariantPropertyResolution
            // by modifying as appropriate (given inputs of percentages or simple ints), and
            // we replace values for properties in VariantPropertyReplacements (if they exist).

            // Make a shallow copy of the item first, so we get all the goodies
            item newitem = new item();
            newitem.Copy(baseitem);

            Logger.DebugFormat("Resolving variant {0} for item {1}", modifier, baseitem.name);

            var itemAccessor = TypeAccessor.Create(typeof(item));
            var variantAccessor = TypeAccessor.Create(typeof(item_variant));

            foreach (var property in VariantPropertyResolutions)
            {
                //var variant_value = GetType().GetProperty(property).GetValue(this, null);
                //var cur_val = (int)baseitem.GetType().GetProperty(property).GetValue(baseitem, null);

                var variantValue = variantAccessor[this, property];
                var curVal = itemAccessor[baseitem, property];

                // Is the variant a number or a percentage? Otherwise, do nothing.
                int number = 0;
                bool isInt = int.TryParse((string)variantValue, out number);
                if (isInt)
                {
                    itemAccessor[newitem, property] = ((int)curVal + number);
                    //newitem.GetType().GetProperty(property).SetValue(newitem, (cur_val + number));
                }
                else if (Constants.PercentageRegex.IsMatch((string)variantValue))
                {
                    itemAccessor[newitem, property] = ResolvePercentage((int)curVal, (string)variantValue);
                    //newitem.GetType().GetProperty(property).SetValue(newitem,
                    //  ResolvePercentage(cur_val, (string)variant_value));
                }
            }

            foreach (var property in VariantPropertyReplacements)
            {
                var value = GetType().GetProperty(property).GetValue(this, null);
                if (value != null)
                {
                    newitem.GetType().GetProperty(property).SetValue(newitem, value);
                }
            }

            newitem.ParentItem = baseitem;
            newitem.IsVariant = true;
            newitem.name = String.Format("{0} {1}", modifier, baseitem.name);
            newitem.CurrentVariant = this;

            return newitem;
        }
    }
}
