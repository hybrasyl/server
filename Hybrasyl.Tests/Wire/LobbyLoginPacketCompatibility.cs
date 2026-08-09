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
using System.IO.Compression;
using System.Net;
using System.Text;
using DALib.Networking.Packets.Server;
using DALib.Networking.Wire;
using Xunit;
using LegacyServerPacket = Hybrasyl.Tests.Wire.LegacyBodyWriter;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Lobby and login packet-compatibility coverage: each converted opcode's typed DALib
///     record must produce the same body bytes the legacy hand-built ServerPacket wrote
///     (except where a signed-off delta changes them, as for 0x56). The legacy
///     emit is reproduced inline from the pre-conversion site code, so any drift between
///     the record and what Hybrasyl always sent is caught here, not by a live client.
/// </summary>
/// <remarks>
///     <strong>These are compatibility tests, not send-path coverage.</strong> Nothing here
///     invokes a production send path: each case constructs a DALib record, writes its body and
///     compares it against a hand-reconstructed copy of the encoder the conversion deleted. That
///     is legitimate migration evidence — it catches a record whose bytes drifted from what
///     Hybrasyl always sent — but it says nothing about whether anything calls it. Wiring is
///     covered by <see cref="ReceiveWiring" />, <see cref="MerchantDispatchWiring" /> and
///     <see cref="CryptoPipeline" />. Named <c>*SendPath</c> until 2026-08-06, which read as
///     integration coverage it never had.
/// </remarks>
public class LobbyLoginPacketCompatibility
{
    private static byte[] Body(DALib.Networking.Wire.ServerPacket record)
    {
        var writer = new PacketWriter();
        record.WriteBody(writer);
        return writer.WrittenSpan.ToArray();
    }

    [Fact]
    public void CryptoKey_MatchesLegacyBody()
    {
        var key = Encoding.ASCII.GetBytes("UrkcnItnI");
        const uint crc = 0xDEADBEEF;
        const byte seed = 3;

        var legacy = new LegacyServerPacket(0x00);
        legacy.WriteByte(0x00);
        legacy.WriteUInt32(crc);
        legacy.WriteByte(seed);
        legacy.WriteByte((byte)key.Length);
        legacy.Write(key);

        var typed = Body(new CryptoKeyPacket { ServerTableCrc = crc, Seed = seed, Key = key });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void LoginMessage_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x02);
        legacy.WriteByte(3);
        legacy.WriteString8("Incorrect password");

