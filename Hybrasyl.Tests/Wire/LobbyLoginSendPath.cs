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
///     Lobby and login send-path coverage: each converted opcode's typed DALib
///     record must produce the same body bytes the legacy hand-built ServerPacket wrote
///     (except where a signed-off delta changes them, as for 0x56). The legacy
///     emit is reproduced inline from the pre-conversion site code, so any drift between
///     the record and what Hybrasyl always sent is caught here, not by a live client.
/// </summary>
public class LobbyLoginSendPath
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
    public void ServerTable_RoundTripsThroughDalibParse()
    {
        var body = Body(new ServerTableDataPacket { Servers = [TestEntry] });
        var parsed = ServerTableDataPacket.Parse(body);

        var entry = Assert.Single(parsed.Servers);
        Assert.Equal(TestEntry.Id, entry.Id);
        Assert.Equal(TestEntry.IpAddress, entry.IpAddress);
        Assert.Equal(TestEntry.Port, entry.Port);
        Assert.Equal(TestEntry.Name, entry.Name);
    }

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

    /// <summary>
    ///     The typed 0x03 parse accepts a well-formed body, integrity trailer and
    ///     all — the trailer's CRC is validated, so a packet whose trailer is malformed throws.
    /// </summary>
    /// <remarks>
    ///     <strong>This is a round-trip and cannot be more than that.</strong> It previously ran
    ///     through the legacy <c>ClientPackets.Login</c> injector and was described as pinning
    ///     "the bytes the client actually sends", but that injector built its body by calling
    ///     DALib's own <c>LoginPacket.WriteBody</c> — so it was DALib agreeing with itself, and
    ///     the wording overstated it. The injector is gone and the construction is now
    ///     direct, which changes nothing about what is proven: this catches a parse that rejects
    ///     its own encoder's output, and would not catch a shared misunderstanding of the trailer
    ///     layout. Pinning that needs a capture, not a round-trip.
    /// </remarks>
    [Fact]
    public void Login_ParsesUnderStrictTrailerValidation()
    {
        var body = new DALib.Networking.Packets.Client.LoginPacket
            { Name = "Kerden", Password = "leethax6" }.ToBody();

        var parsed = DALib.Networking.Packets.Client.LoginPacket.Parse(body);

        Assert.Equal("Kerden", parsed.Name);
        Assert.Equal("leethax6", parsed.Password);
    }
}
