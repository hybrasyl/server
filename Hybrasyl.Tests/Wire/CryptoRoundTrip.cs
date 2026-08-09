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
using DALib.Networking.Crypto;
using DALib.Networking.Packets.Client;
using Hybrasyl.Networking;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Crypto round-trip smoke coverage for the encrypted 0xAA-framed wire, which had none
///     before the conversion. Each case pairs a server-role CryptoState (mirroring Client.Crypto)
///     with a client-role one, encodes an S->C packet, and confirms the client recovers the exact
///     plaintext body across all three encrypt methods.
/// </summary>
/// <remarks>
///     <strong>This is not the send path, and not codec-configuration evidence.</strong> The
///     summary claimed both until 2026-08-06. <c>PacketCodec.EncodeServer</c> reads only the
///     packet's own <c>Opcode</c> and <c>WriteBody</c> — it never consults the configured
///     assembly/parser tables — and <see cref="RawBodyPacket" /> is deliberately unregistered, so
///     nothing here can exercise codec registration. That is
///     <c>InboundFrameUnwrapping.Codec_RegistersDalibOpcodes</c>. The real outbound pipeline,
///     guards included, is <see cref="CryptoPipeline" />.
///     <para>
///         What remains is a dependency smoke test: it would catch DALib's crypto changing shape
///         under us. Worth keeping at that weight and no more. Strengthening it into protocol
///         evidence needs fixed known-good ciphertext vectors, which neither repository has.
///     </para>
/// </remarks>
public class CryptoRoundTrip
{
    private const string KeyTableSeed = "TestCharacter";

    private static (CryptoState server, CryptoState client) Paired()
    {
        var key = Encoding.ASCII.GetBytes("UrkcnItnI");
        var server = new CryptoState { EncryptionSeed = 1, EncryptionKey = key };
        var client = new CryptoState { EncryptionSeed = 1, EncryptionKey = key };
        server.GenerateKeyTable(KeyTableSeed);
        client.GenerateKeyTable(KeyTableSeed);
        return (server, client);
    }

    // Strip the outer [0xAA][u16-BE len] frame; return the [opcode][ordinal?][payload] bytes.
    private static byte[] Unframe(System.ReadOnlyMemory<byte> wire)
    {
        var span = wire.Span;
        Assert.Equal(0xAA, span[0]);
        var len = (span[1] << 8) | span[2];
        Assert.Equal(len, span.Length - 3);
        return span[3..].ToArray();
    }

    [Theory]
    [InlineData(0x08)] // Attributes — MD5Key
    [InlineData(0x33)] // DisplayUser — MD5Key
    [InlineData(0x0A)] // SystemMessage — Normal
    [InlineData(0x60)] // Notification — Normal
    public void ServerEncrypted_RoundTrips(byte opcode)
    {
        var (server, client) = Paired();
        var body = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03 };

        var wire = Client.Codec.EncodeServer(new RawBodyPacket(opcode, body), server);
        var enc = Unframe(wire);

        Assert.Equal(opcode, enc[0]);
        var ordinal = enc[1];
        var recovered = client.DecryptServer(opcode, ordinal, enc[2..]);

