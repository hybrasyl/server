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
///     Crypto/wire round-trip coverage introduced with the DALib conversion (Phase 1).
///     Before this, the encrypted 0xAA-framed wire had zero test coverage. These tests pair
///     a server-role CryptoState (mirroring Client.Crypto) with a client-role one, encode an
///     S->C packet through the real send path (Client.Codec + RawBodyPacket bridge), and
///     confirm the client recovers the exact plaintext body — proving the DALib codec is a
///     drop-in for the hand-rolled crypto across all three encrypt methods.
/// </summary>
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

    [Fact]
    public void ServerNormal_WithoutKey_IsReportedUninitialized()
    {
        // The FlushSendBuffer guard drops Normal-mode packets when no key is negotiated.
        var crypto = new CryptoState();
        Assert.False(crypto.IsInitialized);
        Assert.Equal(DALib.Networking.Crypto.EncryptMethod.Normal, CryptoState.GetServerEncryptMethod(0x0A));
    }
}
