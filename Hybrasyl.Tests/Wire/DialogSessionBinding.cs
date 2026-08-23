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

using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Dialogs;
using System;
using System.Buffers.Binary;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     A 0x3A response may only be handled for the object the dialog session is associated with.
/// </summary>
/// <remarks>
///     Range and association are different properties: range says the player could have reached the
///     object, association says the server is the one that handed it to them. MerchantInteractionRange
///     covers the first. This covers the second, which the range check does not imply -- two objects
///     can both be in the viewport while only one owns the session.
///     <para>
///     The association check lives in <c>DialogState.SetDialogIndex</c>, which on the response path is
///     not reached until after <c>HandleResponse</c> has already run. A JumpDialog option reaches
///     <c>StartDialog(invocation.Origin, ...)</c> from inside that handler, so the click reassociates
///     the session onto whatever object it named -- and the later check then compares against the
///     value the click just wrote.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class DialogSessionBinding
{
    private const string JumpTarget = "DialogSessionBindingJumpTarget";

    public DialogSessionBinding(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    /// <summary>The 0x3A option-response wire body: prefix plus a one-option tail.</summary>
    private static byte[] OptionResponse(uint objectId, uint pursuitId, int pursuitIndex)
    {
        var body = new byte[9];
        body[0] = 0x01;
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(1), objectId);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(5), (ushort) pursuitId);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(7), (ushort) pursuitIndex);

        return [.. body, 0x01, 0x01];
    }

    /// <summary>
    ///     A one-option menu whose option jumps to <see cref="JumpTarget" />, registered on
    ///     <paramref name="jumpHost" /> so that JumpDialog resolves it without touching global state.
    /// </summary>
    private static DialogSequence MenuWithJumpOption(Merchant jumpHost, uint pursuitId)
    {
        var destination = new DialogSequence(JumpTarget) { Id = pursuitId + 1 };
        destination.AddDialog(new OptionsDialog("You have arrived."));
        jumpHost.SequenceIndex[JumpTarget] = destination;

        var menu = new OptionsDialog("Which one?");
        menu.AddDialogOption(new DialogOption
        {
            OptionText = "Jump",
            JumpDialog = new JumpDialog(JumpTarget)
        });

        var sequence = new DialogSequence("DialogSessionBindingProbe") { Id = pursuitId };
        sequence.AddDialog(menu);

        return sequence;
    }

    private (Merchant here, Merchant elsewhere) TwoMarias()
    {
        var here = Fixture.Map.Objects.OfType<Merchant>().Single(x => x.Name == "Maria");
        var elsewhere = Game.World.Objects.Values.OfType<Merchant>()
            .First(x => x.Name == "Maria" && x.Location.Map != Fixture.Map);

        Assert.True(Fixture.TestUser.GetViewport().Contains(here.X, here.Y),
            "precondition: the fixture's merchant must be inside the user's viewport");

        return (here, elsewhere);
    }

    /// <summary>
    ///     The positive control. Without this, a fix that refuses every response would leave the
    ///     defect test green while breaking every dialog in the world -- and nothing else in the
    ///     suite drives a 0x3A response through the handler to find out.
    /// </summary>
    [Fact]
    public void TheAssociatedObject_StillDrivesItsOwnDialog()
    {
        var (here, _) = TwoMarias();
        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var sequence = MenuWithJumpOption(here, pursuitId);

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(here, sequence),
            "couldn't start the probe dialog");
        var indexBefore = Fixture.TestUser.DialogState.CurrentPursuitIndex;

        try
        {
            Game.World.WorldPacketHandlers[0x3A].Invoke(Fixture.TestUser,
                new InboundBody(0x3A, OptionResponse(here.Id, pursuitId, indexBefore + 1)));

            Assert.True(Fixture.TestUser.DialogState.InDialog,
                "a legitimate option response ended the session");
            Assert.Same(here, Fixture.TestUser.DialogState.Associate);
            Assert.Equal(JumpTarget, Fixture.TestUser.DialogState.ActiveDialogSequence?.Name);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
            here.SequenceIndex.Remove(JumpTarget);
        }
    }

    /// <summary>
    ///     The defect. The named object is inside the viewport, so the range check that HS-09 added
    ///     passes; it simply is not the object the session was opened against.
    /// </summary>
    [Fact]
    public void AnUnassociatedObject_CannotSeizeTheDialogSession()
    {
        var (here, elsewhere) = TwoMarias();
        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var sequence = MenuWithJumpOption(here, pursuitId);

        // The session belongs to the merchant on the other map; the click names the one in range.
        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(elsewhere, sequence),
            "couldn't start the probe dialog");
        var indexBefore = Fixture.TestUser.DialogState.CurrentPursuitIndex;

        try
        {
            Game.World.WorldPacketHandlers[0x3A].Invoke(Fixture.TestUser,
                new InboundBody(0x3A, OptionResponse(here.Id, pursuitId, indexBefore + 1)));

            Assert.NotSame(here, Fixture.TestUser.DialogState.Associate);
            Assert.NotEqual(JumpTarget, Fixture.TestUser.DialogState.ActiveDialogSequence?.Name);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
            here.SequenceIndex.Remove(JumpTarget);
        }
    }
}
