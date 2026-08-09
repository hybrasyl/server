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
///         Asserting on the registration set rather than on log output, because the observable
///         difference the gate makes is the work it avoids, and that is not visible from outside.
///         What is checkable is that the predicate the gate consults actually discriminates —
///         which <c>ContainsKey</c> did not.
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
}
