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
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The 0x3A variant discrimination the converted handler branches on. Bodies are hand-written
///     from the field layout rather than produced by DALib's own writers — a round-trip through
///     <c>WriteBody</c> would agree with <c>Parse</c> by construction and could not catch a wrong
///     layout.
/// </summary>
/// <remarks>
///     Reduced on 2026-08-07. The 0x39 prefix and option-byte cases were removed: both are now
///     exercised through the real handler by <see cref="MerchantDispatchWiring" />, which reaches
///     <c>NpcMainMenuSelectPacket.ParseResponse</c> at <c>World.cs:3358</c> and
///     <c>NpcOptionResponsePacket.ParseResponse</c> at <c>Merchant.cs:535</c> with Hybrasyl-owned
///     side effects as the oracle. What remains is the discrimination itself, which no handler
///     test pins — see the two cases below.
/// </remarks>
public class DialogResponseVariants
{
    // 0x39/0x3A share a prefix; 0x3A adds the pursuit index.
    private static byte[] DialogPrefix() => [0x01, 0x00, 0x00, 0xAB, 0xCD, 0x00, 0x07, 0x00, 0x02];

    [Fact]
    public void DialogUse_NoTagIsNavigation()
    {
        // Previous/Next/Close carry no response tail.
        var parsed = DialogUsePacket.Parse(DialogPrefix());

        var nav = Assert.IsType<DialogNavigationPacket>(parsed);
        Assert.Equal(0x0007, nav.PursuitId);
        Assert.Equal(0x0002, nav.PursuitIndex);
        Assert.Null(nav.ResponseType);
    }

    /// <summary>
    ///     A text tail parses as the <em>text</em> variant specifically, not merely as
    ///     "not an option".
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Proposed for removal on 2026-08-07 as superseded by
    ///         <c>ReceivePathHandlerGuards.OptionsDialogActive_TextResponseIsRefusedWithoutRunningTheCallback</c>.
    ///         It is not. That test establishes the parse is <em>not</em>
    ///         <c>DialogOptionResponsePacket</c> — which a navigation parse would satisfy equally.
    ///         Production's <c>TextDialog</c> branch in <c>World.cs</c> requires
    ///         <c>DialogTextResponsePacket</c> by name, and this is the only assertion anywhere
    ///         that a text tail yields it.
    ///     </para>
    ///     <para>
    ///         <strong>The TextDialog happy path is not covered</strong> (not "cannot be"):
    ///         <c>TextDialog.HandleResponse</c> returns false without a script handler, and both
    ///         the refusal branch and a false return end in <c>ClearDialogState(); return;</c> —
    ///         identical observables. Covering it needs a Lua dialog fixture, which the test
    ///         project does not have yet. Until then this case carries the contract.
    ///     </para>
    /// </remarks>
    [Fact]
    public void DialogUse_TextTailParsesAsTextAndNotAsAnOption()
    {
        var parsed = DialogUsePacket.Parse(
            [.. DialogPrefix(), DialogUsePacket.TagText, 0x02, .. "no"u8]);

        Assert.IsNotType<DialogOptionResponsePacket>(parsed);
        Assert.IsType<DialogTextResponsePacket>(parsed);
    }
}
