using System.Linq;
using Hybrasyl;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using Xunit;
using Creature = Hybrasyl.Xml.Creature;

namespace HybrasylTests;

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
        Fixture.TestUser.Teleport(Fixture.Map.Id, 20, 20);

        var trapTest = Game.World.WorldData.GetByIndex<Castable>("Test Trap");

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
    }

    [Fact]
    public void MapReactorCreation() { }


    [Fact]
    public void CastableReactorUsage()
    {
        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.SpellBook.Clear();
        Fixture.TestUser.Teleport(Fixture.Map.Id, 15, 15);

        var trapTest = Game.World.WorldData.GetByIndex<Castable>("Test Trap");

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
        Assert.Equal((uint) 10000, Fixture.TestUser.Stats.Hp);

        Fixture.Map.InsertCreature(bait);

        Assert.True(Fixture.TestUser.Walk(Direction.North), "Walk failed");
        Assert.True(Fixture.TestUser.Walk(Direction.North), "Walk failed");
        Assert.True(Fixture.TestUser.Walk(Direction.North), "Walk failed");

        Assert.Equal(15, Fixture.TestUser.X);
        Assert.Equal(12, Fixture.TestUser.Y);

        // Bait should be undamaged
        Assert.Equal((uint) 500, bait.Stats.Hp);

        // Walk onto reactor
        Assert.True(bait.Walk(Direction.East), "Walk failed");

        var reactors = Fixture.Map.Reactors[(15, 15)];
        Assert.Single(reactors.Values);
        var reactor = reactors.Values.First();
        Assert.Equal(bait.X, reactor.X);
        Assert.Equal(bait.Y, reactor.Y);

        Assert.Equal((uint) 475, bait.Stats.Hp);
        Assert.True(bait.Walk(Direction.East), "Walk failed");
        Assert.True(bait.Walk(Direction.West), "Walk failed");
        // Reactor is expired so it should not have impacted bait's HP
        Assert.Equal((uint) 475, bait.Stats.Hp);
    }

    [Fact]
    public void MapReactorUsage() { }
}