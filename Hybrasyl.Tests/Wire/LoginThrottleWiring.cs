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

using DALib.Networking.Packets.Client;
using Hybrasyl.Networking;
using Hybrasyl.Servers;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     That the login listener's dispatch is subject to packet throttling at all.
/// </summary>
/// <remarks>
///     The throttling infrastructure has always been complete and functional, and the login
///     server could never use it: <c>Client.FlushReceiveBuffer</c> consulted
///     <c>PacketThrottleCheck</c> only on its World branch, so lobby and login invoked their
///     handlers directly. Registering a throttle on 0x03 was therefore inert, which is why this
///     asserts through the dispatch rather than by calling <c>PacketThrottleCheck</c> — the
///     latter passes whether or not anything calls it.
/// </remarks>
[Collection("Hybrasyl")]
public class LoginThrottleWiring
{
    public LoginThrottleWiring(HybrasylFixture fixture) { }

    /// <summary>
    ///     0x03 Login, whose handler verifies a BCrypt hash at cost 12 — about 175 ms of CPU on
    ///     the socket thread, for an unauthenticated packet of about thirty bytes.
    /// </summary>
    /// <remarks>
    ///     The trailer fields are pinned because LoginPacket.WriteBody otherwise draws them from
    ///     Random.Shared, and its Parse validates the XOR'd CRC trailer built from them.
    /// </remarks>
    private static LoginPacket Login() => new()
    {
        Name = "Kerden",
        Password = "leethax6",
        Rand1 = 0x11,
        XorKey = 0x22,
        ServerHash = 0x33445566,
        ClientHash = 0x7788,
        RandData = 0x99AABBCC,
    };

    [Fact]
    public void RepeatedLoginAttempts_AreThrottledBeforeReachingTheHandler()
    {
        var client = new Client(new TestSocket(), Game.Login) { EncryptionKey = "UrkcnItnI"u8.ToArray() };
        var packet = Login();
        var original = Game.Login.PacketHandlers[packet.Opcode];
        var delivered = 0;

        try
        {
            Game.Login.PacketHandlers[packet.Opcode] = (_, _) => delivered++;

            // Encoded separately: the ordinal advances per packet on a Normal-encrypted opcode.
            client.ReceiveFrame(InboundFrame.FromWire(Client.Codec.EncodeClient(packet, client.Crypto)));
            Assert.Equal(1, delivered);

            client.ReceiveFrame(InboundFrame.FromWire(Client.Codec.EncodeClient(packet, client.Crypto)));

            Assert.Equal(1, delivered);
        }
        finally
        {
            Game.Login.PacketHandlers[packet.Opcode] = original;
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }
}
