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
        Data = new byte[0];
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
        value = value ?? string.Empty;
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

    public void GenerateFooter()
    {
        var length = Data.Length;

        if (UseDefaultKey)
        {
            Array.Resize(ref Data, length + 1);
            Data[length++] = 0x00;
        }
        else
        {
            Array.Resize(ref Data, length + 2);
            Data[length++] = 0x00;
            Data[length++] = Opcode;
        }

        Array.Resize(ref Data, length + 3);
    }

    public void Encrypt(Client client)
    {
        var length = Data.Length - 3;

        //var bRand = (ushort)(rand.Next() % 65277 + 256);
        var bRand = (ushort) (Random.Shared.Next(65277) + 256);
        //var sRand = (byte)(rand.Next() % 155 + 100);
        var sRand = (byte) (Random.Shared.Next(155) + 100);

        byte[] key;
        switch (EncryptMethod)
        {
            case EncryptMethod.Normal:
                key = client.EncryptionKey;
                break;
            case EncryptMethod.MD5Key:
                key = client.GenerateKey(bRand, sRand);
                break;
            default:
                return;
        }

        //var key = (UseDefaultKey) ? client.EncryptionKey : client.GenerateKey(bRand, sRand);

        for (var i = 0; i < length; i++)
        {
            Data[i] ^= key[i % key.Length];
            Data[i] ^= SaltTable[client.EncryptionSeed][i / key.Length % SaltTable[client.EncryptionSeed].Length];
            if (i / key.Length % SaltTable[client.EncryptionSeed].Length != Ordinal)
                Data[i] ^= SaltTable[client.EncryptionSeed][Ordinal];
        }

        Data[length] = (byte) ((bRand % 256) ^ 0x74);
        Data[length + 1] = (byte) (sRand ^ 0x24);
        Data[length + 2] = (byte) (((bRand >> 8) % 256) ^ 0x64);
    }

    public ServerPacket Clone()
    {
        var f = ToArray();
        return new ServerPacket(f);
    }
}