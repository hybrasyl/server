using System.Linq;
using Hybrasyl;
using Xunit;

namespace HybrasylTests;


[Collection("Hybrasyl")]
public class Status
{
    public HybrasylFixture Fixture { get; set; }

    public Status(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void ApplyStatus()
    {
        // Apply a status, verify that status exists
        var castable = Game.World.WorldData.FindCastables(x => x.Name == "Plus AC").FirstOrDefault();
        Fixture.TestUser.Stats.BaseMp = 1000;
        Fixture.TestUser.Stats.Mp = 1000;

        Assert.NotNull(castable);
        Fixture.TestUser.SpellBook.Add(castable);
        var beforeAc = Fixture.TestUser.Stats.Ac;
        Fixture.TestUser.UseCastable(castable, Fixture.TestUser);
        Assert.True(Fixture.TestUser.Stats.Ac == beforeAc + 20, $"ac should be {beforeAc+20} but is {Fixture.TestUser.Stats.Ac}");
    }

    [Fact]
    public void ApplyMuteStatus()
    {
        // Apply mute status, verify that user/creature cannot cast
    }
}