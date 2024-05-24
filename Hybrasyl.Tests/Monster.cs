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

using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using System;
using System.Linq;
using System.Threading;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Tests;

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

        foreach (var skillCategory in monster.BehaviorSet.LearnSkillCategories)
            foreach (var skill in
                     Game.World.WorldData.Find<Castable>(condition: x => x.CategoryList.Contains(skillCategory)))
            {
                var reqs = skill.Requirements.Where(predicate: x => x.Physical != null);
                foreach (var req in reqs)
                    if (monster.MeetsRequirement(req))
                        Assert.True(monster.CastableController.ContainsCastable(skill.Name),
                            $"Skills: Should know {skill.Name} but doesn't");
            }

        foreach (var spellCategory in monster.BehaviorSet.LearnSpellCategories)
            foreach (var spell in
                     Game.World.WorldData.Find<Castable>(condition: x => x.CategoryList.Contains(spellCategory)))
            {
                var reqs = spell.Requirements.Where(predicate: x => x.Physical != null);
                foreach (var req in reqs)
                    if (monster.MeetsRequirement(req))
                        Assert.True(monster.CastableController.ContainsCastable(spell.Name),
                            $"Skills: Should know {spell.Name} but doesn't");
            }
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
            filter: x => x.Name == "Wraith Touch" && x.CurrentPriority == CreatureTargetPriority.HighThreat);
        foreach (var castable in
                 Game.World.WorldData.Find<Castable>(condition: x => x.CategoryList.Contains("ElementST")))
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
        Game.World.Insert(monster);
        Assert.NotNull(monster.BehaviorSet);
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
        monster.ActiveSince = DateTime.Now.Subtract(TimeSpan.FromMinutes(5));
        var rot = monster.CastableController.GetAssailRotation();
        var entry2 = monster.CastableController.GetNextAssail();
        Assert.NotNull(entry2);
        // Rotations should now proceed normally
        Assert.Equal("Assail", entry2.Name);
        entry2.Use();
        var maxHp = monster.Stats.MaximumHp;
        monster.Damage(monster.Stats.MaximumHp - 50);
        Assert.True(monster.Stats.Hp == 50, $"hp should be 50 but is {monster.Stats.Hp}");
        Assert.True(monster.ActiveSeconds > 300);
        entry2 = monster.CastableController.GetNextCastable();
        // This should be a threshold cast, but can be any of three spells at random
        Assert.NotNull(entry2);
        Assert.True(entry2.Name == "athar gar" || entry2.Name == "athar meall" ||
                    entry2.Name == "athar lamh");
        Assert.True(entry2.CurrentPriority == CreatureTargetPriority.AttackingHealer);
    }

    [Fact]
    public void MonsterCastableImmunities()
    {
        Fixture.TestUser.LastHeard = null;
        Fixture.TestUser.Stats.BaseMp = 10000;
        Fixture.TestUser.Stats.Mp = 10000;

        var behaviorSet = Game.World.WorldData.Get<CreatureBehaviorSet>("CastableImmune");
        Assert.NotNull(behaviorSet);

        var baitTemplate = Game.World.WorldData.Get<Creature>("Honey Bee");
        Assert.NotNull(baitTemplate);

        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 500,
                Hp = 500
            },
            Name = "Bee Bait",
            X = (byte)(Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };

        var castable = Game.World.WorldData.GetByIndex<Castable>("ard srad");
        Assert.NotNull(castable);
        Assert.NotNull(castable);
        Game.World.Insert(bait);
        Fixture.TestUser.Map.Insert(bait, bait.X, bait.Y);
        Assert.Equal(bait.Map, Fixture.TestUser.Map);

        bait.BehaviorSet = behaviorSet;

        Fixture.TestUser.SpellBook.Add(castable);
        Assert.True(Fixture.TestUser.UseCastable(castable, bait));
        var immunityTriggered =
            behaviorSet.Immunities.FirstOrDefault(predicate: x => x.Type == CreatureImmunityType.Castable);

        Assert.NotNull(immunityTriggered);
        Assert.Equal((uint)500, bait.Stats.Hp);
        Assert.Equal(immunityTriggered.Message, Fixture.TestUser.LastHeard.Message);
        if (immunityTriggered.MessageType == MessageType.Shout)
            Assert.True(Fixture.TestUser.LastHeard.Shout);
        else
            Assert.False(Fixture.TestUser.LastHeard.Shout);
    }

    [Fact]
    public void MonsterElementalImmunities()
    {
        Fixture.TestUser.LastHeard = null;
        Fixture.TestUser.Stats.BaseMp = 10000;
        Fixture.TestUser.Stats.Mp = 10000;

        var behaviorSet = Game.World.WorldData.Get<CreatureBehaviorSet>("ElementImmune");
        Assert.NotNull(behaviorSet);

        var baitTemplate = Game.World.WorldData.Get<Creature>("Honey Bee");
        Assert.NotNull(baitTemplate);

        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 500,
                Hp = 500
            },
            Name = "Bee Bait",
            X = (byte)(Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };

        var castable = Game.World.WorldData.GetByIndex<Castable>("ard srad");
        Assert.NotNull(castable);
        Assert.NotNull(castable);
        Game.World.Insert(bait);
        Fixture.TestUser.Map.Insert(bait, bait.X, bait.Y);
        Assert.Equal(bait.Map, Fixture.TestUser.Map);

        bait.BehaviorSet = behaviorSet;

        Fixture.TestUser.SpellBook.Add(castable);
        Assert.True(Fixture.TestUser.UseCastable(castable, bait));
        var immunityTriggered =
            behaviorSet.Immunities.FirstOrDefault(predicate: x => x.Type == CreatureImmunityType.Element);

        Assert.NotNull(immunityTriggered);
        Assert.Equal((uint)500, bait.Stats.Hp);
        Assert.Equal(immunityTriggered.Message, Fixture.TestUser.LastHeard.Message);
        if (immunityTriggered.MessageType == MessageType.Shout)
            Assert.True(Fixture.TestUser.LastHeard.Shout);
        else
            Assert.False(Fixture.TestUser.LastHeard.Shout);
    }

    [Fact]
    public void MonsterCastableCategoryImmunities()
    {
        Fixture.TestUser.LastHeard = null;
        Fixture.TestUser.Stats.BaseMp = 10000;
        Fixture.TestUser.Stats.Mp = 10000;

        var behaviorSet = Game.World.WorldData.Get<CreatureBehaviorSet>("CastCatImmune");
        Assert.NotNull(behaviorSet);

        var baitTemplate = Game.World.WorldData.Get<Creature>("Honey Bee");
        Assert.NotNull(baitTemplate);

        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 500,
                Hp = 500
            },
            Name = "Bee Bait",
            X = (byte)(Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };

        var castable = Game.World.WorldData.GetByIndex<Castable>("TestPlusAc");
        Assert.NotNull(castable);
        Assert.NotNull(castable);
        Game.World.Insert(bait);
        Fixture.TestUser.Map.Insert(bait, bait.X, bait.Y);
        Assert.Equal(bait.Map, Fixture.TestUser.Map);

        bait.BehaviorSet = behaviorSet;

        Fixture.TestUser.SpellBook.Add(castable);
        var beforeAc = bait.Stats.Ac;
        Assert.True(Fixture.TestUser.UseCastable(castable, bait));
        var immunityTriggered =
            behaviorSet.Immunities.FirstOrDefault(predicate: x => x.Type == CreatureImmunityType.CastableCategory);

        Assert.NotNull(immunityTriggered);
        Assert.NotNull(Fixture.TestUser.LastHeard);
        Assert.Equal(beforeAc, bait.Stats.Ac);
        Assert.Equal(immunityTriggered.Message, Fixture.TestUser.LastHeard.Message);

        if (immunityTriggered.MessageType == MessageType.Shout)
            Assert.True(Fixture.TestUser.LastHeard.Shout);
        else
            Assert.False(Fixture.TestUser.LastHeard.Shout);
        Assert.Empty(bait.CurrentStatuses);
    }

    [Fact]
    public void MonsterStatusCategoryImmunities()
    {
        Fixture.TestUser.LastHeard = null;
        Fixture.TestUser.Stats.BaseMp = 10000;
        Fixture.TestUser.Stats.Mp = 10000;

        var behaviorSet = Game.World.WorldData.Get<CreatureBehaviorSet>("StatCatImmune");
        Assert.NotNull(behaviorSet);

        var baitTemplate = Game.World.WorldData.Get<Creature>("Honey Bee");
        Assert.NotNull(baitTemplate);

        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 500,
                Hp = 500,
                BaseStr = 50
            },
            Name = "Bee Bait",
            X = (byte)(Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };

        var castable = Game.World.WorldData.GetByIndex<Castable>("TestMinusStr");
        Assert.NotNull(castable);
        Assert.NotNull(castable);
        Game.World.Insert(bait);
        Fixture.TestUser.Map.Insert(bait, bait.X, bait.Y);
        Assert.Equal(bait.Map, Fixture.TestUser.Map);

        bait.BehaviorSet = behaviorSet;

        Fixture.TestUser.SpellBook.Add(castable);
        Assert.True(Fixture.TestUser.UseCastable(castable, bait));
        Thread.Sleep(1000);

        var immunityTriggered =
            behaviorSet.Immunities.FirstOrDefault(predicate: x => x.Type == CreatureImmunityType.StatusCategory);

        var beforeStr = bait.Stats.Str;
        Assert.NotNull(immunityTriggered);
        Assert.Equal(beforeStr, bait.Stats.Str);
        Assert.Equal(immunityTriggered.Message, Fixture.TestUser.LastHeard.Message);

        if (immunityTriggered.MessageType == MessageType.Shout)
            Assert.True(Fixture.TestUser.LastHeard.Shout);
        else
            Assert.False(Fixture.TestUser.LastHeard.Shout);

        Assert.Empty(bait.CurrentStatuses);
    }

    [Fact]
    public void MonsterWithStaticStats()
    {
        Fixture.TestUser.LastHeard = null;
        Fixture.TestUser.Stats.BaseMp = 10000;
        Fixture.TestUser.Stats.Mp = 10000;

        var behaviorSet = Game.World.WorldData.Get<CreatureBehaviorSet>("RareGabba");
        Assert.NotNull(behaviorSet);

        var baitTemplate = Game.World.WorldData.Get<Creature>("Gabbaghoul");
        Assert.NotNull(baitTemplate);

        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Name = "Gabbaghoul Test"
        };

        Assert.True(bait.Stats.BaseHp > 100000);
        Assert.True(bait.Stats.BaseMp > 100000);
        Assert.True(bait.Stats.BaseStr > 100);
    }

    [Fact]
    public void MonsterWithDynamicStats()
    {
        Fixture.TestUser.LastHeard = null;
        Fixture.TestUser.Stats.BaseMp = 10000;
        Fixture.TestUser.Stats.Mp = 10000;

        var behaviorSet = Game.World.WorldData.Get<CreatureBehaviorSet>("RareGabbaDynamic");
        Assert.NotNull(behaviorSet);

        var baitTemplate = Game.World.WorldData.Get<Creature>("Gabbaghoul");
        Assert.NotNull(baitTemplate);

        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99, null, behaviorSet)
        {
            Name = "Gabbaghoul Test"
        };

        // Gabba now has its base hp from leveling, and its buff defined in the behaviorset
        Assert.True(bait.Stats.BaseHp >= bait.Stats.Str * 200);
        Assert.True(bait.Stats.BaseMp >= bait.Stats.Int * 200);
    }
}