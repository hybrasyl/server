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
using Hybrasyl.XSD;
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

            if (userobj.Class != Class && Class != Enums.Class.Peasant)
            {
                message = userobj.Class == Enums.Class.Peasant ? "Perhaps one day you'll know how to use such things." : "Your path has forbidden itself from using such vulgar implements.";
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

            if (EquipmentSlot == ClientItemSlots.Shield && userobj.Equipment.Weapon != null && userobj.Equipment.Weapon.WeaponType == Enums.WeaponType.TwoHanded)
            {
                message = "You can't equip a shield with a two-handed weapon.";
            }

            // Check if user is equipping a two-handed weapon while holding a shield

            if (EquipmentSlot == ClientItemSlots.Weapon && WeaponType == Enums.WeaponType.TwoHanded && userobj.Equipment.Shield != null)
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

        private XSD.ItemType Template => World.Items[TemplateId];

        public new string Name => Template.Name;

        public new ushort Sprite => Template.Properties.Appearance.Sprite;

        public ushort EquipSprite
        {
            get
            {
                if (Template.Properties.Appearance.Equipsprite == -1)
                    return Template.Properties.Appearance.Sprite;
                return Template.Properties.Appearance.Equipsprite;
            }
        }

        public Enums.ItemType ItemType
        {
            get
            {
                if (Template.Properties.Equipment != null)
                    return Enums.ItemType.Equipment;
                return Template.Properties.Use != null ? Enums.ItemType.CanUse : Enums.ItemType.CannotUse;
            }
        }

        public Enums.WeaponType WeaponType
        {
            get
            {
                if (Template.Properties.Equipment.WeapontypeSpecified)
                    return (Enums.WeaponType) Template.Properties.Equipment.Weapontype;
                return Enums.WeaponType.None;
            }
        }
        public byte EquipmentSlot => Convert.ToByte(Template.Properties.Equipment.Slot);
        public int Weight => Template.Properties.Physical.Weight;
        public int MaximumStack => Template.Properties.Stackable.Max;
        public bool Stackable => Template.Stackable;

        public uint MaximumDurability => Template.Properties.Physical.Durability;

        public byte Level => Template.Properties.Restrictions.Level.Min;
        public byte Ability => (byte)Template.Properties.Restrictions.Ab.Min;
        public Enums.Class Class => (Enums.Class) Template.Properties.Restrictions.@Class;
        public Sex Sex => (Sex)Template.Properties.Restrictions.Gender;

        public int BonusHp => Template.Properties.Stateffects.@Base.Hp;
        public int BonusMp => Template.Properties.Stateffects.@Base.Mp;
        public sbyte BonusStr => Template.Properties.Stateffects.@Base.Str;
        public sbyte BonusInt => Template.Properties.Stateffects.@Base.@Int;
        public sbyte BonusWis => Template.Properties.Stateffects.@Base.Wis;
        public sbyte BonusCon => Template.Properties.Stateffects.@Base.Con;
        public sbyte BonusDex => Template.Properties.Stateffects.@Base.Dex;
        public sbyte BonusDmg => Template.Properties.Stateffects.Combat.Dmg;
        public sbyte BonusHit => Template.Properties.Stateffects.Combat.Hit;
        public sbyte BonusAc => Template.Properties.Stateffects.Combat.Ac;
        public sbyte BonusMr => Template.Properties.Stateffects.Combat.Mr;
        public sbyte BonusRegen => Template.Properties.Stateffects.Combat.Regen;
        public byte Color => Convert.ToByte(Template.Properties.Appearance.Color);

        public byte BodyStyle => Convert.ToByte(Template.Properties.Appearance.Bodystyle);

        public Enums.Element Element
        {
            get
            {
                if (WeaponType == Enums.WeaponType.None)
                    return (Enums.Element) Template.Properties.Stateffects.Element.Defense;
                return (Enums.Element) Template.Properties.Stateffects.Element.Offense;
            }
        }
        public ushort MinLDamage => Template.Properties.Damage.Large.Min;
        public ushort MaxLDamage => Template.Properties.Damage.Large.Max;
        public ushort MinSDamage => Template.Properties.Damage.Small.Min;
        public ushort MaxSDamage => Template.Properties.Damage.Small.Max;
        public ushort DisplaySprite => Template.Properties.Appearance.Displaysprite;

        public uint Value => Template.Properties.Physical.Value;

        public sbyte Regen => Template.Properties.Stateffects.Combat.Regen;

        public bool Enchantable => Template.Properties.Flags.HasFlag(XSD.ItemFlags.enchantable);

        public bool Consecratable => Template.Properties.Flags.HasFlag(XSD.ItemFlags.consecratable);

        public bool Tailorable => Template.Properties.Flags.HasFlag(XSD.ItemFlags.tailorable);

        public bool Smithable => Template.Properties.Flags.HasFlag(XSD.ItemFlags.smithable);

        public bool Exchangeable => Template.Properties.Flags.HasFlag(XSD.ItemFlags.exchangeable);

        public bool Master => Template.Properties.Flags.HasFlag(XSD.ItemFlags.master);

        public bool Unique => Template.Properties.Flags.HasFlag(XSD.ItemFlags.unique);

        public bool UniqueEquipped => Template.Properties.Flags.HasFlag(XSD.ItemFlags.uniqueequipped);

        public bool IsVariant => Template.IsVariant;

        public XSD.ItemType ParentItem => Template.ParentItem;

        public XSD.VariantType CurrentVariant => Template.CurrentVariant;

        public XSD.ItemType GetVariant(int variantId)
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



    


