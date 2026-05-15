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
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Xml.Objects;
using System.Linq;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;
using HybrasylReactor = Hybrasyl.Objects.Reactor;
using XmlReactor = Hybrasyl.Xml.Objects.Reactor;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Reactor
{
    private static HybrasylFixture Fixture;

    public Reactor(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void CastableReactorCreation()
    {
        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.SpellBook.Clear();
        Fixture.TestUser.Stats.Level = 41; // Test trap formula for uses is 2 uses > 40, 1 use otherwise
        Fixture.TestUser.Teleport(Fixture.Map.Id, 20, 20);

        var trapTest = Game.World.WorldData.GetByIndex<Castable>("TestTrapMulti");

        Assert.NotNull(trapTest);
        Assert.True(Fixture.TestUser.AddSkill(trapTest, 1), "Failed to add castable to skillbook");
        Assert.True(Fixture.TestUser.UseCastable(trapTest), "UseCastable failed");
        Assert.True(Fixture.Map.Reactors.Count > 0, "No reactors added to map?");

        var reactors = Fixture.Map.Reactors[(Fixture.TestUser.X, Fixture.TestUser.Y)];

        Assert.Single(reactors.Values);

        var reactor = reactors.Values.First();

        Assert.Equal(Fixture.TestUser.X, reactor.X);
        Assert.Equal(Fixture.TestUser.Y, reactor.Y);
        Assert.Equal(Fixture.TestUser.Guid, reactor.CreatedBy);
        Assert.Equal(2, reactor.Uses);
    }

    [Fact]
    public void CastableReactorUsage()
    {
        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.SpellBook.Clear();
        Fixture.ResetTestUserStats();

        Fixture.TestUser.Teleport(Fixture.Map.Id, 25, 25);
        // Test trap formula for uses is 2 uses > 40, 1 use otherwise
        Fixture.TestUser.Stats.Level = 39;

        var trapTest = Game.World.WorldData.GetByIndex<Castable>("TestTrapSingle");

        Assert.NotNull(trapTest);
        Assert.True(Fixture.TestUser.AddSkill(trapTest, 1), "Failed to add castable to skillbook");
        Assert.True(Fixture.TestUser.UseCastable(trapTest), "UseCastable failed");

        var baitTemplate = Game.World.WorldData.Get<Creature>("Honey Bee");
        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 500,
                Hp = 500
            },
            Name = "Bee Bait",
            X = (byte) (Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };

        // walk off the reactor and then back onto it
        Assert.True(Fixture.TestUser.Walk(Direction.South), "Walk failed");
        Assert.True(Fixture.TestUser.Walk(Direction.North), "Walk failed");

        // caster / other player walking over the reactor should not trigger it.
        // Note that this is done by the *script* itself and not by Hybrasyl, to allow maximum
        // flexibility for reactor event handling / scripting.
        Assert.Equal((uint) 1000, Fixture.TestUser.Stats.Hp);

        Assert.True(Fixture.TestUser.Walk(Direction.North), "Walk failed");
        Assert.True(Fixture.TestUser.Walk(Direction.North), "Walk failed");
        Assert.True(Fixture.TestUser.Walk(Direction.North), "Walk failed");

        Assert.Equal(25, Fixture.TestUser.X);
        Assert.Equal(22, Fixture.TestUser.Y);

        Fixture.Map.InsertMonster(bait);

        // Bait should be undamaged
        Assert.Equal((uint) 500, bait.Stats.Hp);
        var reactors = Fixture.Map.Reactors[(25, 25)];
        Assert.Single(reactors.Values);
        var reactor = reactors.Values.First();

        // Reactor should have one use remaining
        Assert.Equal(1, reactor.Uses);

        // Bait walks onto reactor, triggering it
        Assert.True(bait.Walk(Direction.East), "Walk failed");

        // Reactor is used, should be 0 uses remaining
        Assert.Equal(0, reactor.Uses);
        Assert.Equal(bait.X, reactor.X);
        Assert.Equal(bait.Y, reactor.Y);

        Assert.Equal((uint) 475, bait.Stats.Hp);
        Assert.True(bait.Walk(Direction.East), "Walk failed");
        Assert.True(bait.Walk(Direction.West), "Walk failed");
        // Reactor is expired so it should not have impacted bait's HP
        Assert.Equal((uint) 475, bait.Stats.Hp);
    }

    [Fact]
    public void ReactorCasterSnapshot()
    {
        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.SpellBook.Clear();
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport(Fixture.Map.Id, 15, 15);
        // Test trap formula for uses is 2 uses > 40, 1 use otherwise
        Fixture.TestUser.Stats.Level = 39;

        var trapTest = Game.World.WorldData.GetByIndex<Castable>("TestTrapSingle");
    }

    // --- Reactor.OnSpawn idempotency tests ---
    //
    // Regression for the QRM20_* "Dialog sequence X is being overwritten" startup warnings:
    // Reactor.OnSpawn used to call DialogSequences.Clear() before re-running the script's OnSpawn
    // function, but left SequenceIndex populated. RegisterDialogSequence checks SequenceIndex
    // for duplicates, so every re-registration after a second OnSpawn (caused by InsertReactor
    // explicitly calling OnSpawn even though World.Insert had already fired it via the ISpawnable
    // path, and by ScriptProcessor.ReloadScript on script reload) tripped a warning per sequence.
    // Fix uses IPursuitable.ResetPursuits, which empties all three collections together.

    private static HybrasylReactor CreateBareReactor(byte x = 5, byte y = 5) =>
        new(new XmlReactor { X = x, Y = y, DisplayName = "TestReactor" }, Fixture.Map);

    [Fact]
    public void ResetPursuits_ClearsAllSequenceCollections()
    {
        var reactor = CreateBareReactor();
        var pursuitable = (IPursuitable)reactor;

        ((IInteractable)reactor).RegisterDialogSequence(new DialogSequence("seq_a"));
        pursuitable.AddPursuit(new DialogSequence("pursuit_a"));

        Assert.NotEmpty(reactor.SequenceIndex);
        Assert.NotEmpty(reactor.DialogSequences);
        Assert.NotEmpty(reactor.Pursuits);

        pursuitable.ResetPursuits();

        Assert.Empty(reactor.SequenceIndex);
        Assert.Empty(reactor.DialogSequences);
        Assert.Empty(reactor.Pursuits);
    }

    [Fact]
    public void DialogSequencesClear_AloneLeavesSequenceIndexStale()
    {
        // Demonstrates the pre-fix bug: clearing only DialogSequences leaves SequenceIndex
        // populated. Future maintainers reading this test should see why ResetPursuits is the
        // correct call, not DialogSequences.Clear().
        var reactor = CreateBareReactor();
        ((IInteractable)reactor).RegisterDialogSequence(new DialogSequence("seq_a"));

        reactor.DialogSequences.Clear();

        Assert.Empty(reactor.DialogSequences);
        Assert.NotEmpty(reactor.SequenceIndex);
    }

    [Fact]
    public void ReRegisterAfterResetPursuits_LeavesNoDuplicates()
    {
        var reactor = CreateBareReactor();
        var interactable = (IInteractable)reactor;

        interactable.RegisterDialogSequence(new DialogSequence("seq_a"));
        interactable.RegisterDialogSequence(new DialogSequence("seq_b"));
        Assert.Equal(2, reactor.SequenceIndex.Count);
        Assert.Equal(2, reactor.DialogSequences.Count);

        ((IPursuitable)reactor).ResetPursuits();

        var freshA = new DialogSequence("seq_a");
        var freshB = new DialogSequence("seq_b");
        interactable.RegisterDialogSequence(freshA);
        interactable.RegisterDialogSequence(freshB);

        Assert.Equal(2, reactor.SequenceIndex.Count);
        Assert.Equal(2, reactor.DialogSequences.Count);
        Assert.Same(freshA, reactor.SequenceIndex["seq_a"]);
        Assert.Same(freshB, reactor.SequenceIndex["seq_b"]);
    }
}