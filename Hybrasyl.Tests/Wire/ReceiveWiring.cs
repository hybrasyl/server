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
using DALib.Networking.Wire;
using Hybrasyl.Networking;
using Hybrasyl.Servers;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     That the receive path is actually <em>wired</em>: a frame arriving on a
///     connection reaches a handler, with the right body.
/// </summary>
/// <remarks>
///     <para>
///         This exists because the receive path shipped twice with the server unable to process a single
///         packet, and the suite was green at 426/426 both times. Neither failure was in any
///         component's logic:
///     </para>
///     <list type="number">
///         <item>
///             Moving framing into <c>ReadCallback</c> dropped the <c>FlushReceiveBuffer</c> call
///             that the old <c>Enqueue(ClientPacket)</c> had been making. Frames were framed and
///             queued correctly; nothing ever drained the queue.
///         </item>
///         <item>
///             The unknown-opcode gate asked <c>Codec.IsClientOpcodeRegistered</c>, which answered
///             false for every opcode because the codec had been scanning only Hybrasyl's assembly
///             since P1.
///         </item>
///     </list>
///     <para>
///         <see cref="InboundFrameUnwrapping" /> covers <c>InboundBody.FromFrame</c> in isolation and
///         would have caught neither: it proves the unwrapping is correct, not that anything calls
///         it. Every other test in the suite invokes its handler directly. The gap was the chain,
///         so these assert the chain — <c>ReceiveFrame</c> in, handler out.
///     </para>
///     <para>
///         The handler slot is swapped for a probe rather than relying on a real handler's side
///         effects, so the assertion is about delivery and body integrity and cannot be broken by
///         unrelated changes to lobby behaviour. Lobby is used because its dispatch is synchronous;
///         the World path queues to <c>MessageQueue</c> for another thread.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class ReceiveWiring
{
    public ReceiveWiring(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    private static ClientJoinPacket Join() => new()
    {
        EncryptionSeed = 0x0B,
        EncryptionKey = "NILCHIRSTNA"u8.ToArray(),
        Name = "Kerden",
        RedirectId = 0x12345678,
    };

    /// <summary>
    ///     Feeds <paramref name="packet" /> through the real receive path of a real
    ///     <see cref="Client" /> and returns the body the handler was handed, or null if no
    ///     handler ran.
    /// </summary>
    private static byte[]? DeliverToHandler(IClientPacket packet)
    {
        // Login rather than Lobby: the fixture does not stand a Lobby up, and Login dispatches
        // synchronously the same way. The ctor only seeds a key for Lobby clients, so set one
        // here — Normal-encrypted opcodes cannot be decrypted without it.
        var client = new Client(new TestSocket(), Game.Login) { EncryptionKey = "UrkcnItnI"u8.ToArray() };
        var original = Game.Login.PacketHandlers[packet.Opcode];
        byte[]? delivered = null;

        try
        {
            Game.Login.PacketHandlers[packet.Opcode] = (_, p) => delivered = p.Body.ToArray();

            var wire = Client.Codec.EncodeClient(packet, client.Crypto);
            client.ReceiveFrame(InboundFrame.FromWire(wire));
        }
        finally
        {
            Game.Login.PacketHandlers[packet.Opcode] = original;
            GlobalConnectionManifest.DeregisterClient(client);
        }

        return delivered;
    }

    /// <summary>
    ///     0x10 ClientJoin — <c>EncryptMethod.None</c>, so no ordinal and a trailing null to
    ///     strip. The plaintext branch of the unwrap, and the shape that sat unprocessed in the
    ///     queue during the first failed smoke.
    /// </summary>
    /// <remarks>
    ///     The exact-body assertion also carries the no-trailing-slack invariant, which is the one
    ///     the live regression violated: a handler reading one field too many got a 0x00 length
    ///     byte back and silently succeeded (<c>MerchantMenuHandler_BuyItemWithQuantity</c>'s dead
    ///     second <c>ReadString8</c>). A separate length-only test used to state that here; it was
    ///     removed as strictly weaker — verified by mutation, leaving the trailing null on the body
    ///     fails this assertion too.
    /// </remarks>
    [Fact]
    public void PlaintextOpcode_ReachesItsHandlerWithTheRightBody()
    {
        var packet = Join();

        var delivered = DeliverToHandler(packet);

        Assert.True(delivered is not null,
            "no handler ran: the frame never made it through the receive path");
        Assert.Equal(packet.ToBody(), delivered);
    }

    /// <summary>
    ///     0x03 Login — Normal-encrypted, so the frame carries an ordinal and the decrypted body
    ///     carries one byte of inner padding that must not reach the handler.
    /// </summary>
    [Fact]
    public void EncryptedOpcode_ReachesItsHandlerWithTheRightBody()
    {
        // The optional trailer fields are pinned because LoginPacket.WriteBody draws rand1,
        // xorKey and randData from Random.Shared when they are null — ToBody() is otherwise
        // non-deterministic and cannot be its own oracle.
        var packet = new LoginPacket
        {
            Name = "Kerden",
            Password = "leethax6",
            Rand1 = 0x11,
            XorKey = 0x22,
            ServerHash = 0x33445566,
            ClientHash = 0x7788,
            RandData = 0x99AABBCC,
        };

        var delivered = DeliverToHandler(packet);

        Assert.True(delivered is not null,
            "no handler ran: the frame never made it through the receive path");
        Assert.Equal(packet.ToBody(), delivered);
    }

    /// <summary>
    ///     One layer earlier again: bytes in at the socket callback, handler out. This covers
    ///     <c>Server.ReadCallback</c>'s <c>while (TryGetFrame) ReceiveFrame</c> loop, which is the
    ///     seam between framing and delivery.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The two tests above begin at <c>ReceiveFrame</c> and so own only the second half of
    ///         the chain. Deleting that loop from <c>ReadCallback</c> left the entire suite green
    ///         at 471/471 — framing tests and delivery tests each passing while nothing connected
    ///         them, which is the shape this branch has already shipped twice.
    ///     </para>
    ///     <para>
    ///         The callback is invoked directly rather than through <see cref="TestSocket" />'s
    ///         completion, because <c>ReadCallback</c> ends by re-arming the receive; see the
    ///         remarks on <c>TestSocket.BeginReceive</c>.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SocketCallback_DrivesFramingAndReachesTheHandler()
    {
        var socket = new TestSocket();
        var client = new Client(socket, Game.Login) { EncryptionKey = "UrkcnItnI"u8.ToArray() };
        var packet = Join();
        var original = Game.Login.PacketHandlers[packet.Opcode];
        byte[]? delivered = null;

        try
        {
            Game.Login.PacketHandlers[packet.Opcode] = (_, p) => delivered = p.Body.ToArray();

            socket.QueueReceive(Client.Codec.EncodeClient(packet, client.Crypto).ToArray());

            var state = client.ClientState;
            var ar = socket.BeginReceive(state.Buffer, state.BytesReceived,
                state.Buffer.Length - state.BytesReceived, System.Net.Sockets.SocketFlags.None, null, state);

            Game.Login.ReadCallback(ar);

            Assert.True(delivered is not null,
                "no handler ran: ReadCallback did not carry the bytes through framing to delivery");
            Assert.Equal(packet.ToBody(), delivered);
        }
        finally
        {
            Game.Login.PacketHandlers[packet.Opcode] = original;
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    /// <summary>
    ///     A frame arriving across two reads is buffered and reassembled, not dropped or
    ///     misjudged. This is the ordinary TCP case: nothing guarantees a frame arrives whole.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>ClientState.TryGetFrame</c> refuses to pop until
    ///         <c>BytesReceived >= packetLength</c>, so the first callback must leave the partial
    ///         frame alone and the second must complete it. The single-read test above cannot
    ///         distinguish that from a framing layer that ignores the byte count entirely.
    ///     </para>
    ///     <para>
    ///         This also holds <see cref="TestSocket" /> honest. It advertised split receives while
    ///         consuming eagerly in <c>BeginReceive</c>, which silently ate one queued receive per
    ///         callback through the re-arm in <c>ContinueReceiving</c> — so this test could not
    ///         have passed before that was fixed on 2026-08-07.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SocketCallback_ReassemblesAFrameSplitAcrossTwoReceives()
    {
        var socket = new TestSocket();
        var client = new Client(socket, Game.Login) { EncryptionKey = "UrkcnItnI"u8.ToArray() };
        var packet = Join();
        var original = Game.Login.PacketHandlers[packet.Opcode];
        byte[]? delivered = null;

        try
        {
            Game.Login.PacketHandlers[packet.Opcode] = (_, p) => delivered = p.Body.ToArray();

            var wire = Client.Codec.EncodeClient(packet, client.Crypto).ToArray();
            var split = wire.Length / 2;
            // The first read must carry the complete 3-byte header, so TryGetFrame knows the real
            // packet length and declines on the byte count. A first read shorter than the header
            // would defer for the weaker reason that it could not read a length at all.
            Assert.True(split > 3,
                $"precondition: the first read ({split} bytes) must contain the whole 3-byte header");

            socket.QueueReceive(wire[..split]);
            socket.QueueReceive(wire[split..]);

            Drive(client, socket);
            Assert.True(delivered is null,
                "a partial frame was dispatched: TryGetFrame popped before the whole frame arrived");

            Drive(client, socket);
            Assert.True(delivered is not null, "the completed frame never reached the handler");
            Assert.Equal(packet.ToBody(), delivered);
        }
        finally
        {
            Game.Login.PacketHandlers[packet.Opcode] = original;
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    /// <summary>One socket read: arm the buffer where the last one left off, then run the callback.</summary>
    private static void Drive(Client client, TestSocket socket)
    {
        var state = client.ClientState;
        var ar = socket.BeginReceive(state.Buffer, state.BytesReceived,
            state.Buffer.Length - state.BytesReceived, System.Net.Sockets.SocketFlags.None, null, state);

        Game.Login.ReadCallback(ar);
    }
}
