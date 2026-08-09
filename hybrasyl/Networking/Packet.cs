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

using Hybrasyl.Internals.Logging;
using System;
using System.Security.Cryptography;

namespace Hybrasyl.Networking;

[Serializable]
public abstract class Packet
{

    private static SHA256 hashAlgorithm = SHA256.Create();
    protected int _position;

    protected byte[] Data = [];

    protected Packet()
    {
        TransmitDelay = 0;
    }

    public byte Opcode { get; protected set; }
    public byte Ordinal { get; set; }
    public int TransmitDelay { get; set; }

    public int Position => _position;

    public abstract EncryptMethod EncryptMethod { get; }

    public bool ShouldEncrypt => EncryptMethod != EncryptMethod.None;

    public string Hash()
    {
        var hash = hashAlgorithm.ComputeHash(Data);
        return BitConverter.ToString(hash)[..8];
    }

    public void DumpPacket()
    {
        // Dump the packet to the console.
        GameLog.Debug("Dumping packet: {Opcode:X2}", Opcode);
        GameLog.Debug(ToString());
    }

    public byte[] ToArray()
    {
        var shouldEncrypt = ShouldEncrypt ? 5 : 4;
        var buffer = new byte[Data.Length + shouldEncrypt];
        buffer[0] = 0xAA;
        buffer[1] = (byte)((buffer.Length - 3) / 256);
        buffer[2] = (byte)(buffer.Length - 3);
        buffer[3] = Opcode;
        buffer[4] = Ordinal;

        try
        {
            Array.Copy(Data, 0, buffer, shouldEncrypt, Data.Length);
        }
        catch (Exception)
        {
            Array.Resize(ref buffer, Data.Length + shouldEncrypt + 2);
        }
        finally
        {
            Array.Copy(Data, 0, buffer, shouldEncrypt, Data.Length);
        }

        return buffer;
    }

    public static explicit operator byte[](Packet packet) => packet.ToArray();

    public override string ToString() => BitConverter.ToString(ToArray());

    public int Seek(int offset, PacketSeekOrigin origin)
    {
        if (origin == PacketSeekOrigin.Begin) _position = 0;
        if (origin == PacketSeekOrigin.End) _position = Data.Length;
        _position += offset;
        if (_position < 0) _position = 0;
        if (_position > Data.Length) _position = Data.Length;
        return _position;
    }
}