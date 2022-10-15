using Hybrasyl;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using Xunit;
using Creature = Hybrasyl.Xml.Creature;

namespace HybrasylTests;

[Collection("Hybrasyl")]
public class Monsters
{
    public Monsters(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    public HybrasylFixture Fixture { get; set; }

    [Fact]
    public void MonsterLearnCastables()
    {
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
        Assert.NotNull(monster.BehaviorSet);
        Game.World.Insert(monster);
        var assails = Game.World.WorldData.FindCastables(condition: x => x.IsAssail);

        foreach (var skill in assails)
            Assert.True(monster.CastableController.ContainsCastable(skill.Name),
                $"Skills: Should know {skill.Name} but doesn't");

        foreach (var spellCategory in monster.BehaviorSet.LearnSpellCategories)
        foreach (var spell in
                 Game.World.WorldData.FindCastables(condition: x => x.CategoryList.Contains(spellCategory)))
            Assert.True(monster.CastableController.ContainsCastable(spell.Name),
                $"Spells: Should know {spell.Name} but doesn't");
    }

    [Fact]
    public void MonsterSpellRotation()
    {
        // We need our gabbaghoul
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
        // Should spawn, and not have a null behaviorset
        Assert.NotNull(monster.BehaviorSet);
        Game.World.Insert(monster);
        // Start with assail rotation
        var entry = monster.CastableController.GetNextCastable(RotationType.Assail);
        Assert.NotNull(entry);
        Assert.True(entry.Parent.Interval == 6);
        entry.Use();
        // Get another rotation, monster was just spawned, shouldn't be able to cast yet
        entry = monster.CastableController.GetNextCastable();
        Assert.Null(entry);
        // Move the timer and try again, should work. 
        // Now it should be in order until the rotation is completed
        monster.ActiveSince = monster.ActiveSince.AddSeconds(-60);
        entry = monster.CastableController.GetNextCastable();
        Assert.NotNull(entry);
        var rot = entry.Parent;
        Assert.Contains(rot,
            filter: x => x.Name == "Wind Blade" && x.CurrentPriority == CreatureTargetPriority.HighThreat);
        Assert.Contains(rot,
            filter: x => x.Name == "puinsein" && x.CurrentPriority == CreatureTargetPriority.AttackingCaster);
        Assert.Contains(rot,
            filter: x => x.Name == "Paralyze" && x.CurrentPriority == CreatureTargetPriority.HighThreat);
        foreach (var castable in
                 Game.World.WorldData.FindCastables(condition: x => x.CategoryList.Contains("ElementST")))
            Assert.Contains(rot, filter: x => x.Name == castable.Name && x.CurrentPriority == rot.TargetPriority);

        entry = monster.CastableController.GetNextCastable();
    }

    [Fact]
    public void MonsterAssailRotationContents()
    {
        // We need our gabbaghoul
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
        // Should spawn, and not have a null behaviorset
        Assert.NotNull(monster.BehaviorSet);
        Game.World.Insert(monster);
        var rot = monster.CastableController.GetAssailRotation();
        Assert.NotNull(rot);
        Assert.True(rot.Count == 3);
        Assert.Contains(rot, filter: x => x.Name == "Assail" && x.CurrentPriority == CreatureTargetPriority.Attacker);
        Assert.Contains(rot, filter: x => x.Name == "Assault" && x.CurrentPriority == CreatureTargetPriority.Attacker);
        Assert.Contains(rot, filter: x => x.Name == "Clobber" && x.CurrentPriority == CreatureTargetPriority.Attacker);
    }

    [Fact]
    public void MonsterAssailRotation()
    {
        // We need our gabbaghoul
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
        // Should spawn, and not have a null behaviorset
        Assert.NotNull(monster.BehaviorSet);
        Game.World.Insert(monster);
        var rot = monster.CastableController.GetAssailRotation();
        var entry2 = monster.CastableController.GetNextAssail();
        Assert.NotNull(entry2);
        // Rotations should now proceed normally
        Assert.Equal("Assail", entry2.Name);
        entry2.Use();
        entry2 = monster.CastableController.GetNextAssail();
        // Next one should immediately be null until interval is reached
        Assert.Null(entry2);
        // Move the timer and try again, should work. 
        // Now it should be in order until the rotation is completed
        rot.LastUse = rot.LastUse.AddSeconds(-30);
        entry2 = monster.CastableController.GetNextAssail();
        Assert.NotNull(entry2);
        Assert.Equal("Assault", entry2.Name);
        entry2.Use();
        rot.LastUse = rot.LastUse.AddSeconds(-30);
        entry2 = monster.CastableController.GetNextAssail();
        Assert.NotNull(entry2);
        Assert.Equal("Clobber", entry2.Name);
        entry2.Use();
        // Ensure the rotation starts over from the beginning
        rot.LastUse = rot.LastUse.AddSeconds(-30);
        entry2 = monster.CastableController.GetNextAssail();
        Assert.NotNull(entry2);
        Assert.Equal("Assail", entry2.Name);
    }

    [Fact]
    public void MonsterThresholdRotations()
    {
        // We need our gabbaghoul
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
        // Should spawn, and not have a null behaviorset
        Assert.NotNull(monster.BehaviorSet);
        Game.World.Insert(monster);
        var rot = monster.CastableController.GetAssailRotation();
        var entry2 = monster.CastableController.GetNextAssail();
        Assert.NotNull(entry2);
        // Rotations should now proceed normally
        Assert.Equal("Assail", entry2.Name);
        entry2.Use();
        var maxHp = monster.Stats.MaximumHp;
        monster.Damage(monster.Stats.MaximumHp - 50);
        Assert.True(monster.Stats.Hp == 50, $"hp should be 50 but is {monster.Stats.Hp}");
        entry2 = monster.CastableController.GetNextCastable();
        // This should be a threshold cast, but can be any of three spells at random
        Assert.NotNull(entry2);
        Assert.True(entry2.Name == "mor athar gar" || entry2.Name == "mor athar meall" ||
                    entry2.Name == "mor athar lamh");
        Assert.True(entry2.CurrentPriority == CreatureTargetPriority.AttackingHealer);
    }
}