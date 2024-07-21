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

internal class HealthBar
{
    private readonly byte OpCode;

    internal HealthBar()
    {
        OpCode = OpCodes.HealthBar;
    }

    internal uint ObjId { get; set; }

    internal byte CurrentPercent { get; set; }
    internal byte? Sound { get; set; }

    internal ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);
        packet.WriteUInt32(ObjId);
        packet.WriteByte(0);
        packet.WriteByte(CurrentPercent);
        packet.WriteByte(Sound ?? 0xFF);

        return packet;
    }
}