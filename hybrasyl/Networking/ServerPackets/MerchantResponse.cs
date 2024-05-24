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
using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;

namespace Hybrasyl.Networking.ServerPackets;

internal class MerchantResponse
{
    private readonly byte OpCode;
    private byte Unknow4 = 2;
    internal byte Unknow7 = 1;


    internal MerchantResponse()
    {
        OpCode = OpCodes.NpcReply;
    }

    internal MerchantDialogType MerchantDialogType { get; set; }
    internal MerchantDialogObjectType MerchantDialogObjectType { get; set; }
    internal uint ObjectId { get; set; }
    internal ushort Tile1 { get; set; }
    internal byte Color1 { get; set; } //affect items only
    internal ushort Tile2 { get; set; }
    internal byte Color2 { get; set; } //affect item only
    internal byte PortraitType { get; set; } //portrait style. 0 = anime 1 = sprite
    internal byte NameLength => Convert.ToByte(Name.Length);
    internal string Name { get; set; }
    internal ushort TextLength { get; set; }
    internal string Text { get; set; }
    internal byte Slot { get; set; }
    internal uint Quantity { get; set; }

    internal MerchantOptions Options { get; set; }
    internal MerchantOptionsWithArgument OptionsWithArgument { get; set; }
    internal MerchantInput Input { get; set; }
    internal MerchantInputWithArgument InputWithArgument { get; set; }
    internal UserInventoryItems UserInventoryItems { get; set; }
    internal MerchantShopItems ShopItems { get; set; }
    internal MerchantSpells Spells { get; set; }
    internal MerchantSkills Skills { get; set; }
    internal UserSkillBook UserSkills { get; set; }
    internal UserSpellBook UserSpells { get; set; }

    internal ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);
        packet.WriteByte((byte) MerchantDialogType);
        packet.WriteByte((byte) MerchantDialogObjectType);
        packet.WriteUInt32(ObjectId);
        packet.WriteByte(0);
        packet.WriteInt16((short) Tile1);
        packet.WriteByte(0);
        packet.WriteByte(1);
        packet.WriteInt16((short) Tile1);
        packet.WriteByte(0);
        packet.WriteByte(0);
        packet.WriteString8(Name);
        packet.WriteString16(Text);
        if (MerchantDialogType == MerchantDialogType.Options)
        {
            packet.WriteByte(Options.OptionsCount);
            foreach (var opt in Options.Options)
            {
                packet.WriteString8(opt.Text);
                packet.WriteUInt16(opt.Id);
            }
        }

        if (MerchantDialogType == MerchantDialogType.OptionsWithArgument)
        {
            packet.WriteString8(OptionsWithArgument.Argument);
            packet.WriteByte(OptionsWithArgument.OptionsCount);
            foreach (var opt in OptionsWithArgument.Options)
            {
                packet.WriteString8(opt.Text);
                packet.WriteUInt16(opt.Id);
            }
        }

        if (MerchantDialogType == MerchantDialogType.Input) packet.WriteUInt16(Input.Id);
        if (MerchantDialogType == MerchantDialogType.InputWithArgument)
        {
            packet.WriteString8(InputWithArgument.Argument);
            packet.WriteUInt16(InputWithArgument.Id);
        }

        if (MerchantDialogType == MerchantDialogType.MerchantShopItems)
        {
            packet.WriteUInt16(ShopItems.Id);
            packet.WriteUInt16(ShopItems.ItemsCount);
            foreach (var item in ShopItems.Items)
            {
                packet.WriteUInt16(item.Tile);
                packet.WriteByte(item.Color);
                packet.WriteUInt32(item.Price);
                packet.WriteString8(item.Name);
                packet.WriteString8(item.Description);
            }
        }

        if (MerchantDialogType == MerchantDialogType.MerchantSkills)
        {
            packet.WriteUInt16(Skills.Id);
            packet.WriteUInt16(Skills.SkillsCount);
            foreach (var skill in Skills.Skills)
            {
                packet.WriteByte(skill.IconType);
                packet.WriteUInt16(skill.Icon);
                packet.WriteByte(skill.Color);
                packet.WriteString8(skill.Name);
            }
        }

        if (MerchantDialogType == MerchantDialogType.MerchantSpells)
        {
            packet.WriteUInt16(Spells.Id);
            packet.WriteUInt16(Spells.SpellsCount);
            foreach (var spell in Spells.Spells)
            {
                packet.WriteByte(spell.IconType);
                packet.WriteUInt16(spell.Icon);
                packet.WriteByte(spell.Color);
                packet.WriteString8(spell.Name);
            }
        }

        if (MerchantDialogType == MerchantDialogType.UserSkillBook) packet.WriteUInt16(UserSkills.Id);
        if (MerchantDialogType == MerchantDialogType.UserSpellBook) packet.WriteUInt16(UserSpells.Id);
        if (MerchantDialogType == MerchantDialogType.UserInventoryItems)
        {
            packet.WriteUInt16(UserInventoryItems.Id);
            packet.WriteByte(UserInventoryItems.InventorySlotsCount);
            foreach (var slot in UserInventoryItems.InventorySlots) packet.WriteByte(slot);
        }

        return packet;
    }
}