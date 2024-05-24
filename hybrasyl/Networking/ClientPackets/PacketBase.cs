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

using System.IO;
using System.Text;
using Hybrasyl.Networking;

namespace Hybrasyl.ClientPackets;

public abstract class PacketBase
{
    public MemoryStream Data = new();
    public virtual byte Opcode { get; set; }
    public byte Ordinal { get; set; } = 0xFF;
    public bool ShouldEncrypt => Opcode != 0x00 && Opcode != 0x10;

    public byte Header => 0xAA;
    public byte[] Footer => new byte[] { 0x68, 0x79, 0x62 };

    public static explicit operator ClientPacket(PacketBase pb)
    {
        var stream = new MemoryStream();
        var shouldEncrypt = pb.ShouldEncrypt ? 5 : 4;
        var totalLength = pb.Data.Length + shouldEncrypt;
        stream.WriteByte(pb.Header);
        stream.WriteByte((byte) (totalLength - 3 / 256));
        stream.WriteByte((byte) (totalLength - 3));
        stream.WriteByte(pb.Opcode);
        if (pb.ShouldEncrypt)
            stream.WriteByte(pb.Ordinal);
        stream.Write(pb.Data.ToArray());
        stream.Write(pb.Footer);

        return new ClientPacket(stream.ToArray());
    }

    public void WriteByte(byte value)
    {
        Data.WriteByte(value);
    }

    public void WriteInt16(short value)
    {
        Data.WriteByte((byte) (value >> 8));
        Data.WriteByte((byte) value);
    }

    public void WriteUInt16(ushort value)
    {
        Data.WriteByte((byte) (value >> 8));
        Data.WriteByte((byte) value);
    }

    public void WriteInt32(int value)
    {
        Data.WriteByte((byte) (value >> 24));
        Data.WriteByte((byte) (value >> 16));
        Data.WriteByte((byte) (value >> 8));
        Data.WriteByte((byte) value);
    }

    public void WriteUInt32(uint value)
    {
        Data.WriteByte((byte) (value >> 24));
        Data.WriteByte((byte) (value >> 16));
        Data.WriteByte((byte) (value >> 8));
        Data.WriteByte((byte) value);
    }

    public void WriteString(string value, bool writeLength = false)
    {
        if (writeLength)
            Data.WriteByte((byte) value.Length);
        var buffer = Encoding.ASCII.GetBytes(value);
        Data.Write(buffer, 0, buffer.Length);
    }
}