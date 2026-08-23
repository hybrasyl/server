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
///     What the world listener does with a join it cannot honour.
/// </summary>
[Collection("Hybrasyl")]
public class WorldJoinGuards
{
    public WorldJoinGuards(HybrasylFixture fixture) { }

    /// <summary>
    ///     A join naming a redirect that was never issued hangs the connection up.
    /// </summary>
    /// <remarks>
    ///     It used to return silently, so one socket could offer join after join forever, and each
    ///     one reached the world queue — which is unbounded and drained by a single thread — before
    ///     being discarded. The two failure branches immediately below it in the handler already
    ///     disconnect; this one did not.
    /// </remarks>
    [Fact]
    public void JoinWithAnUnissuedRedirect_Disconnects()
    {
        var socket = new TestSocket();
        var client = new TestClient(socket)
        {
            EncryptionSeed = 0,
            EncryptionKey = "UrkcnItnI"u8.ToArray(),
        };
        GlobalConnectionManifest.RegisterClient(client);

        var join = new ClientJoinPacket
        {
            EncryptionSeed = 0,
            EncryptionKey = "UrkcnItnI"u8.ToArray(),
            Name = "Kerden",
            // No redirect is minted for this connection, and ids are drawn at random across the
            // full uint range, so collision with one another test left behind is negligible.
            RedirectId = 0xDEADBEEF,
        };

        try
        {
            Game.World.WorldPacketHandlers[join.Opcode]
                .Invoke(client.ConnectionId, new InboundBody(join.Opcode, join.ToBody()));

            Assert.True(socket.IsDisconnected,
                "an unhonourable join left the socket open, so it can be offered again for free");
        }
        finally
        {
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }
}
