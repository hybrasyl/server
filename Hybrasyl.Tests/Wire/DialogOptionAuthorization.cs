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

using Hybrasyl.Casting;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Subsystems.Scripting;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     An option a check expression hid may not be selected, and the selection index means the same
///     list on the way in as it did on the way out.
/// </summary>
/// <remarks>
///     ShowTo sends the options that passed their check expression; HandleResponse indexed the full
///     list. The two agreed only because RunCheckExpression returned <c>!b || b</c> and therefore
///     never hid anything -- so the tautology was holding an off-by-N off. Resolving the response
///     against the same filtered list is what makes the check load-bearing, and is also what makes a
///     hidden option unselectable: it is not in the list to index into.
/// </remarks>
[Collection("Hybrasyl")]
public class DialogOptionAuthorization
{
    private const string Hidden = "DialogOptionAuthHiddenTarget";
    private const string Visible = "DialogOptionAuthVisibleTarget";

    public DialogOptionAuthorization(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    /// <summary>
    ///     A castable is used as the dialog's associate because check expressions are evaluated
    ///     against <c>invocation.Script</c>, and a CastableObject is the interactable in the fixture
    ///     world that reliably carries one.
    /// </summary>
    private (OptionsDialog menu, DialogInvocation invocation, CastableObject castable) Menu()
    {
        var castable = Game.World.WorldState.Values<CastableObject>().FirstOrDefault();
        Assert.True(castable is not null,
            "precondition: the fixture world must load at least one scripted castable");

        foreach (var name in new[] { Hidden, Visible })
        {
            var destination = new DialogSequence(name) { Id = (uint) (name == Hidden ? 90001 : 90002) };
            destination.AddDialog(new OptionsDialog($"arrived at {name}"));
            castable!.SequenceIndex[name] = destination;
        }

        var menu = new OptionsDialog("Which one?");
        // First in the list and hidden: if the response is resolved against the unfiltered list, a
        // selection of 1 lands here.
        menu.AddDialogOption(new DialogOption
        {
            OptionText = "Hidden",
            CheckExpression = "return false",
            JumpDialog = new JumpDialog(Hidden)
        });
        menu.AddDialogOption(new DialogOption
        {
            OptionText = "Visible",
            JumpDialog = new JumpDialog(Visible)
        });

        var sequence = new DialogSequence("DialogOptionAuthProbe") { Id = 90000 };
        sequence.AddDialog(menu);

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(castable, sequence),
            "couldn't start the probe dialog");

        return (menu, new DialogInvocation(castable!, Fixture.TestUser, Fixture.TestUser), castable!);
    }

    private void Cleanup(CastableObject castable)
    {
        Fixture.TestUser.DialogState.EndDialog();
        castable.SequenceIndex.Remove(Hidden);
        castable.SequenceIndex.Remove(Visible);
    }

    /// <summary>
    ///     Selecting the first option the client was shown must reach the first option the server
    ///     displayed, not the first option it holds.
    /// </summary>
    [Fact]
    public void TheFirstShownOption_IsTheFirstSelectableOption()
    {
        var (menu, invocation, castable) = Menu();

        try
        {
            Assert.True(menu.HandleResponse(1, invocation), "the visible option was refused");
            Assert.Equal(Visible, Fixture.TestUser.DialogState.ActiveDialogSequence?.Name);
        }
        finally
        {
            Cleanup(castable);
        }
    }

    /// <summary>
    ///     A hidden option is not addressable at all: with one option displayed, index 2 is past the
    ///     end rather than a way to reach the option that was filtered out.
    /// </summary>
    [Fact]
    public void AHiddenOption_CannotBeReachedByIndex()
    {
        var (menu, invocation, castable) = Menu();

        try
        {
            Assert.False(menu.HandleResponse(2, invocation),
                "an index past the displayed options was accepted");
            Assert.NotEqual(Hidden, Fixture.TestUser.DialogState.ActiveDialogSequence?.Name);
        }
        finally
        {
            Cleanup(castable);
        }
    }

    /// <summary>
    ///     Zero passed the guard, which rejected only negatives and overflow, and then indexed -1.
    /// </summary>
    [Fact]
    public void AZeroOptionIndex_IsRefusedRatherThanThrowing()
    {
        var (menu, invocation, castable) = Menu();

        try
        {
            Assert.False(menu.HandleResponse(0, invocation), "a zero selection was accepted");
        }
        finally
        {
            Cleanup(castable);
        }
    }