        // The bridge appends GenerateFooter inner padding for byte parity, so the
        // recovered plaintext is body + padding; the leading bytes must equal the body verbatim.
        Assert.True(recovered.Length >= body.Length);
        Assert.Equal(body, recovered[..body.Length]);
    }

    [Theory]
    [InlineData(0x00)] // CryptoKey — None
    [InlineData(0x7E)] // AcceptConnection — None
    public void ServerUnencrypted_RoundTrips(byte opcode)
    {
        var (server, _) = Paired();
        var body = new byte[] { 0x11, 0x22, 0x33 };

        var wire = Client.Codec.EncodeServer(new RawBodyPacket(opcode, body), server);
        var enc = Unframe(wire);

        // None frames carry [opcode][body...] with no ordinal, no footer, no encryption.
        Assert.Equal(opcode, enc[0]);
        Assert.Equal(body, enc[1..]);
    }

    [Fact]
    public void ClientEncrypted_RoundTrips_ThroughDecryptClient()
    {
        // Mirror of the receive path: a client encrypts a C->S packet, the server-role
        // CryptoState.DecryptClient recovers the body (the exact call FlushReceiveBuffer makes).
        var (server, client) = Paired();
        var body = new byte[] { 0x41, 0x42, 0x43, 0x44 };
        const byte opcode = 0x0F; // MD5Key C->S

        var plainPayload = new byte[1 + body.Length];
        plainPayload[0] = opcode;
        body.CopyTo(plainPayload, 1);

        var encrypted = client.EncryptClientPacket(opcode, plainPayload);
        // encrypted = [opcode][ordinal][data][7-byte footer]; DecryptClient wants payload after ordinal.
        var recovered = server.DecryptClient(opcode, encrypted[1], encrypted[2..]);

        Assert.True(recovered.Length >= body.Length);
        Assert.Equal(body, recovered[..body.Length]);
    }
    /// <summary>
    ///     A raw retail frame, decrypted by DALib and validated by a CRC it did not compute.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Everything else in this class pairs two DALib <see cref="CryptoState" /> instances and
    ///         is therefore self-consistent by construction — it would survive DALib and retail
    ///         disagreeing about the cipher entirely. This one cannot: the ciphertext was produced by
    ///         a retail client and nothing here participated in making it.
    ///     </para>
    ///     <para>
    ///         Captured pre-decryption 2026-08-07 (J) against <c>da0.kru.com:2610</c>, on the
    ///         connection whose key exchange is pinned in
    ///         <c>LobbyLoginPacketCompatibility.RealRetailKeyExchangeFramesParse</c> — seed 9, key
    ///         <c>3D2943692B5F685446</c>, read off the wire from the 0x00 CryptoKey.
    ///     </para>
    ///     <para>
    ///         <strong>The oracle is the CRC, not the assertion below.</strong> 0x3A is
    ///         dialog-obfuscated, and <c>DialogObfuscation.Remove</c> validates a CRC-CCITT carried
    ///         <em>inside</em> the encrypted payload. A single wrong byte anywhere in the decrypt —
    ///         wrong salt row for the seed, wrong key application, wrong footer width — makes the
    ///         unmask throw rather than return plausible garbage. The asserted plaintext is a
    ///         convenience; the fact that this chain completes at all is the result.
    ///     </para>
    ///     <para>
    ///         Note the direction. Re-encrypting cannot be pinned byte-for-byte: the 7-byte footer
    ///         carries per-packet random bRand/sRand, so DALib encoding the same body produces
    ///         different bytes every time and legitimately so. Decrypt is the only direction in which
    ///         a fixed vector is meaningful.
    ///     </para>
    ///     <para>
    ///         <strong>Scope, established by mutating each input.</strong> This pins the seed (which
    ///         selects the salt row), the key, and the ciphertext body — corrupting any of the three
    ///         makes the inner CRC reject. It pins <em>neither</em> the ordinal nor the footer:
    ///         altering the ordinal byte, or the bRand/sRand at the tail, leaves it green. Both are
    ///         genuinely inert here, because a Normal-mode packet decrypts under the static
    ///         <see cref="CryptoState.EncryptionKey" /> rather than the <c>GenerateKey(bRand, sRand)</c>
    ///         path an MD5Key packet takes. An MD5Key vector would cover them and needs a separate
    ///         capture — plus the session name the key table is built from, which is the redirect's
    ///         name and not the character's.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RetailCiphertextDecryptsAndItsInnerCrcValidates()
    {
        // AA [u16-BE len] [opcode 3A] [ordinal 1D] [ciphertext...]
        var wire = Convert.FromHexString(
            "AA001B3A1D72F4A2869378D0EEFD7F051E7B6B162A17371F7539C64E543E");

        var crypto = new CryptoState
        {
            EncryptionSeed = 9,
            EncryptionKey = Convert.FromHexString("3D2943692B5F685446")
        };

        var packet = InboundPacket.FromFrame(InboundFrame.FromWire(wire), crypto);

        // [u8 objectType][u32-BE objectId][u16-BE pursuitId][u16-BE pursuitIndex][u8 tag][u8 option]
        Assert.Equal(
            new byte[] { 0x01, 0x00, 0x00, 0x1F, 0x70, 0x02, 0x4C, 0x01, 0x09, 0x01, 0x01 },
            packet.Body.ToArray());

        var option = Assert.IsType<DialogOptionResponsePacket>(
            DialogUsePacket.Parse(packet.Body.Span));
        Assert.Equal(0x024C, option.PursuitId);
        Assert.Equal(0x01, option.Option);
    }
}
