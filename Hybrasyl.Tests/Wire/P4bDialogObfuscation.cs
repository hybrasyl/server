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
using DALib.Networking.Crypto;
using DALib.Networking.Wire;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Phase 4b, the 0x39/0x3A dialog-obfuscation cutover. These pin the claim the
///     delta rests on — that DALib's <c>Remove</c> unmasks byte-for-byte identically to the
///     deleted <c>ClientPacket.DecryptDialog</c>, and differs only by stripping the 6-byte header
///     and validating the CRC the legacy path unmasked and then ignored.
/// </summary>
public class P4bDialogObfuscation
{
    // Verbatim reimplementation of the deleted ClientPacket.DecryptDialog, kept here as the
    // oracle. It unmasks in place and leaves the header (and the CRC it never checked) in front.
    private static byte[] LegacyDecryptDialog(byte[] data)
    {
        var copy = (byte[]) data.Clone();
        var xPrime = (byte) (copy[0] - 0x2D);
        var x = (byte) (copy[1] ^ xPrime);
        var y = (byte) (x + 0x72);
        var z = (byte) (x + 0x28);
        copy[2] ^= y;
        copy[3] ^= (byte) ((y + 1) % 256);
        var length = (copy[2] << 8) | copy[3];
        for (var i = 0; i < length; i++) copy[4 + i] ^= (byte) ((z + i) % 256);
        return copy;
    }

    private static byte[] SampleBody() =>
        // A plausible 0x3A option response: [u8 type][u32 id][u16 pursuit][u16 index][tag][option]
        [0x01, 0x00, 0x00, 0x12, 0x34, 0xFF, 0x01, 0x00, 0x02, 0x01, 0x03];

    [Fact]
    public void RemoveAgreesWithTheLegacyUnmaskingByteForByte()
    {
        var body = SampleBody();
        var obfuscated = DialogObfuscation.Apply(body, new Random(20260729));

        var legacy = LegacyDecryptDialog(obfuscated);
        var dalib = DialogObfuscation.Remove(obfuscated);

        // The legacy transform left everything in place; DALib returns the body alone. The
        // overlapping region — everything from offset 6 — must be identical.
        Assert.Equal(body, dalib);
        Assert.Equal(legacy[6..(6 + body.Length)], dalib);
    }

    [Fact]
    public void LegacyUnmaskedTheCrcBytesButNeverCheckedThem()
    {
        // The legacy loop ran from offset 4, so it unmasked the two CRC bytes as if they were
        // body — then did nothing with them. This is what the delta turns into a real check.
        var body = SampleBody();
        var obfuscated = DialogObfuscation.Apply(body, new Random(1));

        var legacy = LegacyDecryptDialog(obfuscated);
        var crc = (ushort) ((legacy[4] << 8) | legacy[5]);

        var actual = (ushort) 0;
        foreach (var b in body)
            actual = CrcCcitt.Step(actual, b);

        // The legacy path had the right value sitting in the buffer and ignored it.
        Assert.Equal(actual, crc);
    }

    [Fact]
    public void RemoveRejectsATamperedBody()
    {
        var obfuscated = DialogObfuscation.Apply(SampleBody(), new Random(7));

        // Flip a body byte, leaving the header (and so the CRC it carries) untouched.
        obfuscated[8] ^= 0xFF;

        Assert.Throws<InvalidDataException>(() => DialogObfuscation.Remove(obfuscated));
    }

    [Fact]
    public void RemoveRejectsAnInconsistentLengthField()
    {
        var obfuscated = DialogObfuscation.Apply(SampleBody(), new Random(9));

        // Corrupt the masked length field so it claims more body than the buffer holds.
        obfuscated[2] ^= 0x7F;

        Assert.Throws<InvalidDataException>(() => DialogObfuscation.Remove(obfuscated));
    }

    [Fact]
    public void RemoveLeavesNoTrailingSlackForAHandlerToOverReadInto()
    {
        // The regression this surfaced. Rung-1 (darkages-741 packet-transforms, "Dialog-response
        // inner wrapper") puts a literal zero after encrypted_inner which is NOT part of the
        // payload — inner_length covers crc16 + payload only. The legacy in-place transform left
        // that zero (and any crypto slack) in the buffer, so a handler reading one field too many
        // got a 0x00 length byte back and silently succeeded. Remove returns the payload exactly,
        // so the same over-read now throws — which is how BuyItemWithQuantity's dead second
        // ReadString8 was found.
        //
        // A 0x39 buy-quantity response: [u8 type][u32 id][u16 pursuit][string8 name] and nothing
        // more.
        byte[] payload = [0x01, 0x00, 0x00, 0x12, 0x34, 0xFF, 0x11, 0x05, .. "Beryl"u8];

        var body = DialogObfuscation.Remove(DialogObfuscation.Apply(payload, new Random(4)));

        Assert.Equal(payload, body);
        // Exactly the payload: no terminator, no slack, nothing a second field read could land in.
        Assert.Equal(payload.Length, body.Length);
    }

    [Fact]
    public void AppliesToCoversExactlyTheTwoDialogOpcodes()
    {
        Assert.True(DialogObfuscation.AppliesTo(0x39));
        Assert.True(DialogObfuscation.AppliesTo(0x3A));
        Assert.False(DialogObfuscation.AppliesTo(0x38));
        Assert.False(DialogObfuscation.AppliesTo(0x3B));
    }
}
