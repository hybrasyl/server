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
using System.Text;

namespace Hybrasyl.Networking;

[Serializable]
public class ServerPacket : Packet
{
    public ServerPacket(byte opcode)
    {
        Opcode = opcode;
        Data = [];
    }

    public ServerPacket(byte[] buffer)
    {
        Opcode = buffer[3];
        if (ShouldEncrypt)
        {
            Ordinal = buffer[4];
            Data = new byte[buffer.Length - 5];
            Array.Copy(buffer, 5, Data, 0, Data.Length);
        }
        else
        {
            Data = new byte[buffer.Length - 4];
            Array.Copy(buffer, 4, Data, 0, Data.Length);
        }
    }

    public override EncryptMethod EncryptMethod
    {
        get
        {
            switch (Opcode)
            {
                case 0x00:
                case 0x03:
                case 0x40:
                case 0x7E:
                    return EncryptMethod.None;
                case 0x01:
                case 0x02:
                case 0x0A:
                case 0x56:
                case 0x60:
                case 0x62:
                case 0x66:
                case 0x6F:
                    return EncryptMethod.Normal;
                default:
                    return EncryptMethod.MD5Key;
            }
        }
    }

    // DALib conversion (Phase 1): the plaintext body written by the Write* methods, handed
    // to RawBodyServerPacket so FlushSendBuffer can encode through DALib's codec.
    internal ReadOnlyMemory<byte> BodyMemory => Data;

    public void Write(byte[] buffer)
    {
        if (_position + buffer.Length > Data.Length) Array.Resize(ref Data, _position + buffer.Length);
        Array.Copy(buffer, 0, Data, _position, buffer.Length);
        _position += buffer.Length;
    }

    public void WriteByte(byte value)
    {
        if (_position + 1 > Data.Length) Array.Resize(ref Data, _position + 1);
        Data[_position++] = value;
    }

    public void WriteSByte(sbyte value)
    {
        if (_position + 1 > Data.Length) Array.Resize(ref Data, _position + 1);
        Data[_position++] = (byte) value;
    }

    public void WriteBoolean(bool value)
    {
        if (_position + 1 > Data.Length) Array.Resize(ref Data, _position + 1);
        Data[_position++] = (byte) (value ? 1 : 0);
    }

    public void WriteInt16(short value)
    {
        if (_position + 2 > Data.Length) Array.Resize(ref Data, _position + 2);
        Data[_position++] = (byte) (value >> 8);
        Data[_position++] = (byte) value;
    }

    public void WriteUInt16(ushort value)
    {
        if (_position + 2 > Data.Length) Array.Resize(ref Data, _position + 2);
        Data[_position++] = (byte) (value >> 8);
        Data[_position++] = (byte) value;
    }

    public void WriteInt32(int value)
    {
        if (_position + 4 > Data.Length) Array.Resize(ref Data, _position + 4);
        Data[_position++] = (byte) (value >> 24);
        Data[_position++] = (byte) (value >> 16);
        Data[_position++] = (byte) (value >> 8);
        Data[_position++] = (byte) value;
    }

    public void WriteUInt32(uint value)
    {
        if (_position + 4 > Data.Length) Array.Resize(ref Data, _position + 4);
        Data[_position++] = (byte) (value >> 24);
        Data[_position++] = (byte) (value >> 16);
        Data[_position++] = (byte) (value >> 8);
        Data[_position++] = (byte) value;
    }

    public void WriteStringWithLength(string value)
    {
        WriteByte((byte) value.Length);
        var buffer = Encoding.ASCII.GetBytes(value);
        if (_position + buffer.Length > Data.Length) Array.Resize(ref Data, _position + buffer.Length);
        Array.Copy(buffer, 0, Data, _position, buffer.Length);
        _position += buffer.Length;
    }

    public void WriteString(string value)
    {
        var buffer = Encoding.ASCII.GetBytes(value);
        if (_position + buffer.Length > Data.Length) Array.Resize(ref Data, _position + buffer.Length);
        Array.Copy(buffer, 0, Data, _position, buffer.Length);
        _position += buffer.Length;
    }

    public void WriteString8(string value)
    {
        var buffer = Encoding.ASCII.GetBytes(value);
        if (_position + 1 + buffer.Length > Data.Length) Array.Resize(ref Data, _position + 1 + buffer.Length);
        Data[_position++] = (byte) buffer.Length;
        Array.Copy(buffer, 0, Data, _position, buffer.Length);
        _position += buffer.Length;
    }

    public void WriteString16(string value)
    {
        var buffer = Encoding.ASCII.GetBytes(value);
        if (_position + 2 + buffer.Length > Data.Length) Array.Resize(ref Data, _position + 2 + buffer.Length);
        Data[_position++] = (byte) (buffer.Length >> 8);
        Data[_position++] = (byte) buffer.Length;
        Array.Copy(buffer, 0, Data, _position, buffer.Length);
        _position += buffer.Length;
    }

    public ServerPacket Clone()
    {
        var f = ToArray();
        return new ServerPacket(f);
    }
}