        var typed = Body(new LoginMessagePacket { Type = 3, Message = "Incorrect password" });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void SystemMessage_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x0A);
        legacy.WriteByte(3);
        legacy.WriteString16("Welcome to Hybrasyl!");

        var typed = Body(new SystemMessagePacket
        {
            MessageType = SystemMessageType.ActiveMessage,
            Message = "Welcome to Hybrasyl!"
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void NotificationChecksum_MatchesLegacyBody()
    {
        const uint crc = 0x12345678;

        var legacy = new LegacyServerPacket(0x60);
        legacy.WriteByte(0x00);
        legacy.WriteUInt32(crc);

        var typed = Body(new LoginNotificationPacket
        {
            Form = new NotificationChecksumForm { Checksum = crc }
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void NotificationData_MatchesLegacyBody()
    {
        var payload = new byte[] { 0x78, 0x9C, 0x01, 0x02, 0x03 };

        var legacy = new LegacyServerPacket(0x60);
        legacy.WriteByte(0x01);
        legacy.WriteUInt16((ushort)payload.Length);
        legacy.Write(payload);

        var typed = Body(new LoginNotificationPacket
        {
            Form = new NotificationDataForm { Data = payload }
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void HomepageUrl_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x66);
        legacy.WriteByte(0x03);
        legacy.WriteString8("http://www.hybrasyl.com");

        var typed = Body(new UrlPacket { Form = new SetUrlForm { Url = "http://www.hybrasyl.com" } });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void Redirect_MatchesLegacyBody()
    {
        var key = Encoding.ASCII.GetBytes("UrkcnItnI");
        var address = IPAddress.Parse("192.168.5.20");
        const ushort port = 2611;
        const byte seed = 7;
        const string name = "socket";
        const uint id = 0xCAFE0001;

        // The pre-conversion Client.Redirect emit, verbatim.
        var addressBytes = address.GetAddressBytes();
        Array.Reverse(addressBytes);
        var legacy = new LegacyServerPacket(0x03);
        legacy.Write(addressBytes);
        legacy.WriteUInt16(port);
        legacy.WriteByte((byte)(key.Length + Encoding.ASCII.GetBytes(name).Length + 7));
        legacy.WriteByte(seed);
        legacy.WriteByte((byte)key.Length);
        legacy.Write(key);
        legacy.WriteString8(name);
        legacy.WriteUInt32(id);

        var typed = Body(new RedirectPacket
        {
            IpAddress = address,
            Port = port,
            EncryptionSeed = seed,
            EncryptionKey = key,
            Name = name,
            RedirectId = id
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void ByteHeartbeat_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x3B);
        legacy.WriteByte(0x11);
        legacy.WriteByte(0x22);

        var typed = Body(new ByteHeartbeatPacket { First = 0x11, Second = 0x22 });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void TickHeartbeat_MatchesLegacyBody()
    {
        const int tick = 0x01020304;

        var legacy = new LegacyServerPacket(0x68);
        legacy.WriteInt32(tick);

        var typed = Body(new TickHeartbeatPacket { ServerTick = (uint)tick });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    private static ServerEntry TestEntry => new()
    {
        Id = 1,
        IpAddress = IPAddress.Parse("10.20.30.40"),
        Port = 2611,
        Name = "Hybrasyl"
    };

    [Fact]
    public void ServerTable_EmitsValidZlibAndRetailTruePlaintext()
    {
        var body = Body(new ServerTableDataPacket { Servers = [TestEntry] });

        // [u16-BE compressedLength][zlib stream]
        var compressedLength = (body[0] << 8) | body[1];
        Assert.Equal(body.Length - 2, compressedLength);

        // ZLibStream validates the Adler-32 trailer on decompress — a bogus checksum
        // (the pre-conversion Hybrasyl emit) would throw here.
        using var input = new MemoryStream(body[2..], writable: false);
        using var inflater = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        var plain = output.ToArray();

        // Retail-true inner layout: [u8 count][u8 id][ip4 network order][u16-BE port][cstring name].
        // The legacy emit reversed the octets and packed ";description" onto the name; retail
        // sends network order and the bare name.
        var expected = new byte[] { 1, 1, 10, 20, 30, 40, 0x0A, 0x33 }; // 2611 = 0x0A33
        Assert.Equal(expected, plain[..8]);
        Assert.Equal("Hybrasyl\0"u8.ToArray(), plain[8..]);
    }

    // Login_ParsesUnderStrictTrailerValidation was removed here: it round-tripped DALib's own
    // LoginPacket.WriteBody through its Parse, and UserTests.LoginToWorld already drives the real
    // Game.Login.PacketHandlers[0x03] end to end. What it could not do -- catch a shared
    // misunderstanding of the trailer layout, since encoder and parser agree by construction --
    // is now covered by the capture below.

    /// <summary>
    ///     A real 0x03 captured off a retail client parses under the strict trailer validation.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Rung 1. Captured live 2026-08-06 (J): <c>C→S 0x03 Login, Normal ord=2</c>. Opcode and
    ///         ordinal are consumed by framing and Normal's single trailing padding byte stripped,
    ///         leaving the body below. The trailer is <strong>verbatim</strong> from the wire, which
    ///         is the whole point — a round-trip through DALib's own writer cannot tell us the
    ///         layout is right, only that it is self-consistent.
    ///     </para>
    ///     <para>
    ///         <strong>The password is redacted, and the fixture is therefore not byte-verbatim.</strong>
    ///         The capture carried a real credential. Substituting it is sound here and only here:
    ///         the trailer covers neither the name nor the password — verified directly by swapping
    ///         each for same-length replacements and watching the parse still succeed — so the
    ///         trailer bytes remain exactly what the client sent. Anything that made the trailer
    ///         depend on the body would invalidate this fixture, and it would need recapturing
    ///         rather than editing.
    ///     </para>
    ///     <para>
    ///         The 0x03 trailer does not authenticate the credentials it travels with — hence
    ///         "integrity" in the loosest sense. Do not read that as harmless on the grounds that
    ///         the transport protects them: <strong>it does not.</strong> Login bodies are
    ///         obfuscated with a key the client must already possess, so a passive observer
    ///         recovers the password from a capture. That is retail behaviour of 25 years'
    ///         standing rather than anything this codebase introduced, and it is a tracked finding
    ///         in the security register — but the cipher here cannot be described as credential
    ///         confidentiality, and the redaction above exists because of it, not merely for
    ///         tidiness.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RealClientLoginParsesUnderStrictTrailerValidation()
    {
        byte[] body =
        [
            0x06, .. "Kedian"u8,
            0x08, .. "redacted"u8,           // real capture carried a live password here
            0x59, 0xB3, 0x4B, 0xB3, 0x4D, 0xB1, 0x98, 0x8A,
            0xD2, 0x80, 0xD2, 0x90, 0x4D, 0x95, 0x01,
        ];

        var parsed = DALib.Networking.Packets.Client.LoginPacket.Parse(body);

        Assert.Equal("Kedian", parsed.Name);
        Assert.Equal("redacted", parsed.Password);

        // The trailer fields, straight off the wire.
        Assert.Equal((byte) 0x59, parsed.Rand1);
        Assert.Equal((byte) 0x27, parsed.XorKey);
        Assert.Equal(0xFF00FF00u, parsed.ServerHash);
        Assert.Equal((ushort) 0x1E0F, parsed.ClientHash);
        Assert.Equal(0x4F1C490Au, parsed.RandData);
    }
    /// <summary>
    ///     The two key-exchange frames, captured raw off a retail connection and parsed by DALib.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Rung 1, and genuinely raw rather than reconstructed: 0x00 and 0x10 are
    ///         <c>EncryptMethod.None</c>, so what the logger printed is what crossed the wire. Every
    ///         other capture in this suite is post-decrypt.
    ///     </para>
    ///     <para>
    ///         Captured 2026-08-07 (J) against <c>da0.kru.com:2610</c>. The connection's own log line
    ///         read <c>Seed=9, Key=3D2943692B5F685446</c>, which is an independent oracle for the
    ///         0x00 parse below — the logger derived it, DALib parses it, and they agree.
    ///     </para>
    ///     <para>
    ///         The key material is kept deliberately. It is a per-connection ephemeral seed and key
    ///         from a session that ended in 1.5 seconds, and it grants nothing now; the fixture is
    ///         worthless without it. Note that <c>socket[259]</c> is the redirect's session name, not
    ///         a character name — it is what the key table is generated from, which is worth knowing
    ///         before anyone tries to reproduce an MD5Key frame with a character's name.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RealRetailKeyExchangeFramesParse()
    {
        // S→C 0x00 CryptoKey: [subtype][u32 ServerTableCrc][seed][u8 keyLen][key]
        var cryptoKey = CryptoKeyPacket.Parse(
            Convert.FromHexString("00" + "4BDA8542" + "09" + "09" + "3D2943692B5F685446"));

        Assert.Equal(0x4BDA8542u, cryptoKey.ServerTableCrc);
        Assert.Equal((byte) 9, cryptoKey.Seed);
        Assert.Equal(Convert.FromHexString("3D2943692B5F685446"), cryptoKey.Key);

        // C→S 0x10 ClientJoin: [seed][u8 keyLen][key][string8 name][u32-BE redirectId]
        var join = DALib.Networking.Packets.Client.ClientJoinPacket.Parse(
            Convert.FromHexString("09" + "09" + "3D2943692B5F685446" + "0B" + "736F636B65745B3235395D" + "0000048B"));

        Assert.Equal((byte) 9, join.EncryptionSeed);
        Assert.Equal(Convert.FromHexString("3D2943692B5F685446"), join.EncryptionKey);
        Assert.Equal("socket[259]", join.Name);

        // The redirect carries the same seed and key the lobby issued -- which is the whole
        // mechanism by which a passive observer recovers them.
        Assert.Equal(cryptoKey.Seed, join.EncryptionSeed);
        Assert.Equal(cryptoKey.Key, join.EncryptionKey);
    }
}
