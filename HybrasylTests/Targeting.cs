using System.Linq;
using Hybrasyl;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using Nustache.Core;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace HybrasylTests;

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
                X = (byte) (Fixture.TestUser.X - i),
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

}