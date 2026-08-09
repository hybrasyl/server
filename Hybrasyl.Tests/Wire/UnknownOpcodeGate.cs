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

using System.Linq;
using DALib.Networking.Packets.Client;
using DALib.Networking.Wire;
using Hybrasyl.Networking;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     A World opcode with no registered handler is dropped before dispatch.
/// </summary>
/// <remarks>
///     <para>
///         The gate asked <c>WorldPacketHandlers.ContainsKey</c> until 2026-08-07, which is always
///         true — <c>Server</c>'s constructor pre-fills all 256 slots with an unhandled-opcode
///         logger. So the gate never fired, and the "rejected before dispatch" claim was a false
///         safety claim: an unregistered opcode was decrypted and unwrapped in full before reaching
///         a logger that discarded it. <see cref="Server.RegisteredWorldOpcodes" /> now holds only
///         the opcodes <c>SetPacketHandlers</c> bound to a real method.
///     </para>
///     <para>
///         The registration tests below check that the <em>predicate</em> discriminates. That is
///         necessary and was not sufficient: until 2026-08-07 nothing drove
///         <c>Client.FlushReceiveBuffer</c>, so reverting the production gate to the dead
///         <c>ContainsKey</c> form left the whole suite green at 471/471. An earlier version of
///         this remark asserted the gate's effect was "not visible from outside," which was wrong
///         and is why the gap survived — the World branch adds to <c>World.MessageQueue</c>, and a
///         frame that never arrives there is precisely the observable.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class UnknownOpcodeGate
{
    public UnknownOpcodeGate(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    /// <summary>
    ///     The predicate must separate real handlers from the fallback. This is the assertion that
    ///     fails against the old <c>ContainsKey</c> form.
    /// </summary>
    [Fact]
    public void RegistrationSetDistinguishesRealHandlersFromTheFallback()
    {
        var registered = Game.World.RegisteredWorldOpcodes;

        Assert.NotEmpty(registered);

        // Every slot is populated, so ContainsKey cannot answer this question.
        Assert.All(
            Enumerable.Range(0, 256).Select(i => (byte) i),
            opcode => Assert.True(Game.World.WorldPacketHandlers.ContainsKey(opcode)));

        // The registration set must be a strict subset, or it is no more informative.
        Assert.True(registered.Count < 256,
            $"expected fewer than 256 real handlers, got {registered.Count} — the set is not discriminating");
    }

    /// <summary>
    ///     Opcodes Hybrasyl actually handles are registered; ones it does not are not.
    /// </summary>
    [Theory]
    [InlineData(0x06, true)]  // Walk
    [InlineData(0x0F, true)]  // UseSpell
    [InlineData(0x2E, true)]  // GroupRequest
    [InlineData(0x39, true)]  // NpcMainMenu
    [InlineData(0x09, false)]
    [InlineData(0x12, false)]
    [InlineData(0x7F, false)]
    public void RegistrationReflectsWhatSetPacketHandlersBound(byte opcode, bool expected)
    {
        Assert.Equal(expected, Game.World.RegisteredWorldOpcodes.Contains(opcode));
    }

    /// <summary>Builds a raw C→S frame: <c>[0xAA][u16-BE len][opcode][body...]</c>.</summary>
    private static byte[] RawFrame(byte opcode, params byte[] body)
    {
        var frame = new byte[body.Length + 4];
        frame[0] = 0xAA;
        frame[1] = (byte) (((body.Length + 1) >> 8) & 0xFF);
        frame[2] = (byte) ((body.Length + 1) & 0xFF);
        frame[3] = opcode;
        body.CopyTo(frame, 4);
        return frame;
    }

    /// <summary>
    ///     The gate itself, driven through a real World client. This is the test whose absence let
    ///     the dead <c>ContainsKey</c> predicate sit in production undetected.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Both halves are in one test on purpose. The negative alone ("nothing reached the
    ///         queue") is satisfied by a receive path that delivers nothing at all — which is the
    ///         failure this branch has actually shipped, twice. The registered-opcode half is the
    ///         positive control that separates "the gate dropped it" from "the path is dead."
    ///     </para>
    ///     <para>
    ///         <c>Crypto.IsInitialized</c> is asserted as a precondition because the pre-key
    ///         discard guard sits <em>above</em> the registration gate in
    ///         <c>FlushReceiveBuffer</c>. Without a key, a Normal/MD5Key frame is dropped by that
    ///         guard instead, and this test would pass while proving nothing about registration.
    ///     </para>
    ///     <para>
    ///         <c>World.MessageQueue</c> is readable because its consumer thread is started only
    ///         by <c>Game.cs</c>, never by the fixture — so the queue accumulates rather than
    ///         draining underneath the assertion. What this test adds is taken back out.
    ///     </para>
    /// </remarks>
    [Fact]
    public void UnregisteredOpcodeIsDroppedBeforeReachingTheMessageQueue()
    {
        const byte registered = 0x0F;   // UseSpell
        const byte unregistered = 0x12; // no handler bound

        Assert.True(Game.World.RegisteredWorldOpcodes.Contains(registered), "precondition");
        Assert.False(Game.World.RegisteredWorldOpcodes.Contains(unregistered), "precondition");

        var client = new Client(new TestSocket(), Game.World)
        {
            EncryptionKey = "UrkcnItnI"u8.ToArray()
        };

        try
        {
            Assert.True(client.Crypto.IsInitialized,
                "precondition: without a key the pre-key guard drops the frame above the gate");

            // Positive control.
            var before = Hybrasyl.Servers.World.MessageQueue.Count;
            client.ReceiveFrame(InboundFrame.FromWire(
                Client.Codec.EncodeClient(new UseSpellPacket { Slot = 1 }, client.Crypto)));
            Assert.Equal(before + 1, Hybrasyl.Servers.World.MessageQueue.Count);

            // The gate.
            var afterControl = Hybrasyl.Servers.World.MessageQueue.Count;
            client.ReceiveFrame(InboundFrame.FromWire(
                RawFrame(unregistered, 0xDE, 0xAD, 0xBE, 0xEF)));
            Assert.Equal(afterControl, Hybrasyl.Servers.World.MessageQueue.Count);
        }
        finally
        {
            while (Hybrasyl.Servers.World.MessageQueue.TryTake(out _)) { }
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }
}
