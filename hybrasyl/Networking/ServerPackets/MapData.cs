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

using System.Collections.Generic;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;

namespace Hybrasyl.Networking.ServerPackets;

internal class MapData
{
    private readonly byte OpCode;

    internal MapData()
    {
        OpCode = OpCodes.MapData;
    }

    internal MapObject Map { get; set; }

    internal List<ServerPacket> Packets()
    {
        var ret = new List<ServerPacket>();
        var tile = 0;
        for (var row = 0; row < Map.Y; row++)
        {
            var packet = new ServerPacket(OpCode);

            packet.WriteUInt16((ushort) row);
            for (var column = 0; column < Map.X * 6; column += 2)
            {
                packet.WriteByte(Map.RawData[tile + 1]);
                packet.WriteByte(Map.RawData[tile]);
                tile += 2;
            }

            ret.Add(packet);
        }

        return ret;
    }
}