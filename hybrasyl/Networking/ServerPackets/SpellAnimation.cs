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

internal class SpellAnimation
{
    private readonly byte OpCode;

    internal SpellAnimation()
    {
        OpCode = OpCodes.SpellAnimation;
    }

    internal uint Id { get; set; }
    internal uint SenderId { get; set; }
    internal ushort AnimationId { get; set; }
    internal ushort SenderAnimationId { get; set; }
    internal ushort Speed { get; set; }
    internal ushort X { get; set; }
    internal ushort Y { get; set; }

    internal ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);
        packet.WriteByte(0x00);
        if (Id != 0)
        {
            packet.WriteUInt32(Id);
            packet.WriteUInt32(SenderId == 0 ? Id : SenderId);
            packet.WriteUInt16(AnimationId);
            packet.WriteUInt16(SenderAnimationId == 0 ? ushort.MinValue : SenderAnimationId);
            packet.WriteUInt16(Speed);
            packet.WriteByte(0x00);
        }
        else
        {
            packet.WriteUInt32(uint.MinValue);
            packet.WriteUInt16(AnimationId);
            packet.WriteUInt16(Speed);
            packet.WriteUInt16(X);
            packet.WriteUInt16(Y);
        }

        return packet;
    }
}