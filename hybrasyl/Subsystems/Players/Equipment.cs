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
using Newtonsoft.Json;

namespace Hybrasyl.Subsystems.Players;

[JsonConverter(typeof(EquipmentConverter))]
public class Equipment : Inventory
{
    public new const byte DefaultSize = 18;

    public Equipment(byte size) : base(size) { }

    public bool RingEquipped => LRing != null || RRing != null;
    public bool GauntletEquipped => LGauntlet != null || RGauntlet != null;


    public ItemObject this[EquipmentSlot slot] => this[(byte) slot];

    public List<Tuple<ushort, byte>> GetEquipmentDisplayList()
    {
        var returnList = new List<Tuple<ushort, byte>>();

        foreach (var slot in Enum.GetValues(typeof(ItemSlots)))
            switch (slot)
            {
                // Work around a very weird edge case in the client
                case ItemSlots.Foot:
                    returnList.Add(Items[(byte) ItemSlots.FirstAcc] == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort) (0x8000 + Items[(byte) ItemSlots.FirstAcc].EquipSprite),
                            Items[
                                (byte) ItemSlots.FirstAcc].Color));
                    break;
                case ItemSlots.FirstAcc:
                    returnList.Add(Items[(byte) ItemSlots.Foot] == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort) (0x8000 + Items[(byte) ItemSlots.Foot].EquipSprite),
                            Items[
                                (byte) ItemSlots.Foot].Color));
                    break;
                case ItemSlots.None:
                case ItemSlots.Ring:
                case ItemSlots.Gauntlet:
                    break;
                default:
                    returnList.Add(Items[(byte) slot] == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort) (0x8000 + Items[(byte) slot].EquipSprite),
                            Items[(byte) slot].Color));
                    break;
            }

        return returnList;
    }

    #region Equipment Properties

    public ItemObject Weapon => Items[(byte) ItemSlots.Weapon];

    public ItemObject Armor => Items[(byte) ItemSlots.Armor];

    public ItemObject Shield => Items[(byte) ItemSlots.Shield];

    public ItemObject Helmet => Items[(byte) ItemSlots.Helmet];

    public ItemObject Earring => Items[(byte) ItemSlots.Earring];

    public ItemObject Necklace => Items[(byte) ItemSlots.Necklace];

    public ItemObject LRing => Items[(byte) ItemSlots.LHand];

    public ItemObject RRing => Items[(byte) ItemSlots.RHand];

    public ItemObject LGauntlet => Items[(byte) ItemSlots.LArm];

    public ItemObject RGauntlet => Items[(byte) ItemSlots.RArm];

    public ItemObject Belt => Items[(byte) ItemSlots.Waist];

    public ItemObject Greaves => Items[(byte) ItemSlots.Leg];

    public ItemObject Boots => Items[(byte) ItemSlots.Foot];

    public ItemObject FirstAcc => Items[(byte) ItemSlots.FirstAcc];

    public ItemObject Overcoat => Items[(byte) ItemSlots.Trousers];

    public ItemObject DisplayHelm => Items[(byte) ItemSlots.Coat];

    public ItemObject SecondAcc => Items[(byte) ItemSlots.SecondAcc];

    public ItemObject ThirdAcc => Items[(byte) ItemSlots.ThirdAcc];

    #endregion Equipment Properties
}