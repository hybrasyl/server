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

using System.Text;
using DALib.Networking.Crypto;
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
}
