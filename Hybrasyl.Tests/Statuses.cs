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
using Hybrasyl.Subsystems.Scripting;
using Hybrasyl.Xml.Objects;
using System;
using System.Linq;
using System.Threading;
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
        Fixture.ResetTestUserStats();
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
        Assert.NotEmpty(Fixture.TestUser.CurrentStatuses);
        Assert.True(Fixture.TestUser.Stats.Ac == beforeAc + expectedAcDelta * intensity,
            $"ac was {beforeAc}, delta {expectedAcDelta}, should be {beforeAc + expectedAcDelta} but is {Fixture.TestUser.Stats.Ac}");
    }

    [Fact]
    public void ApplyConditionStatus()
    {
        Fixture.ResetTestUserStats();
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

        var invisible = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddInvisible")
            .FirstOrDefault();
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

        Assert.Empty(Fixture.TestUser.CurrentStatuses);
    }

    [Fact]
    public void InvisibilityStatusBreakOnBreakStealth()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Stats.BaseMp = 1000;
        Fixture.TestUser.Stats.Mp = 1000;
        Fixture.TestUser.RemoveAllStatuses();

        var invisible = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddInvisible")
            .FirstOrDefault();
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
        Thread.Sleep(200);
        Assert.Empty(Fixture.TestUser.CurrentStatuses);
    }

    [Fact]
    public void ApplyStatusToUserFromScript()
    {
        var scriptUser = new HybrasylUser(Fixture.TestUser);
        Assert.True(scriptUser.ApplyStatus("TestMinusStr", 30));
        Assert.NotEmpty(Fixture.TestUser.CurrentStatuses.Values.Where(predicate: x => x.Name == "TestMinusStr"));
    }

    [Fact]
    public void ApplyStatusToMonsterFromScript()
    {
        var monster = new Monster(Game.World.WorldData.Get<Creature>("Honey Bee"), SpawnFlags.AiDisabled, 99)
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
        var scriptMonster = new HybrasylMonster(monster);
        Assert.True(scriptMonster.ApplyStatus("TestMinusStr", 30));
        Assert.NotEmpty(monster.CurrentStatuses.Values.Where(predicate: x => x.Name == "TestMinusStr"));
    }

    [Fact]
    public void ApplyAndRemoveStatus()
    {
        Fixture.ResetTestUserStats();
        var beforeAc = Fixture.TestUser.Stats.Ac;
        var testadd1 = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddCurse1").FirstOrDefault();
        var testadd2 = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddCurse2").FirstOrDefault();
        var testremove1 = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestRemCurse1")
            .FirstOrDefault();
        var testremove2 = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestRemCurse2")
            .FirstOrDefault();

        Assert.NotNull(testadd1);
        Assert.NotNull(testadd2);
        Assert.NotNull(testremove1);
        Assert.NotNull(testremove2);

        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");

        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
        // Should spawn, and not have a null behaviorset
        Assert.NotNull(monster.BehaviorSet);
        monster.X = 11;
        monster.Y = 11;
        Fixture.TestUser.Teleport("XUnit Test Realm", 10, 10);
        Fixture.TestUser.Map.InsertCreature(monster);

        Assert.True(Fixture.TestUser.SpellBook.Add(testadd1));
        Assert.True(Fixture.TestUser.SpellBook.Add(testadd2));
        Assert.True(Fixture.TestUser.SpellBook.Add(testremove1));
        Assert.True(Fixture.TestUser.SpellBook.Add(testremove1));

        Assert.True(Fixture.TestUser.UseCastable(testadd1, monster));

        // Gabbaghoul should have test curse 1 applied
        Assert.True(monster.CurrentStatuses.Count == 1);
        Assert.True(monster.CurrentStatuses.Values.Count(predicate: x => x.Name == "TestCurse1") > 0);

        // Remove 2 should not remove 1
        Assert.True(Fixture.TestUser.UseCastable(testremove2, monster));
        Assert.True(monster.CurrentStatuses.Values.Count(predicate: x => x.Name == "TestCurse1") > 0);
        Assert.True(monster.CurrentStatuses.Count == 1);

        // Remove 1 should remove 1
        Assert.True(Fixture.TestUser.UseCastable(testremove1, monster));
        Assert.True(monster.CurrentStatuses.Values.Count(predicate: x => x.Name == "TestCurse1") == 0);
        Assert.True(monster.CurrentStatuses.Count == 0);
    }

    [Fact]
    public void ApplyAndRemoveMultipleStatuses()
    {
        Fixture.ResetTestUserStats();
        var beforeAc = Fixture.TestUser.Stats.Ac;
        var testadd1 = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddCurse1").FirstOrDefault();
        var testadd2 = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddCurse2").FirstOrDefault();
        var testremove1 = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestRem2Curse")
            .FirstOrDefault();

        Assert.NotNull(testadd1);
        Assert.NotNull(testadd2);
        Assert.NotNull(testremove1);

        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");

        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
        // Should spawn, and not have a null behaviorset
        Assert.NotNull(monster.BehaviorSet);
        monster.X = 11;
        monster.Y = 11;
        Fixture.TestUser.Teleport("XUnit Test Realm", 10, 10);
        Fixture.TestUser.Map.InsertCreature(monster);

        Assert.True(Fixture.TestUser.SpellBook.Add(testadd1));
        Assert.True(Fixture.TestUser.SpellBook.Add(testadd2));
        Assert.True(Fixture.TestUser.SpellBook.Add(testremove1));
        Assert.True(Fixture.TestUser.SpellBook.Add(testremove1));

        // Gabbaghoul should have test curse 1 applied
        Assert.True(Fixture.TestUser.UseCastable(testadd1, monster));
        Assert.True(monster.CurrentStatuses.Values.Count(predicate: x => x.Name == "TestCurse1") > 0);
        Assert.True(monster.CurrentStatuses.Count == 1);

        // Gabbaghoul should have test curse 2 applied
        Assert.True(Fixture.TestUser.UseCastable(testadd2, monster));
        Assert.True(monster.CurrentStatuses.Values.Count(predicate: x => x.Name == "TestCurse2") > 0);
        Assert.True(monster.CurrentStatuses.Count == 2);

        // Gabbaghoul should have both curses removed by Test Remove 2
        Assert.True(Fixture.TestUser.UseCastable(testremove1, monster));
        Assert.True(monster.CurrentStatuses.Values.Count(predicate: x => x.Name == "TestCurse1") == 0);
        Assert.True(monster.CurrentStatuses.Values.Count(predicate: x => x.Name == "TestCurse2") == 0);
        Assert.True(monster.CurrentStatuses.Count == 0);
    }
}