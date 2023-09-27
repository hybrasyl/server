using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using System.Linq;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Targeting
{
    public Targeting(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    public HybrasylFixture Fixture { get; set; }


    [Fact]
    public void ClosestTargetFirst()
    {
        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.SpellBook.Clear();
        Fixture.TestUser.Stats.Level = 41; // Test trap formula for uses is 2 uses > 40, 1 use otherwise
        Fixture.Map.Clear();

        Fixture.TestUser.Teleport(Fixture.Map.Id, 20, 20);

        var baitTemplate = Game.World.WorldData.Get<Creature>("Honey Bee");

        for (var i = 1; i < 10; i++)
        {
            var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
            {
                Stats =
                {
                    BaseHp = 500,
                    Hp = 500
                },
                Name = "Bee Bait",
                X = (byte)(Fixture.TestUser.X - i),
                Y = Fixture.TestUser.Y
            };
            Fixture.Map.InsertCreature(bait);
        }

        var targets = Fixture.TestUser.GetDirectionalTargets(Direction.West);
        Assert.NotEmpty(targets);

        var radiusTargets = Fixture.TestUser.GetDirectionalTargets(Direction.West, 5);
        Assert.NotEmpty(radiusTargets);
        Assert.Equal(5, radiusTargets.Count);
        var firstTarget = radiusTargets.First();
        var lastTarget = radiusTargets.Last();
        Assert.Equal(Fixture.TestUser.X - 1, firstTarget.X);
        Assert.Equal(Fixture.TestUser.Y, firstTarget.Y);
        Assert.Equal(Fixture.TestUser.X - 5, lastTarget.X);
        Assert.Equal(Fixture.TestUser.Y, lastTarget.Y);
    }

    [Fact]
    public void NoDuplicateTargets()
    {

        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.SpellBook.Clear();
        Fixture.TestUser.Teleport(Fixture.Map.Id, 20, 20);
        Fixture.Map.Clear();
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
        var bait2 = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 500,
                Hp = 500
            },
            Name = "Bee Bait",
            X = (byte) (bait.X - 1),
            Y = bait.Y
        };

        Fixture.Map.InsertCreature(bait);
        Fixture.Map.InsertCreature(bait2);

        var castable = Game.World.WorldData.GetByIndex<Castable>("athar meall");
        Assert.NotNull(castable);

        var targets = Fixture.TestUser.GetTargets(castable, bait);
        Assert.Equal(2, targets.Count);
        Fixture.Map.Clear();
        Fixture.Map.InsertCreature(bait2);
        var targets2 = Fixture.TestUser.GetTargets(castable, bait2);
        Assert.Single(targets2);

    }

}