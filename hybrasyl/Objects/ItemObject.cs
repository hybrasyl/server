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
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */
 
using Hybrasyl.Enums;
using Hybrasyl.Scripting;
using Hybrasyl.Threading;
using System;

namespace Hybrasyl.Objects
{
    public class ItemObject : VisibleObject
    {
        public string TemplateId { get; private set; }

        /// <summary>
        /// Check to see if a specified user can equip an ItemObject. Returns a boolean indicating whether
        /// the ItemObject can be equipped and if not, sets the message reference to contain an appropriate
        /// message to be sent to the user.
        /// </summary>
        /// <param name="userobj">User object to check for meeting this ItemObject's requirements.</param>
        /// <param name="message">A reference that will be used in the case of failure to set an appropriate error message.</param>
        /// <returns></returns>
        public bool CheckRequirements(User userobj, out string message)
        {
            // We check a variety of conditions and return the first failure.

            message = string.Empty;

            // Check gender

            if (Gender != 0 && (Gender != userobj.Gender))
            {
                message = "You conclude this garment would look much better on someone else.";
                return false;
            }

            // Check class

            if (userobj.Class != Class && Class != Xml.Class.Peasant)
            {
                message = userobj.Class == Xml.Class.Peasant ? "Perhaps one day you'll know how to use such things." : "Your path has forbidden itself from using such vulgar implements.";
                return false;
            }

            // Check level / AB

            if (userobj.Stats.Level < Level || (Ability != 0 && userobj.Stats.Ability < Ability))
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

            if (EquipmentSlot == ClientItemSlots.Shield && userobj.Equipment.Weapon != null && userobj.Equipment.Weapon.WeaponType == Xml.WeaponType.TwoHand)
            {
                message = "You can't equip a shield with a two-handed weapon.";
                return false;
            }

            // Check if user is equipping a two-handed weapon while holding a shield

            if (EquipmentSlot == ClientItemSlots.Weapon && (WeaponType == Xml.WeaponType.TwoHand || WeaponType == Xml.WeaponType.Staff) && userobj.Equipment.Shield != null)
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

        private Xml.Item Template => World.WorldData.Get<Xml.Item>(TemplateId);

        public new string Name => Template.Name;

        public new ushort Sprite => Template.Properties.Appearance.Sprite;

        public bool Usable => Template.Properties.Use != null;
        public Xml.Use Use => Template.Properties.Use;

        public ushort EquipSprite => Template.Properties.Appearance.EquipSprite == 0 ? Template.Properties.Appearance.Sprite : Template.Properties.Appearance.EquipSprite;
           
        public ItemObjectType ItemObjectType
        {
            get
            {
                if (Template.Properties.Equipment != null)
                    return ItemObjectType.Equipment;
                else if (Template.Properties.Use != null)
                    return ItemObjectType.CanUse;
                return ItemObjectType.CannotUse;
            }
        }

        public Xml.WeaponType WeaponType => Template.Properties.Equipment.WeaponType;
        public byte EquipmentSlot => Convert.ToByte(Template.Properties.Equipment.Slot);
        public int Weight => Template.Properties.Physical.Weight;
        public int MaximumStack => Template.MaximumStack;
        public bool Stackable => Template.Stackable;

        public uint MaximumDurability => Template.Properties.Physical.Durability;

        public byte Level => Template.Level;
        public byte Ability => Template.Ability;
        public Xml.Class Class => Template.Class;
        public Xml.Gender Gender => Template.Gender;

        public int BonusHp => Template.BonusHp;
        public int BonusMp => Template.BonusMp;
        public sbyte BonusStr => Template.BonusStr;
        public sbyte BonusInt => Template.BonusInt;
        public sbyte BonusWis => Template.BonusWis;
        public sbyte BonusCon => Template.BonusCon;
        public sbyte BonusDex => Template.BonusDex;
        public sbyte BonusDmg => Template.BonusDmg;
        public sbyte BonusHit => Template.BonusHit;
        public sbyte BonusAc => Template.BonusAc;
        public sbyte BonusMr => Template.BonusMr;
        public sbyte BonusRegen => Template.BonusRegen;
        public byte Color => Convert.ToByte(Template.Properties.Appearance.Color);

        public byte BodyStyle => Convert.ToByte(Template.Properties.Appearance.BodyStyle);

        public Xml.Element Element => Template.Element;

        public ushort MinLDamage => Template.MinLDamage;
        public ushort MaxLDamage => Template.MaxLDamage;
        public ushort MinSDamage => Template.MinSDamage;
        public ushort MaxSDamage => Template.MaxSDamage;
        public ushort DisplaySprite => Template.Properties.Appearance.DisplaySprite;

        public uint Value => Template.Properties.Physical.Value;

        public sbyte Regen => Template.Regen;

        public bool Enchantable => Template.Properties.Flags.HasFlag(Xml.ItemFlags.Enchantable);

        public bool Consecratable => Template.Properties.Flags.HasFlag(Xml.ItemFlags.Consecratable);

        public bool Tailorable => Template.Properties.Flags.HasFlag(Xml.ItemFlags.Tailorable);

        public bool Smithable => Template.Properties.Flags.HasFlag(Xml.ItemFlags.Smithable);

        public bool Exchangeable => Template.Properties.Flags.HasFlag(Xml.ItemFlags.Exchangeable);

        public bool Master => Template.Properties.Flags.HasFlag(Xml.ItemFlags.Master);

        public bool Perishable => Template.Properties.Physical.Perishable;

        public bool Unique => Template.Properties.Flags.HasFlag(Xml.ItemFlags.Unique);

        public bool UniqueEquipped => Template.Properties.Flags.HasFlag(Xml.ItemFlags.UniqueEquipped);

        public bool IsVariant => Template.IsVariant;

        public Xml.Item ParentItem => Template.ParentItem;

        public Xml.Variant CurrentVariant => Template.CurrentVariant;

        private Lockable<int> _count { get; set; }
        public int Count
        {
            get { return _count.Value; }
            set { _count.Value = value; }
        }

        private Lockable<uint> _durability { get; set; }

        public uint Durability
        {
            get { return _durability.Value; }
            set { _durability.Value = value; }
        }

        public void Invoke(User trigger)
        {
            // Run through all the different potential uses. We allow combinations of any
            // use specified in the item XML.
            GameLog.InfoFormat($"User {trigger.Name}: used item {Name}");
            if (Use.Script != null)
            {
                Script invokeScript;
                if (!World.ScriptProcessor.TryGetScript(Use.Script, out invokeScript))
                {
                    trigger.SendSystemMessage("It doesn't work.");
                    return;
                }

                if (!invokeScript.ExecuteFunction("OnUse", trigger, null, this))
                {
                    trigger.SendSystemMessage("It doesn't work.");
                    return;
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

        public ItemObject(string id, World world)
        {
            World = world;
            TemplateId = id;
            _durability = new Lockable<uint>(MaximumDurability);
            _count = new Lockable<int>(1);
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



    


