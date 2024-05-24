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

internal class MapInfo
{
    private readonly byte OpCode;

    internal MapInfo()
    {
        OpCode = OpCodes.MapInfo;
    }

    internal User User { get; set; }

    internal ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);
        packet.WriteUInt16(User.Map.Id);
        packet.WriteByte((byte) (User.Map.X % 256));
        packet.WriteByte((byte) (User.Map.Y % 256));
        byte flags = 0;
        //if ((User.Map.Flags & MapFlags.Snow) == MapFlags.Snow)
        //    flags |= 1;
        //if ((User.Map.Flags & MapFlags.Rain) == MapFlags.Rain)
        //    flags |= 2;
        //if ((User.Map.Flags & MapFlags.NoMap) == MapFlags.NoMap)
        //    flags |= 64;
        //if ((User.Map.Flags & MapFlags.Winter) == MapFlags.Winter)
        //    flags |= 128;
        packet.WriteByte(flags);
        packet.WriteByte((byte) (User.Map.X / 256));
        packet.WriteByte((byte) (User.Map.Y / 256));
        packet.WriteByte((byte) (User.Map.Checksum % 256));
        packet.WriteByte((byte) (User.Map.Checksum / 256));
        packet.WriteString8(User.Map.Name);

        return packet;
    }
}