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
using MapFlags = Hybrasyl.Xml.Objects.MapFlags;
using Hybrasyl.Objects;

namespace Hybrasyl.Networking.ServerPackets;

internal class MapInfo
{
    private readonly byte OpCode;

    internal MapInfo()
    {
        OpCode = OpCodes.MapInfo;
    }

    internal required User User { get; set; }

    internal ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);
        // MapInfo is only built after the user's map is assigned during map load.
        var map = User.Location.Map!;
        packet.WriteUInt16(map.Id);
        packet.WriteByte((byte) (map.X % 256));
        packet.WriteByte((byte) (map.Y % 256));
        // I hate this
        byte flags = 0;
        if (map.Flags.HasFlag(MapFlags.Snow))
            flags |= 1;
        if (map.Flags.HasFlag(MapFlags.Rain))
            flags |= 2;
        if (map.Flags.HasFlag(MapFlags.Dark)) {
            flags |= 1;
            flags |= 2;
        }
        if (map.Flags.HasFlag(MapFlags.NoMap))
            flags |= 64;
        if (map.Flags.HasFlag(MapFlags.Snow))
            flags |= 128;
        packet.WriteByte(flags);
        packet.WriteByte((byte) (map.X / 256));
        packet.WriteByte((byte) (map.Y / 256));
        packet.WriteByte((byte) (map.Checksum % 256));
        packet.WriteByte((byte) (map.Checksum / 256));
        packet.WriteString8(map.Name);

        return packet;
    }
}