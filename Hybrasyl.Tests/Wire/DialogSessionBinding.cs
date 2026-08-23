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
using Hybrasyl.Xml.Objects;
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

    /// <summary>
    ///     The same defect through the CastableObject branch, which resolves a client-supplied id from
    ///     WorldState with no ownership test of its own.
    /// </summary>
    /// <remarks>
    ///     These are built once at world load, one per castable template, with a dense sequential id
    ///     from zero -- so the id space is enumerable rather than guessable. A castable dialog is
    ///     associated by its script calling StartSequence with the origin it was handed, which is the
    ///     CastableObject; nothing else can legitimately own such a session.
    /// </remarks>
    [Fact]
    public void AnUnassociatedCastable_CannotSeizeTheDialogSession()
    {
        var (here, _) = TwoMarias();
        var castable = Game.World.WorldState.Values<CastableObject>().FirstOrDefault();
        Assert.True(castable is not null,
            "precondition: the fixture world must load at least one scripted castable");

        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var sequence = MenuWithJumpOption(here, pursuitId);

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(here, sequence),
            "couldn't start the probe dialog");
        var indexBefore = Fixture.TestUser.DialogState.CurrentPursuitIndex;

        try
        {
            // objectType 0x05 is DialogObjectType.CastableObject.
            var wire = OptionResponse(castable!.Id, pursuitId, indexBefore + 1);
            wire[0] = 0x05;
            Game.World.WorldPacketHandlers[0x3A].Invoke(Fixture.TestUser, new InboundBody(0x3A, wire));

            Assert.Same(here, Fixture.TestUser.DialogState.Associate);
            Assert.NotEqual(JumpTarget, Fixture.TestUser.DialogState.ActiveDialogSequence?.Name);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
            here.SequenceIndex.Remove(JumpTarget);
        }
    }

    /// <summary>
    ///     The positive control for the branch above: a castable that does own the session still
    ///     drives its own dialog, so the check binds association rather than refusing 0x05 outright.
    /// </summary>
    [Fact]
    public void TheAssociatedCastable_StillDrivesItsOwnDialog()
    {
        var castable = Game.World.WorldState.Values<CastableObject>().FirstOrDefault();
        Assert.True(castable is not null,
            "precondition: the fixture world must load at least one scripted castable");

        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var destination = new DialogSequence(JumpTarget) { Id = pursuitId + 1 };
        destination.AddDialog(new OptionsDialog("You have arrived."));
        castable!.SequenceIndex[JumpTarget] = destination;

        var menu = new OptionsDialog("Which one?");
        menu.AddDialogOption(new DialogOption { OptionText = "Jump", JumpDialog = new JumpDialog(JumpTarget) });
        var sequence = new DialogSequence("DialogSessionBindingCastableProbe") { Id = pursuitId };
        sequence.AddDialog(menu);

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(castable, sequence),
            "couldn't start the probe dialog");
        var indexBefore = Fixture.TestUser.DialogState.CurrentPursuitIndex;

        try
        {
            var wire = OptionResponse(castable.Id, pursuitId, indexBefore + 1);
            wire[0] = 0x05;
            Game.World.WorldPacketHandlers[0x3A].Invoke(Fixture.TestUser, new InboundBody(0x3A, wire));

            Assert.True(Fixture.TestUser.DialogState.InDialog,
                "a legitimate castable option response ended the session");
            Assert.Same(castable, Fixture.TestUser.DialogState.Associate);
            Assert.Equal(JumpTarget, Fixture.TestUser.DialogState.ActiveDialogSequence?.Name);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
            castable.SequenceIndex.Remove(JumpTarget);
        }
    }

    /// <summary>
    ///     A reactor is interactable, in-world and not a Creature, so an ownership check that named
    ///     the types it knew and let the rest through admitted it.
    /// </summary>
    /// <remarks>
    ///     This is the case that decided the default: OwnsSession denies anything not established as
    ///     able to own a session, rather than permitting anything it does not recognise.
    /// </remarks>
    [Fact]
    public void AnUnassociatedReactor_CannotSeizeTheDialogSession()
    {
        var (here, _) = TwoMarias();

        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.Stats.Level = 41;
        Fixture.TestUser.Teleport(Fixture.Map.Id, 20, 20);
        var trap = Game.World.WorldData.GetByIndex<Castable>("TestTrapMulti");
        Assert.True(Fixture.TestUser.AddSkill(trap, 1), "couldn't add the trap castable");
        Assert.True(Fixture.TestUser.UseCastable(trap), "UseCastable failed");

        var reactor = Fixture.Map.Reactors[(Fixture.TestUser.X, Fixture.TestUser.Y)].Values.First();
        Assert.True(Game.World.Objects.ContainsKey(reactor.Id),
            "precondition: the reactor must be resolvable from World.Objects, as 0x3A resolves it");

        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var sequence = MenuWithJumpOption(here, pursuitId);
        reactor.SequenceIndex[JumpTarget] = new DialogSequence(JumpTarget) { Id = pursuitId + 2 };

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(here, sequence),
            "couldn't start the probe dialog");
        var indexBefore = Fixture.TestUser.DialogState.CurrentPursuitIndex;

        try
        {
            Game.World.WorldPacketHandlers[0x3A].Invoke(Fixture.TestUser,
                new InboundBody(0x3A, OptionResponse(reactor.Id, pursuitId, indexBefore + 1)));

            Assert.Same(here, Fixture.TestUser.DialogState.Associate);
            Assert.NotEqual(JumpTarget, Fixture.TestUser.DialogState.ActiveDialogSequence?.Name);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
            here.SequenceIndex.Remove(JumpTarget);
            reactor.SequenceIndex.Remove(JumpTarget);
        }
    }

    /// <summary>
    ///     Previous-navigation reaches ShowTo through SetDialogIndex, which has no CastableObject
    ///     case at all -- so before the ownership check was hoisted above this path, a castable could
    ///     rewind another castable's session and run under its script.
    /// </summary>
    [Fact]
    public void AnUnassociatedCastable_CannotRewindTheSession()
    {
        var castables = Game.World.WorldState.Values<CastableObject>().Take(2).ToList();
        Assert.True(castables.Count == 2,
            "precondition: the fixture world must load at least two scripted castables");
        var (owner, other) = (castables[0], castables[1]);

        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var sequence = new DialogSequence("DialogSessionBindingRewindProbe") { Id = pursuitId };
        sequence.AddDialog(new OptionsDialog("first"));
        sequence.AddDialog(new OptionsDialog("second"));

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(owner, sequence),
            "couldn't start the probe dialog");
        Assert.True(Fixture.TestUser.DialogState.SetDialogIndex(owner, (int) pursuitId, 1),
            "couldn't advance the probe dialog to index 1");
        Assert.Equal(1, Fixture.TestUser.DialogState.CurrentPursuitIndex);

        try
        {
            // Index 0 is CurrentPursuitIndex - 1: the previous-navigation path.
            var wire = OptionResponse(other.Id, pursuitId, 0);
            wire[0] = 0x05;
            Game.World.WorldPacketHandlers[0x3A].Invoke(Fixture.TestUser, new InboundBody(0x3A, wire));

            Assert.Same(owner, Fixture.TestUser.DialogState.Associate);
            Assert.Equal(1, Fixture.TestUser.DialogState.CurrentPursuitIndex);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
        }
    }

    /// <summary>
    ///     The pursuit id must name the sequence actually active. The owning object is correct here
    ///     and the index is the expected next one; only the pursuit disagrees.
    /// </summary>
    /// <remarks>
    ///     The server acts on its own ActiveDialog regardless of what the packet claims, so the risk
    ///     is not option substitution but replay: a response left over from a finished sequence
    ///     driving whichever one is open now.
    /// </remarks>
    [Fact]
    public void AResponseNamingAnotherPursuit_IsRefused()
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
                new InboundBody(0x3A, OptionResponse(here.Id, pursuitId + 100, indexBefore + 1)));

            Assert.NotEqual(JumpTarget, Fixture.TestUser.DialogState.ActiveDialogSequence?.Name);
            Assert.Equal(indexBefore, Fixture.TestUser.DialogState.CurrentPursuitIndex);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
            here.SequenceIndex.Remove(JumpTarget);
        }
    }
}
