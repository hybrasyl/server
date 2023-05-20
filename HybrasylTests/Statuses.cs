using System.Linq;
using Hybrasyl;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace HybrasylTests;

[Collection("Hybrasyl")]
public class Status
{
    public Status(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    public HybrasylFixture Fixture { get; set; }

    [Fact]
    public void ApplyStatus()
    {
        // Apply a status, verify that status exists
        Fixture.TestUser.Stats.BaseMp = 1000;
        Fixture.TestUser.Stats.Mp = 1000;
        var beforeAc = Fixture.TestUser.Stats.Ac;
        var castable = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "Plus AC").FirstOrDefault();
        Assert.NotNull(castable);
        Fixture.TestUser.SpellBook.Add(castable);
        Fixture.TestUser.UseCastable(castable, Fixture.TestUser);
        Assert.True(Fixture.TestUser.Stats.Ac == beforeAc - 20,
            $"ac should be {beforeAc - 20} but is {Fixture.TestUser.Stats.Ac}");
    }

    [Fact]
    public void ApplyConditionStatus()
    {
        Fixture.TestUser.Stats.BaseMp = 1000;
        Fixture.TestUser.Stats.Mp = 1000;
        var beforeAc = Fixture.TestUser.Stats.Ac;
        var castable = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "Sleep").FirstOrDefault();
        Assert.NotNull(castable);
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
        // Should spawn, and not have a null behaviorset
        Assert.NotNull(monster.BehaviorSet);
        monster.X = 11;
        monster.Y = 11;
        Fixture.TestUser.Teleport("XUnit Test Realm", 10, 10);
        Fixture.TestUser.Map.InsertCreature(monster);
        Assert.NotNull(castable);
        Fixture.TestUser.SpellBook.Add(castable);
        Fixture.TestUser.UseCastable(castable, monster);
        // Gabbaghoul should be asleep now, and unable to cast
        Assert.True(monster.Condition.Asleep);
        Assert.False(monster.Condition.CastingAllowed);
    }
}