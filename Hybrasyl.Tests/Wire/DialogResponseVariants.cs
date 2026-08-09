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
///     The 0x39/0x3A response-variant mapping the converted handlers depend on.
///     Bodies here are hand-written from the field layout rather than produced by DALib's own
///     writers — a round-trip through <c>WriteBody</c> would agree with <c>Parse</c> by
///     construction and could not catch a wrong layout.
/// </summary>
public class DialogResponseVariants
{
    // 0x39/0x3A share a prefix; 0x3A adds the pursuit index.
    private static byte[] MenuPrefix() => [0x01, 0x00, 0x00, 0xAB, 0xCD, 0xFF, 0x11];

    private static byte[] DialogPrefix() => [0x01, 0x00, 0x00, 0xAB, 0xCD, 0x00, 0x07, 0x00, 0x02];

    [Fact]
    public void MainMenuSelect_ReadsPrefixAndIgnoresAnyTail()
    {
        // The 0x39 handler parses this form for every response; a merchant callback then
        // re-parses the same body as whichever variant its own menu carries.
        var parsed = NpcMainMenuSelectPacket.ParseResponse([.. MenuPrefix(), 0x05, .. "Beryl"u8]);

        Assert.Equal(0x01, parsed.ObjectType);
        Assert.Equal(0x0000ABCDu, parsed.ObjectId);
        Assert.Equal(0xFF11, parsed.PursuitId);
    }

    [Fact]
    public void MainMenuOptionResponse_ReadsTheTrailingByte()
    {
        var parsed = NpcOptionResponsePacket.ParseResponse([.. MenuPrefix(), 0x03]);

        Assert.Equal(0xFF11, parsed.PursuitId);
        Assert.Equal(0x03, parsed.Option);
    }

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

    // Tag1/Tag2 field-level parsing is covered upstream by DALib's
    // DialogUsePacketTests Parse_Tag01_IsOptionResponse / Parse_Tag02_IsTextResponse, both of
    // which parse hand-built bodies. Kept here: choice indexes arrive one-based, and
    // OptionsDialog.HandleResponse indexes accordingly.
    [Fact]
    public void DialogUse_DispatchIsWhatLetsAMismatchedResponseBeSeen()
    {
        // The handler branches on the server's idea of the active dialog, then asserts
        // the wire shape agrees. The legacy positional read had no way to notice a
        // disagreement — it simply took the next byte as the option. This is the case that
        // used to be invisible: a text submission arriving while an options dialog is open.
        var parsed = DialogUsePacket.Parse(
            [.. DialogPrefix(), DialogUsePacket.TagText, 0x02, .. "no"u8]);

        Assert.IsNotType<DialogOptionResponsePacket>(parsed);
        Assert.IsType<DialogTextResponsePacket>(parsed);
    }
}
