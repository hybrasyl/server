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
///     The keyless-connection guard in <see cref="Client.FlushSendBuffer" />, exercised through
///     the real flush loop. The receive-side guard is deliberately not covered — see the note at
///     the foot of this class for why it cannot be, at this boundary.
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

    // ---- receive half: deliberately not covered here ----

    // The FlushReceiveBuffer keyless guard is NOT tested, and a test was removed rather than
    // left in place looking like coverage. It is not observable at this boundary: with the guard
    // neutered the frame still never reaches a handler, because InboundPacket.FromFrame throws
    // DivideByZeroException on the empty key (verified directly) and the loop's own per-frame
    // catch logs it and continues. Guard and exception produce the same observable, so any
    // assertion on dispatch or on queue drain passes either way — confirmed by mutating the
    // flush-loop guard, ReceiveFrame's outer gate, and both together. All three stayed green.
    //
    // There are two guards on this path: ReceiveFrame declines to call FlushReceiveBuffer at all
    // for a keyless Normal frame, so the inner one is unreachable through the normal entry point.
    // What removing either actually changes is which warning is logged and whether an exception
    // is constructed per crafted packet — a cost and clarity property, not a behavioural one.
    // Pinning it needs a capturing Serilog sink over the global Log.Logger; that is shared
    // mutable state across this xunit collection and was judged not worth it for defence in
    // depth. See task notes if that judgement is revisited.
}
