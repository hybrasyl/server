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
using System.Buffers.Binary;
using System.Linq;
using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Dialogs;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Closing a dialog is subject to the same ownership rule as driving one.
/// </summary>
/// <remarks>
///     A 0x3A response whose pursuit id and index both match the live session is read as "the
///     client closed the dialog", and it clears dialog state and tears down any async session.
///     That is a mutation, so it has to sit below the ownership guard: while it sat above, any
///     resolvable in-range interactable could end a session opened against something else.
/// </remarks>
[Collection("Hybrasyl")]
public class DialogCloseAuthorization(HybrasylFixture fixture)
{
    private HybrasylFixture Fixture { get; } = fixture;

    private static byte[] CloseResponse(uint objectId, uint pursuitId, int pursuitIndex)
    {
        var body = new byte[9];
        body[0] = 0x01;
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(1), objectId);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(5), (ushort) pursuitId);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(7), (ushort) pursuitIndex);

        return [.. body, 0x01, 0x01];
    }

    private (Merchant here, Merchant elsewhere) TwoMarias()
    {
        var here = Fixture.Map.Objects.OfType<Merchant>().Single(predicate: x => x.Name == "Maria");
        var elsewhere = Game.World.Objects.Values.OfType<Merchant>()
            .First(predicate: x => x.Name == "Maria" && x.Location.Map != Fixture.Map);

        Assert.True(Fixture.TestUser.GetViewport().Contains(here.X, here.Y),
            "precondition: the fixture's merchant must be inside the user's viewport");

        return (here, elsewhere);
    }

    [Fact]
    public void AnUnassociatedObject_CannotCloseTheDialogSession()
    {
        var (here, elsewhere) = TwoMarias();
        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var sequence = new DialogSequence("DialogCloseAuthorizationProbe") { Id = pursuitId };
        sequence.AddDialog(new OptionsDialog("Which one?"));

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(elsewhere, sequence),
            "couldn't start the probe dialog");

        var index = Fixture.TestUser.DialogState.CurrentPursuitIndex;

        try
        {
            Game.World.WorldPacketHandlers[0x3A].Invoke(Fixture.TestUser,
                new InboundBody(0x3A, CloseResponse(here.Id, pursuitId, index)));

            Assert.True(Fixture.TestUser.DialogState.InDialog,
                "an object that does not own the session closed it");
            Assert.Same(elsewhere, Fixture.TestUser.DialogState.Associate);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
        }
    }

    /// <summary>
    ///     The positive control: the owner can still close its own dialog. Without this, moving the
    ///     close below the guard could refuse every close and leave the test above green.
    /// </summary>
    [Fact]
    public void TheAssociatedObject_StillClosesItsOwnDialog()
    {
        var (here, _) = TwoMarias();
        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var sequence = new DialogSequence("DialogCloseAuthorizationProbe") { Id = pursuitId };
        sequence.AddDialog(new OptionsDialog("Which one?"));

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(here, sequence),
            "couldn't start the probe dialog");

        var index = Fixture.TestUser.DialogState.CurrentPursuitIndex;

        try
        {
            Game.World.WorldPacketHandlers[0x3A].Invoke(Fixture.TestUser,
                new InboundBody(0x3A, CloseResponse(here.Id, pursuitId, index)));

            Assert.False(Fixture.TestUser.DialogState.InDialog,
                "the owning object could not close its own dialog");
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
        }
    }
}
