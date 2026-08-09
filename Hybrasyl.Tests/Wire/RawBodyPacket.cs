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
using DALib.Networking.Crypto;
using DALib.Networking.Wire;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Carries an arbitrary body through DALib's codec, so a crypto round-trip can be exercised
///     over bytes chosen by the test rather than over whatever some typed record happens to emit.
/// </summary>
/// <remarks>
///     <para>
///         This is the <c>RawBodyServerPacket</c> bridge, kept as a test fixture when the conversion
///         deleted it from the server. In production its job was to let unconverted send sites
///         encode through DALib; with every site converted there is nothing left to bridge. Its
///         remaining value is that <c>CryptoRoundTrip</c> needs to encrypt and decrypt bodies it
///         controls — including edge lengths a real packet would never produce.
///     </para>
///     <para>
///         Deliberately unregistered (no <c>[ServerOpcode]</c>): encoding never consults the parse
///         dispatch tables, and registering it would collide with the real record for that opcode.
///     </para>
/// </remarks>
internal sealed record RawBodyPacket : DALib.Networking.Wire.ServerPacket
{
    private readonly byte _opcode;

    internal RawBodyPacket(byte opcode, ReadOnlyMemory<byte> body)
    {
        _opcode = opcode;
        Body = body;
    }

    public ReadOnlyMemory<byte> Body { get; }

    public override byte Opcode => _opcode;

    public override void WriteBody(IPacketWriter writer)
    {
        writer.WriteBytes(Body.Span);

        // The legacy GenerateFooter inner padding, reproduced so the round-trip covers the same
        // shape the pre-conversion emit produced. Retail sends no S->C inner padding, so if that
        // delta is ever taken this goes with it.
        switch (CryptoState.GetServerEncryptMethod(_opcode))
        {
            case EncryptMethod.Normal:
                writer.WriteByte(0x00);
                break;
            case EncryptMethod.MD5Key:
                writer.WriteByte(0x00);
                writer.WriteByte(_opcode);
                break;
        }
    }
}
