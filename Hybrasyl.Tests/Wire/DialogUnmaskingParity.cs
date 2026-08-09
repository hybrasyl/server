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
using DALib.Networking.Packets.Client;
using DALib.Networking.Wire;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The 0x39/0x3A dialog-obfuscation cutover. These pin the claim it rests on — that DALib's
///     <c>Remove</c> unmasks byte-for-byte identically to the
///     deleted <c>ClientPacket.DecryptDialog</c>, and differs only by stripping the 6-byte header
///     and validating the CRC the legacy path unmasked and then ignored.
/// </summary>
public class DialogUnmaskingParity
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


    // CRC-mismatch rejection and the AppliesTo opcode gate are covered upstream
    // (DialogObfuscationTests, whose AppliesTo case is a strict superset of ours). This one is
    // not: DALib reaches only the `length < 6` branch, never `4 + lengthField > buffer`.
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
        // The regression the cutover surfaced. Rung-1 (darkages-741 packet-transforms, "Dialog-response
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
    /// <summary>
    ///     A real 0x39 captured off a retail client survives <c>Remove</c>, CRC validation and all,
    ///     and decodes to a sensible menu select.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the one thing every other test on this path could not do. The cutover turned the
    ///         CRC from "unmasked and ignored" into "validated, and the packet dropped if it
    ///         fails", and until now every input to <c>Remove</c> originated from DALib's own
    ///         <c>Apply</c> — self-consistent by construction. If the validation rejected
    ///         legitimate client traffic, nothing in either repository would have known and every
    ///         dialog would have broken in production.
    ///     </para>
    ///     <para>
    ///         Rung 1. Captured from a live session 2026-08-06 (J):
    ///         <c>C→S 0x39 NPCMainMenu, MD5Key ord=39</c>, logged post-decrypt as
    ///         <c>39 27 4F C5 59 53 C8 DB 10 12 13 0B 65 16 57 00 39</c> — opcode, ordinal, then
    ///         the body with MD5Key's two-byte inner padding (<c>00</c> plus a copy of the opcode)
    ///         still on the tail. The bytes below are what <c>InboundFrame.FromFrame</c> hands to
    ///         <c>Remove</c>: opcode and ordinal consumed by framing, padding stripped.
    ///     </para>
    ///     <para>
    ///         Incidentally verified: leaving the padding on yields the identical payload, because
    ///         <c>Remove</c> is governed by the length field rather than the buffer end. So trailing
    ///         slack cannot corrupt it — which is the invariant
    ///         <see cref="RemoveLeavesNoTrailingSlackForAHandlerToOverReadInto" /> asserts from the
    ///         other side.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RealClientDialogFrameSurvivesTheCrcValidation()
    {
        byte[] captured =
        [
            0x4F, 0xC5, 0x59, 0x53, 0xC8, 0xDB,
            0x10, 0x12, 0x13, 0x0B, 0x65, 0x16, 0x57
        ];

        var plain = DialogObfuscation.Remove(captured);

        // [u8 objectType][u32-BE objectId][u16-BE pursuitId] — the bare select form.
        Assert.Equal(new byte[] { 0x01, 0x00, 0x00, 0x1F, 0x70, 0x00, 0x40 }, plain);

        var parsed = NpcMainMenuSelectPacket.ParseResponse(plain);
        Assert.Equal(0x01, parsed.ObjectType);
        Assert.Equal(0x00001F70u, parsed.ObjectId);
        Assert.Equal(0x0040, parsed.PursuitId);
    }
    /// <summary>
    ///     A real 0x3A captured off a retail client survives <c>Remove</c> and dispatches to the
    ///     variant its tag byte names — the discrimination the cutover introduced.
    /// </summary>
    /// <remarks>
    ///     Rung 1. Captured live 2026-08-06 (J): <c>C→S 0x3A DialogUse, Normal ord=51</c>, logged
    ///     post-decrypt as <c>3A 33 38 04 81 8F F0 65 38 3A 3B 26 B4 3F 64 40 5E 43 42 00</c>.
    ///     Opcode and ordinal are consumed by framing and Normal's single trailing padding byte is
    ///     stripped by <c>InboundFrame.FromFrame</c>, leaving the 17 bytes below.
    ///     <para>
    ///         The cutover made <c>DialogUsePacket.Parse</c> dispatch on the tag byte that the legacy
    ///         handler read and discarded, and until now that dispatch was only ever fed frames
    ///         DALib had produced itself. This is a real client saying "menu choice, option 1" and
    ///         the parse agreeing.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RealClient0x3ASurvivesRemoveAndDispatchesOnItsTag()
    {
        byte[] captured =
        [
            0x38, 0x04, 0x81, 0x8F, 0xF0, 0x65, 0x38, 0x3A, 0x3B,
            0x26, 0xB4, 0x3F, 0x64, 0x40, 0x5E, 0x43, 0x42
        ];

        var plain = DialogObfuscation.Remove(captured);

        // [u8 objectType][u32-BE objectId][u16-BE pursuitId][u16-BE pursuitIndex][u8 tag][u8 option]
        Assert.Equal(new byte[] { 0x01, 0x00, 0x00, 0x1A, 0x89, 0x01, 0x5B, 0x00, 0x1F, 0x01, 0x01 }, plain);

        var option = Assert.IsType<DialogOptionResponsePacket>(DialogUsePacket.Parse(plain));
        Assert.Equal(0x015B, option.PursuitId);
        Assert.Equal(0x01, option.Option);
    }
}
