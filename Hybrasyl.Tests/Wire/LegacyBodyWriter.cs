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
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System;
using System.Text;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The write API of the pre-DALib <c>Hybrasyl.Networking.ServerPacket</c>, lifted verbatim
///     into the test project when P5b deleted that type from the server.
/// </summary>
/// <remarks>
///     <para>
///         This is the oracle for the ~86 byte-identity assertions across P2–P3d that pin the
///         send-side conversion as wire-neutral. Those goldens work <em>because</em> this is an
///         independent reimplementation: the expected bytes are produced by the code Hybrasyl
///         shipped before the conversion, not by DALib's writer, so a shared misunderstanding
///         between record and codec cannot make them agree.
///     </para>
///     <para>
///         Deleting it along with the production type would have discarded that evidence, and
///         rewriting the goldens as hex literals would have kept the bytes while losing the
///         structure that makes them auditable. Keeping it here costs nothing at runtime and
///         puts it where its remaining value is.
///     </para>
///     <para>
///         <strong>Do not "improve" it.</strong> Its correctness is defined as
///         byte-for-byte agreement with what the legacy builder did, quirks included — the
///         big-endian integer writes and the ASCII-only string encodings are the retail wire's
///         behaviour, not an accident to be modernized. The frame/ordinal/footer handling and
///         the <c>EncryptMethod</c> table are deliberately <em>not</em> carried over: the goldens
///         compare bodies, and framing is DALib's job now.
///     </para>
/// </remarks>
public sealed class LegacyBodyWriter
{
    private byte[] _data = [];
    private int _position;

    /// <summary>Legacy builders took the opcode at construction; kept so call sites read the same.</summary>
    public LegacyBodyWriter(byte opcode)
    {
        Opcode = opcode;
    }

    public byte Opcode { get; }

    /// <summary>The plaintext body, for comparison against a DALib record's <c>ToBody()</c>.</summary>
    public ReadOnlyMemory<byte> BodyMemory => _data.AsMemory(0, _position);

    private void Reserve(int count)
    {
        if (_position + count > _data.Length)
            Array.Resize(ref _data, _position + count);
    }

    public void Write(byte[] buffer)
    {
        Reserve(buffer.Length);
        Array.Copy(buffer, 0, _data, _position, buffer.Length);
        _position += buffer.Length;
    }

    public void WriteByte(byte value)
    {
        Reserve(1);
        _data[_position++] = value;
    }

    public void WriteSByte(sbyte value)
    {
        Reserve(1);
        _data[_position++] = (byte) value;
    }

    public void WriteBoolean(bool value)
    {
        Reserve(1);
        _data[_position++] = (byte) (value ? 1 : 0);
    }

    public void WriteInt16(short value)
    {
        Reserve(2);
        _data[_position++] = (byte) (value >> 8);
        _data[_position++] = (byte) value;
    }

    public void WriteUInt16(ushort value)
    {
        Reserve(2);
        _data[_position++] = (byte) (value >> 8);
        _data[_position++] = (byte) value;
    }

    public void WriteInt32(int value)
    {
        Reserve(4);
        _data[_position++] = (byte) (value >> 24);
        _data[_position++] = (byte) (value >> 16);
        _data[_position++] = (byte) (value >> 8);
        _data[_position++] = (byte) value;
    }

    public void WriteUInt32(uint value)
    {
        Reserve(4);
        _data[_position++] = (byte) (value >> 24);
        _data[_position++] = (byte) (value >> 16);
        _data[_position++] = (byte) (value >> 8);
        _data[_position++] = (byte) value;
    }

    /// <summary>Length-prefixed by <c>value.Length</c> (chars), not by encoded byte count — as the original did.</summary>
    public void WriteStringWithLength(string value)
    {
        WriteByte((byte) value.Length);
        WriteAscii(value);
    }

    public void WriteString(string value) => WriteAscii(value);

    public void WriteString8(string value)
    {
        var buffer = Encoding.ASCII.GetBytes(value);
        Reserve(1 + buffer.Length);
        _data[_position++] = (byte) buffer.Length;
        Array.Copy(buffer, 0, _data, _position, buffer.Length);
        _position += buffer.Length;
    }

    public void WriteString16(string value)
    {
        var buffer = Encoding.ASCII.GetBytes(value);
        Reserve(2 + buffer.Length);
        _data[_position++] = (byte) (buffer.Length >> 8);
        _data[_position++] = (byte) buffer.Length;
        Array.Copy(buffer, 0, _data, _position, buffer.Length);
        _position += buffer.Length;
    }

    private void WriteAscii(string value)
    {
        var buffer = Encoding.ASCII.GetBytes(value);
        Reserve(buffer.Length);
        Array.Copy(buffer, 0, _data, _position, buffer.Length);
        _position += buffer.Length;
    }
}
