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

using System;
using System.Collections.Generic;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Players;

public class Equipment : Inventory
{
    public new const byte DefaultSize = 18;

    public Equipment(byte size) : base(size) { }

    public bool RingEquipped => LRing != null || RRing != null;
    public bool GauntletEquipped => LGauntlet != null || RGauntlet != null;


    public ItemObject? this[EquipmentSlot slot] => this[(byte) slot];

    public List<Tuple<ushort, byte>> GetEquipmentDisplayList()
    {
        var returnList = new List<Tuple<ushort, byte>>();

        foreach (var slot in Enum.GetValues<EquipmentSlot>())
            switch (slot)
            {
                // Work around a very weird edge case in the client
                case EquipmentSlot.Foot:
                    var footAcc = Items[(byte) EquipmentSlot.FirstAcc];
                    returnList.Add(footAcc == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort) (0x8000 + footAcc.EquipSprite), footAcc.Color));
                    break;
                case EquipmentSlot.FirstAcc:
                    var accFoot = Items[(byte) EquipmentSlot.Foot];
                    returnList.Add(accFoot == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort) (0x8000 + accFoot.EquipSprite), accFoot.Color));
                    break;
                case EquipmentSlot.None:
                case EquipmentSlot.Ring:
                case EquipmentSlot.Gauntlet:
                    break;
                default:
                    var item = Items[(byte) slot];
                    returnList.Add(item == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort) (0x8000 + item.EquipSprite), item.Color));
                    break;
            }

        return returnList;
    }

    #region Equipment Properties

    public ItemObject? Weapon => Items[(byte) EquipmentSlot.Weapon];

    public ItemObject? Armor => Items[(byte) EquipmentSlot.Armor];

    public ItemObject? Shield => Items[(byte) EquipmentSlot.Shield];

    public ItemObject? Helmet => Items[(byte) EquipmentSlot.Helmet];

    public ItemObject? Earring => Items[(byte) EquipmentSlot.Earring];

    public ItemObject? Necklace => Items[(byte) EquipmentSlot.Necklace];

    public ItemObject? LRing => Items[(byte) EquipmentSlot.LeftHand];

    public ItemObject? RRing => Items[(byte) EquipmentSlot.RightHand];

    public ItemObject? LGauntlet => Items[(byte) EquipmentSlot.LeftArm];

    public ItemObject? RGauntlet => Items[(byte) EquipmentSlot.RightArm];

    public ItemObject? Belt => Items[(byte) EquipmentSlot.Waist];

    public ItemObject? Greaves => Items[(byte) EquipmentSlot.Leg];

    public ItemObject? Boots => Items[(byte) EquipmentSlot.Foot];

    public ItemObject? FirstAcc => Items[(byte) EquipmentSlot.FirstAcc];

    public ItemObject? Overcoat => Items[(byte) EquipmentSlot.Trousers];

    public ItemObject? DisplayHelm => Items[(byte) EquipmentSlot.Coat];

    public ItemObject? SecondAcc => Items[(byte) EquipmentSlot.SecondAcc];

    public ItemObject? ThirdAcc => Items[(byte) EquipmentSlot.ThirdAcc];

    #endregion Equipment Properties
}