    /// <summary>
    ///     The seam between what ShowTo sends and what HandleResponse resolves. The three tests above
    ///     call HandleResponse directly, so a ShowTo that filtered differently -- or not at all --
    ///     would leave every one of them green while the wire indices disagreed.
    /// </summary>
    [Fact]
    public void ShowTo_SendsOnlyTheOptionsThatPassedTheirCheck()
    {
        var (menu, _, castable) = Menu();
        var client = HybrasylFixture.AttachTestClient(Fixture.TestUser, out var restore);

        try
        {
            menu.ShowTo(new DialogInvocation(castable, Fixture.TestUser, Fixture.TestUser));

            Assert.True(client.ClientState.SendBufferTake(out var queued),
                "the options dialog sent nothing");
            var record = Assert.IsAssignableFrom<DALib.Networking.Wire.ServerPacket>(queued.Packet);
            var writer = new DALib.Networking.Wire.PacketWriter();
            record.WriteBody(writer);
            var body = Encoding.ASCII.GetString(writer.WrittenSpan.ToArray());

            Assert.Contains("Visible", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Hidden", body, StringComparison.Ordinal);
        }
        finally
        {
            restore.Dispose();
            Cleanup(castable);
        }
    }

    /// <summary>
    ///     With every option hidden there is nothing to answer, and the player must not be left
    ///     holding a session with no packet from which to close it.
    /// </summary>
    [Fact]
    public void AnAllHiddenMenu_DoesNotStrandThePlayerInADialog()
    {
        var castable = Game.World.WorldState.Values<CastableObject>().FirstOrDefault();
        Assert.True(castable is not null, "precondition: a scripted castable must exist");

        var menu = new OptionsDialog("Nothing for you");
        menu.AddDialogOption(new DialogOption { OptionText = "Hidden", CheckExpression = "return false" });
        var sequence = new DialogSequence("DialogOptionAuthAllHidden") { Id = 90003 };
        sequence.AddDialog(menu);

        var client = HybrasylFixture.AttachTestClient(Fixture.TestUser, out var restore);
        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(castable, sequence),
            "couldn't start the probe dialog");

        try
        {
            menu.ShowTo(new DialogInvocation(castable!, Fixture.TestUser, Fixture.TestUser));

            Assert.False(Fixture.TestUser.DialogState.InDialog,
                "the player is still in a dialog with no options to answer");

            // Ending the state server-side is not enough: without a close packet the client keeps
            // whatever dialog it is already displaying, which is the case that matters when an
            // all-hidden menu follows a visible one.
            Assert.True(client.ClientState.SendBufferTake(out var queued),
                "no packet was sent, so the client was never told to close");
            Assert.Equal(0x30, queued.Opcode);
        }
        finally
        {
            restore.Dispose();
            Fixture.TestUser.DialogState.EndDialog();
        }
    }

    /// <summary>
    ///     An async session lives on both participants in <c>ActiveDialogSession</c>, which is not
    ///     part of <c>DialogState</c> -- so ending the dialog state alone closes the UI and leaves
    ///     both of them marked busy for every later request.
    /// </summary>
    [Fact]
    public void AnAllHiddenAsyncMenu_TearsDownTheSessionForBothParticipants()
    {
        var source = Fixture.SecondTestUser;
        var target = Fixture.TestUser;

        var menu = new OptionsDialog("Nothing for you");
        menu.AddDialogOption(new DialogOption { OptionText = "Hidden", CheckExpression = "return false" });
        var sequence = new DialogSequence("DialogOptionAuthAsyncAllHidden") { Id = 90004 };
        sequence.AddDialog(menu);

        var castable = Game.World.WorldState.Values<CastableObject>().FirstOrDefault();
        Assert.True(castable is not null, "precondition: a scripted castable must exist");
        castable!.SequenceIndex[sequence.Name] = sequence;

        var targetClient = HybrasylFixture.AttachTestClient(target, out var restoreTarget);
        var sourceClient = HybrasylFixture.AttachTestClient(source, out var restoreSource);

        try
        {
            var session = new AsyncDialogSession(sequence.Name, castable, source, target);
            Assert.True(session.Start(), "couldn't start the async session");
            Assert.Same(session, target.ActiveDialogSession);
            Assert.Same(session, source.ActiveDialogSession);

            target.DialogState.EndDialog();
            Assert.True(target.DialogState.StartDialog(session, sequence),
                "couldn't start the probe dialog");

            menu.ShowTo(new DialogInvocation(castable, target, source));

            Assert.Null(target.ActiveDialogSession);
            Assert.Null(source.ActiveDialogSession);
        }
        finally
        {
            restoreSource.Dispose();
            restoreTarget.Dispose();
            target.ActiveDialogSession = null;
            source.ActiveDialogSession = null;
            target.DialogState.EndDialog();
            castable.SequenceIndex.Remove(sequence.Name);
        }
    }
}
