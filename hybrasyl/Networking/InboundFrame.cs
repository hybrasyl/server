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
using System.IO;

namespace Hybrasyl.Networking;

/// <summary>
///     One complete 0xAA-framed C→S frame, popped off the receive buffer but not yet decrypted
///     or parsed.
/// </summary>
/// <remarks>
///     Framing stays on this side rather than being handed to <c>PacketCodec.TryGetClientPacket</c>
///     directly, because the codec throws on an unknown opcode without reporting how many bytes
///     the frame occupied. Popping the whole frame first means a rejected packet is dropped
///     without desyncing the stream behind it.
/// </remarks>
public readonly record struct InboundFrame(byte Opcode, ReadOnlyMemory<byte> Wire)
{
    /// <summary>Frame layout is [0xAA][u16 length][opcode][body...], so the opcode is at index 3.</summary>
    private const int OpcodeIndex = 3;

    public static InboundFrame FromWire(ReadOnlyMemory<byte> wire) =>
        new(wire.Span[OpcodeIndex], wire);
}

/// <summary>
///     A received packet after decryption and dialog de-obfuscation: the opcode plus the
///     plaintext body, with the frame header, ordinal and any obfuscation header stripped.
/// </summary>
/// <remarks>
///     Handlers parse this body into the DALib record they expect rather than being handed an
///     already-parsed record, because 0x39 cannot be parsed without server state: its tail shape
///     depends on the menu the server last displayed, and the codec's opcode-keyed parse yields
///     the bare select form with the merchant tail silently dropped. Keeping the body as the unit
///     of dispatch means one rule for every opcode instead of a carve-out for that family.
/// </remarks>
public readonly record struct InboundPacket(byte Opcode, ReadOnlyMemory<byte> Body)
{
    /// <summary>The body as a span, for the DALib records' <c>Parse</c> entry points.</summary>
    public ReadOnlySpan<byte> Span => Body.Span;

    /// <summary>
    ///     Turns a framed wire packet into the plaintext body a handler parses: strips the frame
    ///     header and opcode, decrypts (consuming the ordinal byte encrypted opcodes carry), and
    ///     removes dialog obfuscation for the opcodes that use it.
    /// </summary>
    /// <remarks>
    ///     De-obfuscation is DALib's <c>DialogObfuscation.Remove</c>, which
    ///     validates the CRC-CCITT the legacy in-place transform unmasked and ignored, and returns
    ///     the body with the 6-byte header already stripped — so 0x39/0x3A handlers must not skip
    ///     it. Throws on a malformed body; the caller drops the packet and keeps the connection.
    /// </remarks>
    public static InboundPacket FromFrame(InboundFrame frame, DALib.Networking.Crypto.CryptoState crypto)
    {
        var wire = frame.Wire.Span;
        var method = DALib.Networking.Crypto.CryptoState.GetClientEncryptMethod(frame.Opcode);

        byte[] body;

        if (method == DALib.Networking.Crypto.EncryptMethod.None)
        {
            // [0xAA][u16 len][opcode][body...][0x00]. The trailing null is framing, not body —
            // leaving it on hands handlers slack to over-read into.
            if (wire.Length < HeaderLength + 2)
                throw new InvalidDataException(
                    $"0x{frame.Opcode:X2}: frame too short for opcode + trailing null.");

            if (wire[^1] != 0x00)
                throw new InvalidDataException(
                    $"0x{frame.Opcode:X2}: expected trailing 0x00, got 0x{wire[^1]:X2}.");

            body = wire[(OpcodeIndex + 1)..^1].ToArray();
        }
        else
        {
            // Encrypted: [0xAA][u16 len][opcode][ordinal][ciphertext...]. Decrypting yields the
            // body plus inner plaintext padding that is NOT part of it — a trailing 0x00 for
            // Normal, 0x00 plus a copy of the opcode for MD5Key. Handing that slack to a handler
            // is what let a merchant callback read one string8 too many and silently succeed;
            // these widths mirror DALib's DecryptAndUnpadBody.
            if (wire.Length < HeaderLength + 2)
                throw new InvalidDataException(
                    $"0x{frame.Opcode:X2}: frame too short for opcode + ordinal.");

            var decrypted = crypto.DecryptClient(frame.Opcode, wire[OrdinalIndex],
                wire[(OrdinalIndex + 1)..].ToArray());

            var padding = method == DALib.Networking.Crypto.EncryptMethod.MD5Key ? 2 : 1;
            if (decrypted.Length < padding)
                throw new InvalidDataException(
                    $"0x{frame.Opcode:X2}: decrypted body ({decrypted.Length}) is shorter than its " +
                    $"{padding}-byte padding.");

            body = decrypted[..^padding];
        }

        if (DALib.Networking.Crypto.DialogObfuscation.AppliesTo(frame.Opcode))
            body = DALib.Networking.Crypto.DialogObfuscation.Remove(body);

        return new InboundPacket(frame.Opcode, body);
    }

    private const int HeaderLength = 3;
    private const int OpcodeIndex = 3;
    private const int OrdinalIndex = 4;
}
