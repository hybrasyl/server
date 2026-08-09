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
///         its handler directly with a body already in hand, so <c>InboundPacket.FromFrame</c> —
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
    private static InboundPacket RoundTrip(IClientPacket packet, CryptoState crypto)
    {
        var wire = Client.Codec.EncodeClient(packet, crypto);
        var frame = InboundFrame.FromWire(wire);
        return InboundPacket.FromFrame(frame, crypto);
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

    /// <summary>
    ///     Framing pops exactly one frame and leaves the next aligned — the property that lets an
    ///     undecodable packet be dropped without desyncing the stream behind it.
    /// </summary>
    [Fact]
    public void FromWire_ReadsTheOpcodeFromTheFrameHeader()
    {
        var crypto = MakeCrypto();
        var wire = Client.Codec.EncodeClient(new TurnPacket { Direction = Direction.West }, crypto);

        var frame = InboundFrame.FromWire(wire);

        Assert.Equal((byte) 0x11, frame.Opcode);
        Assert.Equal(0xAA, wire.Span[0]);
    }
}
