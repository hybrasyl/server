using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using Xunit;

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

        Fixture.Map.Reactors.Clear();
    }

    [Fact]
    public void MapReactorCreation() { }

    [Fact]
    public void CastableReactorUsage()
    {
        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.SpellBook.Clear();
        Fixture.TestUser.Teleport(Fixture.Map.Id, 20, 20);

        var trapTest = Game.World.WorldData.GetByIndex<Castable>("Test Trap");
        
        Assert.NotNull(trapTest);
        Assert.True(Fixture.TestUser.AddSkill(trapTest, 1), "Failed to add castable to skillbook");
        Assert.True(Fixture.TestUser.UseCastable(trapTest), "UseCastable failed");

        var baitTemplate = Game.World.WorldData.Get<Hybrasyl.Xml.Creature>("Honey Bee");
        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99, Fixture.Map.Id)
        {
            Stats =
            {
                BaseHp = 50
            },
            X = (byte) (Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
            
        };
        
        Fixture.Map.InsertCreature(bait);

        Assert.True(Fixture.TestUser.Walk(Direction.North));
        Assert.True(Fixture.TestUser.Walk(Direction.North));
        Assert.True(Fixture.TestUser.Walk(Direction.North));

        Assert.Equal(20, Fixture.TestUser.X);
        Assert.Equal(17, Fixture.TestUser.Y);

        Assert.True(bait.Walk(Direction.East), "Walk failed");

        var reactors = Fixture.Map.Reactors[(Fixture.TestUser.X, Fixture.TestUser.Y)];

        Assert.Single(reactors.Values);

        var reactor = reactors.Values.First();

        Assert.Equal(bait.X, reactor.X);
        Assert.Equal(bait.Y, reactor.Y);

        Assert.Equal((uint) 25, bait.Stats.Hp);
    }

    [Fact]
    public void MapReactorUsage() { }

}
