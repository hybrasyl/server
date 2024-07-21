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

namespace Hybrasyl.Networking.ServerPackets;

internal class ExchangeControl
{
    internal const string CancelMessage = "Exchange was cancelled.";
    internal const string ConfirmMessage = "You exchanged.";

    private static readonly byte OpCode =
        OpCodes.Exchange;

    internal byte Action;
    internal uint Gold;
    internal byte ItemColor;
    internal string ItemName;
    internal byte ItemSlot;
    internal ushort ItemSprite;
    internal uint RequestorId;
    internal string RequestorName;
    internal bool Side;

    internal ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);
        packet.WriteByte(Action);
        switch (Action)
        {
            case ExchangeActions.Initiate:
                {
                    packet.WriteUInt32(RequestorId);
                    packet.WriteString8(RequestorName);
                }
                break;
            case ExchangeActions.QuantityPrompt:
                {
                    packet.WriteByte(ItemSlot);
                }
                break;
            case ExchangeActions.ItemUpdate:
                {
                    packet.WriteByte((byte)(Side ? 0 : 1));
                    packet.WriteByte(ItemSlot);
                    packet.WriteUInt16((ushort)(0x8000 + ItemSprite));
                    packet.WriteByte(ItemColor);
                    packet.WriteString8(ItemName);
                }
                break;
            case ExchangeActions.GoldUpdate:
                {
                    packet.WriteByte((byte)(Side ? 0 : 1));
                    packet.WriteUInt32(Gold);
                }
                break;
            case ExchangeActions.Cancel:
                {
                    packet.WriteByte((byte)(Side ? 0 : 1));
                    packet.WriteString8(CancelMessage);
                }
                break;
            case ExchangeActions.Confirm:
                {
                    packet.WriteByte((byte)(Side ? 0 : 1));
                    packet.WriteString8(ConfirmMessage);
                }
                break;
        }

        return packet;
    }
}