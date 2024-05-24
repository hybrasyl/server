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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;

namespace Hybrasyl.Networking.ServerPackets;

internal class PlayerShop
{
    private readonly byte OpCode;

    internal PlayerShop()
    {
        OpCode = OpCodes.PlayerShop;
    }

    public uint ShopId { get; set; }
    public uint ShopGold { get; set; }
    public string ShopName { get; set; }
    public bool NameOnly { get; set; }
    public (uint id, ItemObject item, ushort count, uint price)[] ShopItems { get; set; }

    public ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);
        packet.WriteByte(0x01);
        packet.WriteUInt32(ShopId);
        if (NameOnly)
        {
            packet.WriteByte(0x04);
            packet.WriteString8(ShopName);
        }
        else
        {
            packet.WriteByte(0x00);
            packet.WriteUInt32(ShopGold);
            packet.WriteByte(0x64); // unknown
            packet.WriteByte((byte) ShopItems.Length);
            foreach (var listing in ShopItems)
            {
                packet.WriteUInt32(listing.id);
                packet.WriteUInt16(listing.item.Sprite);
                packet.WriteByte(listing.item.Color);
                packet.WriteString8(listing.item.Name);
                packet.WriteUInt32(listing.item.DisplayDurability);
                packet.WriteUInt16(listing.count);
                packet.WriteUInt32(listing.price);
                packet.WriteUInt32(0);
            }
        }

        return packet;
    }
}