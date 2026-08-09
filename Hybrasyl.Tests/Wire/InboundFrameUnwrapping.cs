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
using DALib.Enums;
using DALib.Networking.Crypto;
using DALib.Networking.Packets.Client;
using DALib.Networking.Wire;
using Hybrasyl.Networking;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The receive path itself: framing, decrypt, dialog de-obfuscation, and the body a
///     handler ends up parsing.
/// </summary>
/// <remarks>
///     <para>
///         This is the one part of the pipeline no other test reaches. Every handler test invokes
///         its handler directly with a body already in hand, so <c>InboundBody.FromFrame</c> —
///         which does the slicing, ordinal handling and de-obfuscation — had no coverage at all
///         until this file. A green suite said nothing about it.
///     </para>
///     <para>
///         The assertion is the invariant callers actually depend on: <strong>the body a handler
///         receives is byte-identical to the body the packet was built from</strong>. DALib's
///         encoder is the wire authority and stands in for the client here; what is under test is
///         Hybrasyl's own framing and unwrapping, which can and does fail independently of it.
///     </para>
/// </remarks>
public class InboundFrameUnwrapping
{
    private static CryptoState MakeCrypto() => new()
    {
        EncryptionSeed = 5,
        EncryptionKey = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09],
    };

    /// <summary>Encodes a packet the way a client would, then unwraps it the way the server now does.</summary>
    private static InboundBody RoundTrip(IClientPacket packet, CryptoState crypto)
    {
        var wire = Client.Codec.EncodeClient(packet, crypto);
        var frame = InboundFrame.FromWire(wire);
        return InboundBody.FromFrame(frame, crypto);
    }

    /// <summary>0x06 Walk — MD5Key, so the frame carries an ordinal the decrypt is keyed on.</summary>
    [Fact]
    public void EncryptedOpcode_UnwrapsToTheOriginalBody()
    {
        var crypto = MakeCrypto();
        var original = new WalkPacket { Direction = Direction.South, Sequence = 0x11 };

        var received = RoundTrip(original, crypto);

        Assert.Equal(original.Opcode, received.Opcode);
        Assert.Equal(original.ToBody(), received.Body.ToArray());
        // And it still parses back to the same values a handler would read.
        var reparsed = WalkPacket.Parse(received.Body.Span);
        Assert.Equal(Direction.South, reparsed.Direction);
        Assert.Equal((byte) 0x11, reparsed.Sequence);
    }

    /// <summary>
    ///     0x10 ClientJoin — an EncryptMethod.None opcode, so there is no ordinal byte and the
    ///     body starts one byte earlier. Getting this boundary wrong shifts every field by one.
    /// </summary>
    [Fact]
    public void PlaintextOpcode_HasNoOrdinalAndUnwrapsToTheOriginalBody()
    {
        var crypto = MakeCrypto();
        var original = new ClientJoinPacket
        {
            EncryptionSeed = 0x0B,
            EncryptionKey = "NILCHIRSTNA"u8.ToArray(),
            Name = "Kerden",
            RedirectId = 0x12345678,
        };

        var received = RoundTrip(original, crypto);

        Assert.Equal(original.Opcode, received.Opcode);
        Assert.Equal(original.ToBody(), received.Body.ToArray());

        var reparsed = ClientJoinPacket.Parse(received.Body.Span);
        Assert.Equal("Kerden", reparsed.Name);
        Assert.Equal(0x12345678u, reparsed.RedirectId);
        Assert.Equal((byte) 0x0B, reparsed.EncryptionSeed);
    }

    /// <summary>
    ///     0x3A carries the dialog-obfuscation layer. The body handed to a handler must have the
    ///     6-byte header already stripped — if de-obfuscation were skipped or applied twice,
    ///     the body would not match and the reparse would throw or yield garbage.
    /// </summary>
    [Fact]
    public void DialogOpcode_IsDeobfuscatedAndHeaderStripped()
    {
        var crypto = MakeCrypto();
        var original = new DialogOptionResponsePacket
        {
            ObjectType = NpcMainMenuPacket.ObjectTypeCreature,
            ObjectId = 0xDEADBEEF,
            PursuitId = 0x1234,
            PursuitIndex = 0x0005,
            Option = 2,
        };

        var received = RoundTrip(original, crypto);

        Assert.Equal(original.Opcode, received.Opcode);
        Assert.Equal(original.ToBody(), received.Body.ToArray());

        // The header is gone: a handler parses straight from byte 0 of what it is handed.
        var reparsed = DialogUsePacket.Parse(received.Body.Span);
        Assert.Equal(0xDEADBEEFu, reparsed.ObjectId);
    }

    /// <summary>
    ///     The 0x39 merchant tail is the reason handlers receive a body rather than a parsed
    ///     record: the codec's opcode-keyed parse yields the bare select form and drops the tail,
    ///     because the tail's shape depends on the menu the server last sent. The raw body must
    ///     survive the receive path intact so a callback can re-parse it as its own form.
    /// </summary>
    [Fact]
    public void MerchantTail_SurvivesTheReceivePathForCallbackReparse()
    {
        var crypto = MakeCrypto();
        var original = new NpcTextResponsePacket
        {
            ObjectType = NpcMainMenuPacket.ObjectTypeCreature,
            ObjectId = 0x00C0FFEE,
            PursuitId = 0xFF01,
            Text = "12",
        };

        var received = RoundTrip(original, crypto);

        Assert.Equal(original.ToBody(), received.Body.ToArray());

        // What the codec would have given us instead: the tail is gone.
        var viaCodec = NpcMainMenuPacket.Parse(received.Body.Span);
        Assert.Equal(0x00C0FFEEu, viaCodec.ObjectId);

        // What the merchant callback actually does, and must keep working.
        var reparsed = NpcTextResponsePacket.ParseResponse(received.Body.Span);
        Assert.Equal("12", reparsed.Text);
    }

    /// <summary>
    ///     The codec must scan DALib's assembly as well as Hybrasyl's — PacketCodec does not
    ///     include its own implicitly. Scanning only Hybrasyl's left both parser tables empty,
    ///     which went unnoticed because nothing parsed through the codec: encoding uses only
    ///     Opcode + WriteBody. It surfaced as every opcode reporting unregistered.
    /// </summary>
    [Fact]
    public void Codec_RegistersDalibOpcodes()
    {
        Assert.True(Client.Codec.IsClientOpcodeRegistered(0x00), "0x00 ClientVersion");
        Assert.True(Client.Codec.IsClientOpcodeRegistered(0x06), "0x06 Walk");
        Assert.True(Client.Codec.IsClientOpcodeRegistered(0x10), "0x10 ClientJoin");
        Assert.True(Client.Codec.RegisteredClientOpcodeCount > 40,
            $"expected the full C→S surface, got {Client.Codec.RegisteredClientOpcodeCount}");
        Assert.True(Client.Codec.RegisteredServerOpcodeCount > 40,
            $"expected the full S→C surface, got {Client.Codec.RegisteredServerOpcodeCount}");
    }

    /// <summary>Builds a raw C→S frame: <c>[0xAA][u16-BE len][opcode][body...]</c>.</summary>
    private static byte[] RawFrame(byte opcode, params byte[] body)
    {
        var frame = new byte[body.Length + 4];
        frame[0] = 0xAA;
        frame[1] = (byte) (((body.Length + 1) >> 8) & 0xFF);
        frame[2] = (byte) ((body.Length + 1) & 0xFF);
        frame[3] = opcode;
        body.CopyTo(frame, 4);

        return frame;
    }

    /// <summary>
    ///     Framing pops exactly one frame and leaves the next aligned. This is the property that
    ///     lets the receive loop drop an undecodable packet and keep the connection: every
    ///     <c>continue</c> in <c>Client.FlushReceiveBuffer</c> depends on the stream behind the
    ///     bad frame still starting on a boundary.
    /// </summary>
    /// <remarks>
    ///     A neighbouring opcode-header test claimed this property until 2026-08-06 without
    ///     asserting it — that test has since been removed, its DALib-encoder cross-check
    ///     superseded by the raw retail frame in <c>CryptoRoundTrip</c> — and a sweep of the test
    ///     project for <c>ReceiveBufferPop</c> found nothing: it had no coverage anywhere. Two frames are
    ///     written into one buffer with distinct opcodes and distinct bodies, so a length
    ///     miscalculation shows up as the second frame being wrong rather than merely absent.
    /// </remarks>
    [Fact]
    public void FramingPopsOneFrameAndLeavesTheNextAligned()
    {
        var state = new ClientState(new TestSocket());
        var first = RawFrame(0x11, 0x01, 0x02);
        var second = RawFrame(0x06, 0x03, 0x04, 0x05);

        first.CopyTo(state.Buffer, 0);
        second.CopyTo(state.Buffer, first.Length);
        state.BytesReceived = first.Length + second.Length;

        Assert.True(state.TryGetFrame(out var f1), "first frame should pop");
        Assert.Equal((byte) 0x11, f1.Opcode);
        Assert.Equal(first, f1.Wire.ToArray());

        // The byte count must come down with the pop, or a later partial frame is misjudged.
        Assert.Equal(second.Length, state.BytesReceived);

        Assert.True(state.TryGetFrame(out var f2),
            "the second frame must still start on a boundary after the first was popped");
        Assert.Equal((byte) 0x06, f2.Opcode);
        Assert.Equal(second, f2.Wire.ToArray());

        Assert.False(state.TryGetFrame(out _), "buffer should be drained");
    }
    /// <summary>
    ///     A frame that has not fully arrived must not pop, and must pop intact once the rest of
    ///     it lands. This is the <c>BytesReceived</c> half of the alignment property: TCP delivers
    ///     the stream in arbitrary chunks, so a header claiming more than the buffer holds is the
    ///     normal case, not an error.
    /// </summary>
    /// <remarks>
    ///     Added because the two-frame test above does not cover it — deleting
    ///     <c>BytesReceived -= packetLength</c> left that test green, since its drain assertion
    ///     rests on the popped buffer no longer starting with 0xAA rather than on the byte count.
    /// </remarks>
    [Fact]
    public void PartialFrameDoesNotPopUntilTheRestArrives()
    {
        var state = new ClientState(new TestSocket());
        var frame = RawFrame(0x06, 0x03, 0x04, 0x05);

        // Everything but the final byte.
        frame[..^1].CopyTo(state.Buffer, 0);
        state.BytesReceived = frame.Length - 1;

        Assert.False(state.TryGetFrame(out _),
            "an incomplete frame must not pop: the body would be truncated and the stream desynced");

        // The tail arrives.
        state.Buffer[frame.Length - 1] = frame[^1];
        state.BytesReceived = frame.Length;

        Assert.True(state.TryGetFrame(out var complete), "the frame should pop once complete");
        Assert.Equal((byte) 0x06, complete.Opcode);
        Assert.Equal(frame, complete.Wire.ToArray());
    }
}
