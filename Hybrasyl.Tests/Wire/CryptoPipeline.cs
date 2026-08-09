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
using Hybrasyl.Networking;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The keyless-connection guards in <see cref="Client.FlushSendBuffer" /> and
///     <see cref="Client.FlushReceiveBuffer" />, both exercised through the real flush loops.
///     <para>
///         The receive half was described here as uncoverable at this boundary until 2026-08-07.
///         That was wrong twice over: the claim was reached by probing an entry point production
///         does not use, and the underlying reason it looked unobservable was a live defect —
///         <c>ReceiveFrame</c> enqueued without flushing, so the frame sat in an unbounded queue
///         forever. With that fixed, queue state distinguishes discard from retention exactly.
///     </para>
/// </summary>
/// <remarks>
///     <para>
///         These guards had no coverage at all. The two tests that claimed to cover them
///         (<c>NullableRegressions.DecryptBeforeKeyExchangeReportsFailure</c> and its send-side
///         twin) asserted DALib's opcode table plus a constructor-set flag and never called
///         either flush method — deleting both guards left the whole suite green at 459/459.
///     </para>
///     <para>
///         The guard matters because a Normal-mode packet on a connection that has not completed
///         the key exchange is reachable by crafted traffic straight to the port, and DALib
///         divides by zero on an empty key. The pre-DALib code NRE'd on a null key at the same
///         point.
///     </para>
///     <para>
///         Driven against the real <see cref="Client" />, not <c>TestClient</c> — the latter is a
///         separate implementation with no flush loops, which is exactly why this seam went
///         untested. <see cref="TestSocket" /> records outbound buffers so the send half can be
///         observed at the wire.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class CryptoPipeline
{
    private static readonly TimeSpan SendWait = TimeSpan.FromSeconds(5);

    // Long enough that a send would have landed; only used for negative assertions, where the
    // drop path has already returned synchronously before Task.Run is ever reached.
    private static readonly TimeSpan DropWait = TimeSpan.FromMilliseconds(250);

    private const byte NormalServerOpcode = 0x0A;   // SystemMessage
    private const byte NoneServerOpcode = 0x7E;     // AcceptConnection
    private const byte Md5KeyServerOpcode = 0x08;   // Attributes
    private const byte NormalClientOpcode = 0x02;   // LoginMessage

    /// <summary>
    ///     A real Client wired to a capturing socket, bypassing the socket constructor's
    ///     connection registration and lobby key negotiation.
    /// </summary>
    private static (Client client, TestSocket socket) Keyless()
    {
        var socket = new TestSocket();

        return (new Client { ClientState = new ClientState(socket) }, socket);
    }

    private static (Client client, TestSocket socket) Keyed()
    {
        var (client, socket) = Keyless();
        client.EncryptionKey = "UrkcnItnI"u8.ToArray();

        return (client, socket);
    }

    private static byte[] Body(params byte[] bytes) => bytes;

    // ---- send half ----

    /// <summary>
    ///     The guard's reason for existing: a Normal-mode response queued before the key exchange
    ///     is dropped rather than handed to a codec that would divide by zero on the empty key.
    /// </summary>
    /// <remarks>
    ///     The queue holds a second, sendable packet behind the one that must be dropped, and the
    ///     assertion is that the second one still reaches the wire. Asserting only "nothing was
    ///     sent" does not test the guard at all: without it the encode throws, the catch-all in
    ///     <see cref="Client.FlushSendBuffer" /> swallows it, and nothing is sent either way — the
    ///     guard and the exception share an observable. What separates them is that the guard's
    ///     <c>continue</c> keeps the loop running while the exception escapes the whole
    ///     <c>while</c>, so everything queued behind the bad packet is lost with it. Verified:
    ///     neutering the guard fails this test, and asserting on absence alone did not.
    /// </remarks>
    [Fact]
    public void KeylessSend_DropsNormalModePacketsWithoutLosingTheRestOfTheBatch()
    {
        var (client, socket) = Keyless();
        Assert.False(client.Crypto.IsInitialized, "precondition: no negotiated key");

        client.Enqueue(new RawBodyPacket(NormalServerOpcode, Body(0x01, 0x02, 0x03)));
        client.Enqueue(new RawBodyPacket(NoneServerOpcode, Body(0x11, 0x22)));
        client.FlushSendBuffer();

        Assert.True(socket.TryTakeSent(SendWait, out var wire),
            "the keyless-safe packet queued behind the dropped one must still be sent");

        // Only the None packet: the Normal one was dropped, not encoded.
        Assert.Equal(NoneServerOpcode, wire[3]);
        Assert.False(socket.TryTakeSent(DropWait, out _),
            "the Normal-mode packet must not have reached the wire");
    }

    /// <summary>
    ///     The other side of the same branch — with a key, the identical packet encodes and is
    ///     framed. Without this, a guard that dropped *everything* would pass the test above.
    /// </summary>
    [Fact]
    public void KeyedSend_EncodesNormalModePacketsAndFramesThem()
    {
        var (client, socket) = Keyed();
        Assert.True(client.Crypto.IsInitialized, "precondition: key present");

        client.Enqueue(new RawBodyPacket(NormalServerOpcode, Body(0x01, 0x02, 0x03)));
        client.FlushSendBuffer();

        Assert.True(socket.TryTakeSent(SendWait, out var wire),
            "a Normal-mode packet must reach the wire once the key is negotiated");
        Assert.Equal(0xAA, wire[0]);
        Assert.Equal(wire.Length - 3, (wire[1] << 8) | wire[2]);
        Assert.Equal(NormalServerOpcode, wire[3]);
    }

    /// <summary>
    ///     None and MD5Key must NOT be caught by the guard: None needs no key at all, MD5Key
    ///     tolerates the zeroed pre-world table. A guard widened to "any opcode without a key"
    ///     would silently break the login handshake, which is carried entirely by None frames.
    /// </summary>
    [Theory]
    [InlineData(NoneServerOpcode)]
    [InlineData(Md5KeyServerOpcode)]
    public void KeylessSend_StillEmitsOpcodesThatDoNotNeedTheNegotiatedKey(byte opcode)
    {
        var (client, socket) = Keyless();

        client.Enqueue(new RawBodyPacket(opcode, Body(0x11, 0x22)));
        client.FlushSendBuffer();

        Assert.True(socket.TryTakeSent(SendWait, out var wire),
            $"0x{opcode:X2} does not need the negotiated key and must still be sent");
        Assert.Equal(0xAA, wire[0]);
        Assert.Equal(opcode, wire[3]);
    }

    // ---- receive half ----

    /// <summary>
    ///     A Normal-mode frame arriving before the key exchange is discarded, and the queue drains.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This class previously carried a note claiming the receive guard was unobservable at
    ///         this boundary, on the grounds that a wrong decrypt throws and is caught per-frame so
    ///         nothing reaches a handler either way. That was true of the inner guard and wrong
    ///         about the path: <see cref="Client.ReceiveFrame" /> declined to flush at all for a
    ///         keyless Normal frame, so the frame stayed in an unbounded queue permanently and the
    ///         inner guard was never reached. Queue state distinguishes the two perfectly — this
    ///         test failed before the fix.
    ///     </para>
    ///     <para>
    ///         The accumulation was the real defect. Nothing else drains that queue, and the key
    ///         never arrives on a connection sending crafted pre-key traffic, so repeated frames
    ///         grew without limit.
    ///     </para>
    /// </remarks>
    [Fact]
    public void KeylessReceive_DiscardsTheFrameAndDrainsTheQueue()
    {
        var (client, _) = Keyless();
        Assert.Equal(EncryptMethod.Normal, CryptoState.GetClientEncryptMethod(NormalClientOpcode));
        Assert.False(client.Crypto.IsInitialized, "precondition: no negotiated key");

        client.ReceiveFrame(CraftedFrame(NormalClientOpcode));

        Assert.False(client.ClientState.ReceiveBufferTake(out _),
            "a keyless Normal-mode frame must be discarded, not left queued: the receive buffer is "
            + "unbounded and nothing else drains it");
    }

    /// <summary>
    ///     Repeated crafted frames do not accumulate. The single-frame case above would pass on a
    ///     path that merely dropped the first one.
    /// </summary>
    [Fact]
    public void KeylessReceive_RepeatedFramesDoNotAccumulate()
    {
        var (client, _) = Keyless();

        for (var i = 0; i < 32; i++)
            client.ReceiveFrame(CraftedFrame(NormalClientOpcode));

        Assert.False(client.ClientState.ReceiveBufferTake(out _), "queue must be empty after 32 crafted frames");
    }

    /// <summary>
    ///     Crafted traffic straight to the port: a frame whose ciphertext is garbage.
    /// </summary>
    private static InboundFrame CraftedFrame(byte opcode)
    {
        // Long enough to clear the padding strip in InboundFrame.FromFrame.
        var payload = new byte[]
        {
            opcode, 0x00,
            0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C
        };
        var wire = new byte[payload.Length + 3];
        wire[0] = 0xAA;
        wire[1] = (byte) ((payload.Length >> 8) & 0xFF);
        wire[2] = (byte) (payload.Length & 0xFF);
        payload.CopyTo(wire, 3);

        return InboundFrame.FromWire(wire);
    }
}
