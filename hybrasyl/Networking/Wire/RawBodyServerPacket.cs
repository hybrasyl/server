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

namespace Hybrasyl.Networking.Wire;

/// <summary>
///     DALib conversion bridge (Phase 1): carries a legacy ServerPacket body through
///     DALib's codec so unconverted send sites encode with DALib crypto/framing.
///     Deliberately unregistered (no [ServerOpcode] attribute) — encode never consults
///     the parse dispatch tables. Deleted in Phase 5 when the last site converts.
/// </summary>
internal sealed record RawBodyServerPacket : DALib.Networking.Wire.ServerPacket
{
    private readonly byte _opcode;

    internal RawBodyServerPacket(byte opcode, ReadOnlyMemory<byte> body)
    {
        _opcode = opcode;
        Body = body;
    }

    public ReadOnlyMemory<byte> Body { get; }

    public override byte Opcode => _opcode;

    public override void WriteBody(IPacketWriter writer)
    {
        writer.WriteBytes(Body.Span);
        // Legacy GenerateFooter inner padding, preserved for byte parity with the
        // pre-conversion emit. Retail sends no S->C inner padding (a signed-off delta,
        // pending sign-off); dropped when this delta is approved or per converted slice.
        switch (CryptoState.GetServerEncryptMethod(_opcode))
        {
            case DALib.Networking.Crypto.EncryptMethod.Normal:
                writer.WriteByte(0x00);
                break;
            case DALib.Networking.Crypto.EncryptMethod.MD5Key:
                writer.WriteByte(0x00);
                writer.WriteByte(_opcode);
                break;
        }
    }
}
