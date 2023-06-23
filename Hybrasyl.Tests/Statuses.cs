using System;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Tests;

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
        Fixture.ResetUserStats();
        Fixture.TestUser.Stats.BaseAc = 50;
        var beforeAc = Fixture.TestUser.Stats.Ac;
        var castable = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestPlusAc").FirstOrDefault();
        Assert.NotNull(castable);
        Assert.NotNull(castable.AddStatuses);
        Assert.NotEmpty(castable.AddStatuses);
        var expectedStatus = Game.World.WorldData.Get<Xml.Objects.Status>(castable.AddStatuses.First().Value);
        Assert.NotNull(expectedStatus.Effects.OnApply.StatModifiers);
        var expectedAcDelta = Convert.ToSByte(expectedStatus.Effects.OnApply.StatModifiers.BonusAc);
        var intensity = castable.AddStatuses.First().Intensity;
        Fixture.TestUser.SpellBook.Add(castable);
        Assert.True(Fixture.TestUser.UseCastable(castable, Fixture.TestUser));
        Assert.NotEmpty(Fixture.TestUser.CurrentStatusInfo);
        Assert.True(Fixture.TestUser.Stats.Ac == beforeAc + (expectedAcDelta * intensity),
            $"ac was {beforeAc}, delta {expectedAcDelta}, should be {beforeAc + expectedAcDelta} but is {Fixture.TestUser.Stats.Ac}");
    }

    [Fact]
    public void ApplyConditionStatus()
    {
        Fixture.ResetUserStats();
        var beforeAc = Fixture.TestUser.Stats.Ac;
        var castable = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddSleep").FirstOrDefault();
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

    [Fact]
    public void InvisibilityStatusBreakOnAssail()
    {
        Fixture.TestUser.Stats.BaseMp = 1000;
        Fixture.TestUser.Stats.Mp = 1000;
        Fixture.TestUser.RemoveAllStatuses();

        var invisible = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddInvisible").FirstOrDefault();
        var assail = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "Assail").FirstOrDefault();

        Assert.NotNull(invisible);
        Assert.NotNull(assail);
        // Apply invisibility
        Assert.True(Fixture.TestUser.UseCastable(invisible, Fixture.TestUser));
        // Should be invisible
        Assert.True(Fixture.TestUser.Condition.IsInvisible);
        // Using assail breaks invisibility
        Assert.True(Fixture.TestUser.UseCastable(assail));
        Assert.False(Fixture.TestUser.Condition.IsInvisible);
        // Allow sufficient time for control message handler to process messages
        Thread.Sleep(50); 

        Assert.Empty(Fixture.TestUser.CurrentStatusInfo);
    }

    [Fact]
    public void InvisibilityStatusBreakOnBreakStealth()
    {
        Fixture.TestUser.Stats.BaseMp = 1000;
        Fixture.TestUser.Stats.Mp = 1000;
        Fixture.TestUser.RemoveAllStatuses();

        var invisible = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddInvisible").FirstOrDefault();
        var assail = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "Assail").FirstOrDefault();
        var castable = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "beag athar").FirstOrDefault();

        Assert.NotNull(invisible);
        Assert.NotNull(assail);
        Assert.NotNull(castable);
        // Apply invisibility
        Assert.True(Fixture.TestUser.UseCastable(invisible, Fixture.TestUser));
        // Should be invisible
        Assert.True(Fixture.TestUser.Condition.IsInvisible);
        // Using a spell with BreakStealth set, breaks stealth
        Fixture.TestUser.UseCastable(castable);
        Assert.False(Fixture.TestUser.Condition.IsInvisible);
        // Allow sufficient time for control message handler to process messages
        Thread.Sleep(50); 
        Assert.Empty(Fixture.TestUser.CurrentStatusInfo);
    }

}