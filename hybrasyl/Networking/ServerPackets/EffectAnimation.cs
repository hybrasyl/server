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

internal class EffectAnimation
{
    private readonly byte OpCode;

    internal EffectAnimation()
    {
        OpCode = OpCodes.SpellAnimation;
    }

    internal uint TargetId { get; set; }
    internal uint? SourceId { get; set; }
    internal uint TargetAnimation { get; set; }
    internal uint? SourceAnimation { get; set; }
    internal short Speed { get; set; }

    internal ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);
        var position = packet.Position;
        packet.WriteUInt32(TargetId);
        packet.WriteUInt32(SourceId ?? 0);
        packet.WriteUInt16((ushort) TargetAnimation);
        packet.WriteUInt16((ushort) (SourceAnimation ?? 0));
        packet.WriteInt16(Speed);
        packet.WriteInt32(0);
        return packet;
    }
}