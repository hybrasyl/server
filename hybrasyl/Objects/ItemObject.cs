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
using Hybrasyl.Items;
using Hybrasyl.Properties;
using log4net;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Hybrasyl.Objects
{
    public class ItemObject : VisibleObject
    {
        public int TemplateId { get; private set; }

        /// <summary>
        /// Check to see if a specified user can equip an ItemObject. Returns a boolean indicating whether
        /// the ItemObject can be equipped and if not, sets the message reference to contain an appropriate
        /// message to be sent to the user.
        /// </summary>
        /// <param name="userobj">User object to check for meeting this ItemObject's requirements.</param>
        /// <param name="message">A reference that will be used in the case of failure to set an appropriate error message.</param>
        /// <returns></returns>
        public bool CheckRequirements(User userobj, out String message)
        {
            // We check a variety of conditions and return the first failure.

            message = string.Empty;

            // Check gender

            if (Sex != 0 && (Sex != userobj.Sex))
            {
                message = "You conclude this garment would look much better on someone else.";
                return false;
            }

            // Check class

            if (userobj.Class != Class && Class != Enums.Class.Peasant)
            {
                message = userobj.Class == Enums.Class.Peasant ? "Perhaps one day you'll know how to use such things." : "Your path has forbidden itself from using such vulgar implements.";
                return false;
            }

            // Check level / AB

            if (userobj.Level < Level || (Ability != 0 && userobj.Ability < Ability))
            {
                message = "You require more insight.";
                return false;
            }

            if (userobj.Equipment.Weight + Weight > userobj.MaximumWeight/2)
            {
                message = "You can't even lift it above your head, let alone wield it!";
                return false;
            }

            // Check if user is equipping a shield while holding a two-handed weapon

            if (EquipmentSlot == ClientItemSlots.Shield && userobj.Equipment.Weapon != null && userobj.Equipment.Weapon.WeaponType == Items.WeaponType.TwoHand)
            {
                message = "You can't equip a shield with a two-handed weapon.";
                return false;
            }

            // Check if user is equipping a two-handed weapon while holding a shield

            if (EquipmentSlot == ClientItemSlots.Weapon && (WeaponType == Items.WeaponType.TwoHand || WeaponType == Items.WeaponType.Staff) && userobj.Equipment.Shield != null)
            {
                message = "You can't equip a two-handed weapon with a shield.";
                return false;
            }

            // Check mastership

            if (Master && !userobj.IsMaster)
            {
                message = "Perhaps one day you'll know how to use such things.";
                return false;
            }

            if (UniqueEquipped && userobj.Equipment.Find(Name) != null)
            {
                message = "You can't equip more than one of these.";
                return false;
            }

            return true;
        }

        private Item Template => World.Items[TemplateId];

        public new string Name => Template.Name;

        public new ushort Sprite => Template.Properties.Appearance.Sprite;

        public ItemPropertiesUse Use => Template.Properties.Use;

        public ushort EquipSprite
        {
            get
            {
                if (Template.Properties.Appearance.EquipSprite == 0)
                    return Template.Properties.Appearance.Sprite;
                return Template.Properties.Appearance.EquipSprite;
            }
        }

        public ItemObjectType ItemObjectType
        {
            get
            {
                if (Template.Properties.Equipment != null)
                    return ItemObjectType.Equipment;
                else
                    return Template.Properties.Use != null ? ItemObjectType.CanUse : ItemObjectType.CannotUse;

            }
        }

        public WeaponType WeaponType => Template.Properties.Equipment.WeaponType;
        public byte EquipmentSlot => Convert.ToByte(Template.Properties.Equipment.Slot);
        public int Weight => Template.Properties.Physical.Weight;
        public int MaximumStack => Template.Properties.Stackable.Max;
        public bool Stackable => Template.Properties.Stackable.Max > 1;

        public uint MaximumDurability => Template.Properties.Physical.Durability;

        public byte Level => Template.Properties.Restrictions.Level.Min;
        public byte Ability => (byte)Template.Properties.Restrictions.Ab.Min;
        public Enums.Class Class => (Enums.Class) Template.Properties.Restrictions.@Class;
        public Sex Sex => (Sex)Template.Properties.Restrictions.Gender;

        public int BonusHp => Template.Properties.StatEffects.@Base.Hp;
        public int BonusMp => Template.Properties.StatEffects.@Base.Mp;
        public sbyte BonusStr => Template.Properties.StatEffects.@Base.Str;
        public sbyte BonusInt => Template.Properties.StatEffects.@Base.@Int;
        public sbyte BonusWis => Template.Properties.StatEffects.@Base.Wis;
        public sbyte BonusCon => Template.Properties.StatEffects.@Base.Con;
        public sbyte BonusDex => Template.Properties.StatEffects.@Base.Dex;
        public sbyte BonusDmg => Template.Properties.StatEffects.Combat.Dmg;
        public sbyte BonusHit => Template.Properties.StatEffects.Combat.Hit;
        public sbyte BonusAc => Template.Properties.StatEffects.Combat.Ac;
        public sbyte BonusMr => Template.Properties.StatEffects.Combat.Mr;
        public sbyte BonusRegen => Template.Properties.StatEffects.Combat.Regen;
        public byte Color => Convert.ToByte(Template.Properties.Appearance.Color);

        public byte BodyStyle => Convert.ToByte(Template.Properties.Appearance.BodyStyle);

        public Enums.Element Element
        {
            get
            {
                if (WeaponType == WeaponType.None)
                    return (Enums.Element) Template.Properties.StatEffects.Element.Defense;
                return (Enums.Element) Template.Properties.StatEffects.Element.Offense;
            }
        }
        public ushort MinLDamage => Template.Properties.Damage.Large.Min;
        public ushort MaxLDamage => Template.Properties.Damage.Large.Max;
        public ushort MinSDamage => Template.Properties.Damage.Small.Min;
        public ushort MaxSDamage => Template.Properties.Damage.Small.Max;
        public ushort DisplaySprite => Template.Properties.Appearance.DisplaySprite;

        public uint Value => Template.Properties.Physical.Value;

        public sbyte Regen => Template.Properties.StatEffects.Combat.Regen;

        public bool Enchantable => Template.Properties.Flags.HasFlag(ItemFlags.Enchantable);

        public bool Consecratable => Template.Properties.Flags.HasFlag(ItemFlags.Consecratable);

        public bool Tailorable => Template.Properties.Flags.HasFlag(ItemFlags.Tailorable);

        public bool Smithable => Template.Properties.Flags.HasFlag(ItemFlags.Smithable);

        public bool Exchangeable => Template.Properties.Flags.HasFlag(ItemFlags.Exchangeable);

        public bool Master => Template.Properties.Flags.HasFlag(ItemFlags.Master);

        public bool Perishable => Template.Properties.Physical.Perishable;

        public bool Unique => Template.Properties.Flags.HasFlag(ItemFlags.Unique);

        public bool UniqueEquipped => Template.Properties.Flags.HasFlag(ItemFlags.UniqueEquipped);

        public bool IsVariant => Template.IsVariant;

        public Item ParentItem => Template.ParentItem;

        public Variant CurrentVariant => Template.CurrentVariant;

        public Item GetVariant(int variantId)
        {
            return Template.Variants[variantId];
        }

        public int Count { get; set; }

        public uint Durability { get; set; }

        public void Invoke(User trigger)
        {
            trigger.SendMessage("Not implemented.", 3);
            // Run through all the different potential uses. We allow combinations of any
            // use specified in the item XML.
            Logger.InfoFormat($"User {trigger.Name}: used item {Name}");
            if (Use.Script != null)
            {
                Script invokeScript;
                if (!World.ScriptProcessor.TryGetScript(Use.Script, out invokeScript))
                {
                    trigger.SendSystemMessage("It doesn't work.");
                    return;
                }
                    
                try
                {
                    invokeScript.ExecuteScriptableFunction("OnUse", new HybrasylWorldObject(this), new HybrasylUser(trigger));
                }
                catch (Exception e)
                {
                    trigger.SendSystemMessage("It doesn't work.");
                    Logger.ErrorFormat($"User {trigger.Name}, item {Name}: exception {e}");
                }              
            }            
            if (Use.Effect != null)
            {
               trigger.SendEffect(trigger.Id, Use.Effect.Id, Use.Effect.Speed); 
            }
            if (Use.PlayerEffect != null)
            {
                if (Use.PlayerEffect.Gold > 0)
                    trigger.AddGold(new Gold((uint)Use.PlayerEffect.Gold));
                if (Use.PlayerEffect.Hp > 0)
                    trigger.Heal(Use.PlayerEffect.Hp);
                if (Use.PlayerEffect.Mp > 0)
                    trigger.RegenerateMp(Use.PlayerEffect.Mp);
                if (Use.PlayerEffect.Xp > 0)
                    trigger.GiveExperience((uint)Use.PlayerEffect.Xp);
                trigger.UpdateAttributes(StatUpdateFlags.Current|StatUpdateFlags.Experience);
            }
            if (Use.Sound != null)
            {
                trigger.SendSound((byte) Use.Sound.Id);
            }
            if (Use.Teleport != null)
            {
                trigger.Teleport(Use.Teleport.Value, Use.Teleport.X, Use.Teleport.Y);
            }
            if (Use.Consumed)
            {
                Count--;
            }
        }

        public ItemObject(int id, World world)
        {
            World = world;
            TemplateId = id;
            Durability = MaximumDurability;
            Count = 1;
        }

        // Simple copy constructor for an ItemObject, mostly used when we split a stack and it results
        // in the creation of a new ItemObject.
        public ItemObject(ItemObject previousItemObject)
        {
            World = previousItemObject.World;
            TemplateId = previousItemObject.TemplateId;
            Durability = previousItemObject.Durability;
            Count = previousItemObject.Count;
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



    


