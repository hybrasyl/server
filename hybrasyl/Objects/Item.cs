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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using Hybrasyl.Enums;
using System;
using System.Collections.Generic;

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
            message = String.Empty;

            if (userobj.Class != Class && Class != Class.Peasant)
            {
                if (userobj.Class == Class.Peasant)
                {
                    message = "Perhaps one day you'll know how to use such things.";
                }
                else
                {
                    message = "Your path has forbidden itself from using such vulgar implements.";
                }
                return false;
            }

            if (userobj.Level < Level || (Ability != 0 && userobj.Ability < Ability))
            {
                message = "You can't even lift it above your head, let alone wield it!";
                return false;
            }

            if (Sex != 0 && (Sex != userobj.Sex))
            {
                message = "You conclude this garment would look much better on someone else.";
                return false;
            }

            return true;
        }

        private items Template
        {
            get
            {
                return World.Items[TemplateId];
            }
        }

        public new string Name
        {
            get
            {
                return Template.Name;
            }
        }

        public new ushort Sprite
        {
            get
            {
                return (ushort)Template.Sprite;
            }
        }

        public ushort EquipSprite
        {
            get
            {
                if (Template.Equip_sprite == -1)
                {
                    return (ushort)Template.Sprite;
                }
                return (ushort)Template.Equip_sprite;
            }
        }

        public ItemType ItemType
        {
            get
            {
                return (ItemType)Template.Item_type;
            }
        }
        public WeaponType WeaponType
        {
            get
            {
                return (WeaponType)Template.Weapon_type;
            }
        }
        public byte EquipmentSlot
        {
            get
            {
                return (byte)Template.Equip_slot;
            }
        }
        public ushort Weight
        {
            get
            {
                return (ushort)Template.Weight;
            }
        }
        public int MaximumStack
        {
            get
            {
                return Template.Max_stack;
            }
        }
        public bool Stackable
        {
            get
            {
                return MaximumStack > 1;
            }
        }

        public uint MaximumDurability
        {
            get
            {
                return (uint)Template.Max_durability;
            }
        }

        public string ScriptName
        {
            get
            {
                return Template.Script_name;
            }
        }

        public byte Level
        {
            get
            {
                return (byte)Template.Level;
            }
        }
        public byte Ability
        {
            get
            {
                return (byte)Template.Ab;
            }
        }
        public Class Class
        {
            get
            {
                return (Class)Template.Class_type;
            }
        }
        public Sex Sex
        {
            get
            {
                return (Sex)Template.Sex;
            }
        }

        public int BonusHp
        {
            get
            {
                return (int)Template.Hp;
            }
        }
        public int BonusMp
        {
            get
            {
                return (int)Template.Mp;
            }
        }
        public sbyte BonusStr
        {
            get
            {
                return (sbyte)Template.Str;
            }
        }
        public sbyte BonusInt
        {
            get
            {
                return (sbyte)Template.Int;
            }
        }
        public sbyte BonusWis
        {
            get
            {
                return (sbyte)Template.Wis;
            }
        }
        public sbyte BonusCon
        {
            get
            {
                return (sbyte)Template.Con;
            }
        }
        public sbyte BonusDex
        {
            get
            {
                return (sbyte)Template.Dex;
            }
        }
        public sbyte BonusDmg
        {
            get
            {
                return (sbyte)Template.Dmg;
            }
        }
        public sbyte BonusHit
        {
            get
            {
                return (sbyte)Template.Hit;
            }
        }
        public sbyte BonusAc
        {
            get
            {
                return (sbyte)Template.Ac;
            }
        }
        public sbyte BonusMr
        {
            get
            {
                return (sbyte)Template.Mr;
            }
        }
        public sbyte BonusRegen
        {
            get
            {
                return (sbyte)Template.Regen;
            }
        }
        public byte Color
        {
            get
            {
                return (byte)Template.Color;
            }
        }

        public byte BodyStyle
        {
            get
            {
                return (byte)Template.Bodystyle;
            }
        }

        public Element Element
        {
            get
            {
                return (Element)Template.Element;
            }
        }
        public ushort MinLDamage
        {
            get
            {
                return (ushort)Template.Min_l_dmg;
            }
        }
        public ushort MaxLDamage
        {
            get
            {
                return (ushort)Template.Max_l_dmg;
            }
        }
        public ushort MinSDamage
        {
            get
            {
                return (ushort)Template.Min_s_dmg;
            }
        }
        public ushort MaxSDamage
        {
            get
            {
                return (ushort)Template.Max_s_dmg;
            }
        }
        public ushort DisplaySprite
        {
            get
            {
                return (ushort)Template.Display_sprite;
            }
        }

        public uint Value
        {
            get
            {
                return (uint)Template.Value;
            }
        }

        public sbyte Regen
        {
            get
            {
                return (sbyte)Template.Regen;
            }
        }

        public bool Enchantable
        {
            get
            {
                return Convert.ToBoolean(Template.Enchantable);
            }
        }

        public bool Consecratable
        {
            get
            {
                return Convert.ToBoolean(Template.Consecratable);
            }
        }

        public bool Tailorable
        {
            get
            {
                return Convert.ToBoolean(Template.Tailorable);
            }
        }

        public bool Smithable
        {
            get
            {
                return Convert.ToBoolean(Template.Smithable);
            }
        }

        public bool Exchangeable
        {
            get
            {
                return Convert.ToBoolean(Template.Exchangeable);
            }
        }

        public int Count { get; set; }

        public uint Durability { get; set; }

        public void Invoke(User trigger)
        {
        }

        public Item(int id, World world)
        {
            World = world;
            TemplateId = id;
            Durability = MaximumDurability;
            Count = 1;
        }

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
