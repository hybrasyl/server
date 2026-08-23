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
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The transport probe in <c>Server.ReadCallback</c> (HS-1621): a connection's first byte
///     decides retail-plaintext vs TLS vs garbage, exactly once. Driven through the real
///     callback with a <see cref="TestSocket" />, the same way <see cref="ReceiveWiring" />
///     holds the framing chain honest.
/// </summary>
[Collection("Hybrasyl")]
public class TransportProbeWiring
{
    public TransportProbeWiring(HybrasylFixture fixture) { }

    /// <summary>One socket read: arm the buffer where the last one left off, run the callback.</summary>
    private static void Drive(Client client, TestSocket socket)
    {
        var state = client.ClientState;
        var ar = socket.BeginReceive(state.Buffer, state.BytesReceived,
            state.Buffer.Length - state.BytesReceived, System.Net.Sockets.SocketFlags.None, null, state);

        Game.Login.ReadCallback(ar);
    }

    [Fact]
    public void FirstByte_RetailFrame_DecidesPlaintextAndDeliversTheFrame()
    {
        var socket = new TestSocket();
        var client = new Client(socket, Game.Login) { EncryptionKey = "UrkcnItnI"u8.ToArray() };
        var packet = new ClientJoinPacket
        {
            EncryptionSeed = 0x0B,
            EncryptionKey = "NILCHIRSTNA"u8.ToArray(),
            Name = "Kerden",
            RedirectId = 0x12345678,
        };
        var original = Game.Login.PacketHandlers[packet.Opcode];
        var delivered = false;

        try
        {
            Game.Login.PacketHandlers[packet.Opcode] = (_, _) => delivered = true;
            socket.QueueReceive(Client.Codec.EncodeClient(packet, client.Crypto).ToArray());

            Drive(client, socket);

            Assert.Equal(TransportKind.Plaintext, client.ClientState.Transport);
            Assert.True(delivered, "the probe must not disturb retail delivery");
            Assert.True(client.Connected);
        }
        finally
        {
            Game.Login.PacketHandlers[packet.Opcode] = original;
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    /// <summary>
    ///     With no stack certificate the upgrade seam cannot act, so a ClientHello is refused by
    ///     disconnect - and the lobby advertises no capability marker, so a capable client has
    ///     nothing to answer.
    /// </summary>
    [Fact]
    public void FirstByte_TlsClientHello_Disconnects()
    {
        var socket = new TestSocket();
        var client = new Client(socket, Game.Login);

        try
        {
            socket.QueueReceive([0x16, 0x03, 0x01, 0x00, 0xA5]);

            Drive(client, socket);

            Assert.False(client.Connected);
            Assert.Equal(TransportKind.Undecided, client.ClientState.Transport);
        }
        finally
        {
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    [Fact]
    public void FirstByte_Garbage_Disconnects()
    {
        var socket = new TestSocket();
        var client = new Client(socket, Game.Login);

        try
        {
            socket.QueueReceive([0x99, 0x00, 0x00]);

            Drive(client, socket);

            Assert.False(client.Connected);
        }
        finally
        {
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    /// <summary>
    ///     The retail invariant the probe must preserve: once a connection is decided plaintext,
    ///     later unparseable bytes behave exactly as before HS-1621 - the framing layer declines
    ///     them and the connection stays up. Only the <em>first</em> byte carries transport
    ///     meaning.
    /// </summary>
    [Fact]
    public void GarbageAfterPlaintextDecision_DoesNotDisconnect()
    {
        var socket = new TestSocket();
        var client = new Client(socket, Game.Login) { EncryptionKey = "UrkcnItnI"u8.ToArray() };
        var packet = new ClientJoinPacket
        {
            EncryptionSeed = 0x0B,
            EncryptionKey = "NILCHIRSTNA"u8.ToArray(),
            Name = "Kerden",
            RedirectId = 0x12345678,
        };

        var original = Game.Login.PacketHandlers[packet.Opcode];

        try
        {
            // The real 0x10 handler would disconnect on an unknown redirect id; this test is
            // about the probe, so the handler is a sink.
            Game.Login.PacketHandlers[packet.Opcode] = (_, _) => { };

            // A complete frame decides plaintext and is consumed, compacting the buffer -
            // whatever arrives next sits at position 0, which is what makes this able to catch
            // a probe that runs more than once.
            socket.QueueReceive(Client.Codec.EncodeClient(packet, client.Crypto).ToArray());
            Drive(client, socket);
            Assert.Equal(TransportKind.Plaintext, client.ClientState.Transport);
            Assert.True(client.Connected, "precondition: the frame alone must not disconnect");

            // 0x16 at buffer[0] on a decided connection: pre-HS-1621 behavior, no disconnect
            socket.QueueReceive([0x16, 0x99, 0x99]);
            Drive(client, socket);

            Assert.True(client.Connected,
                "a decided connection was re-probed: mid-stream bytes must have no transport meaning");
        }
        finally
        {
            Game.Login.PacketHandlers[packet.Opcode] = original;
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }
}
