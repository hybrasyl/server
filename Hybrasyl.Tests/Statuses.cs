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
using Hybrasyl.Subsystems.Statuses;
using Hybrasyl.Xml.Objects;
using System;
using System.Linq;
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
        Fixture.TestUser.Map.InsertMonster(monster);
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
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Stats.BaseMp = 1000;
        Fixture.TestUser.Stats.Mp = 1000;
        // Wait for any in-flight control messages from prior tests to settle
        TestHelpers.WaitFor(() => Fixture.TestUser.CurrentStatuses.Count == 0, 2000);

        var invisible = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddInvisible")
            .FirstOrDefault();
        var assail = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "Assail").FirstOrDefault();

        Assert.NotNull(invisible);
        Assert.NotNull(assail);
        Assert.Empty(Fixture.TestUser.CurrentStatuses);
        // Apply invisibility
        Assert.True(Fixture.TestUser.UseCastable(invisible, Fixture.TestUser));
        // Should be invisible
        Assert.True(Fixture.TestUser.Condition.IsInvisible);
        // Using assail breaks invisibility
        Assert.True(Fixture.TestUser.UseCastable(assail));
        Assert.True(TestHelpers.WaitFor(() => !Fixture.TestUser.Condition.IsInvisible),
            "Invisibility was not broken by assail within timeout");
        if (!TestHelpers.WaitFor(() => Fixture.TestUser.CurrentStatuses.Count == 0))
        {
            var remaining = string.Join(", ",
                Fixture.TestUser.CurrentStatuses.Values.Select(s => $"{s.Name} (icon={s.Icon})"));
            Assert.Fail($"Statuses not cleared within timeout. Remaining: [{remaining}]");
        }
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
        var castable = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "beag ioc").FirstOrDefault();

        Assert.NotNull(invisible);
        Assert.NotNull(assail);
        Assert.NotNull(castable);
        // Apply invisibility
        Assert.True(Fixture.TestUser.UseCastable(invisible, Fixture.TestUser));
        // Should be invisible
        Assert.True(Fixture.TestUser.Condition.IsInvisible);
        // Using a spell with BreakStealth set, breaks stealth
        Assert.True(Fixture.TestUser.UseCastable(castable, Fixture.TestUser), $"Casting {castable.Name} has utterly failed: {Fixture.TestUser.LastSystemMessage}");
        Assert.True(TestHelpers.WaitFor(() => !Fixture.TestUser.Condition.IsInvisible),
            "Invisibility was not broken by BreakStealth castable within timeout");
        Assert.True(TestHelpers.WaitFor(() => Fixture.TestUser.CurrentStatuses.Count == 0),
            "Statuses were not cleared after stealth break within timeout");
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
        Fixture.TestUser.Map.InsertMonster(monster);

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
        Fixture.TestUser.Map.InsertMonster(monster);

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

    [Fact]
    public void RemoveStatusWithRemovalChance()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();

        // Bump the remover's level so TestScalingCurse.RemoveChance formula
        // (min(0.97, 0.35 + (SOURCELEVEL/(TARGETLEVEL+10))/1.5)) caps at 0.97.
        // The remaining ~3% flake rate is acknowledged in the loose-ends plan.
        Fixture.TestUser.Stats.Level = 99;
        Fixture.TestUser.SetCookie("combatlog", "on");
        Fixture.SecondTestUser.SetCookie("combatlog", "on");

        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");

        Assert.True(Game.World.WorldData.TryGetValueByIndex<Castable>("TestAddScaleCurse", out var scaleCurse),
            "Castable TestAddScaleCurse not found");
        Assert.True(Game.World.WorldData.TryGetValueByIndex<Castable>("TestRemScaleCurse", out var remScaleCurse),
            "Castable TestRemScaleCurse not found");

        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
        // Should spawn, and not have a null behaviorset
        Assert.NotNull(monster.BehaviorSet);
        monster.X = 11;
        monster.Y = 11;

        Fixture.TestUser.Teleport("XUnit Test Realm", 10, 10);
        Fixture.SecondTestUser.Teleport("XUnit Test Realm", 9, 9);
        
        Fixture.TestUser.Map.InsertMonster(monster);

        monster.UseCastable(scaleCurse, Fixture.SecondTestUser);
        // Second user now has curse applied
        Assert.True(Fixture.SecondTestUser.CurrentStatuses.Values.Count(predicate: x => x.Name == "TestScalingCurse") > 0);

        // Attempt to remove the curse
        Fixture.TestUser.UseCastable(remScaleCurse, Fixture.SecondTestUser);

        Assert.True(Fixture.SecondTestUser.CurrentStatuses.Values.Count(predicate: x => x.Name == "TestScalingCurse") ==
                    0, "Curse was not removed");

        Assert.True(Fixture.SecondTestUser.CombatEvents.TryPop(out var result));
        Assert.True(result is StatusEvent);
        var statusEvent = (StatusEvent)result;
        Assert.True(statusEvent.StatusName == "TestScalingCurse", "Status name does not match");


    }

    [Fact]
    public void RemoveStatusWithRemovalChanceFails()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        Fixture.TestUser.RemoveAllStatuses();

        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        Assert.True(Game.World.WorldData.TryGetValue<Hybrasyl.Xml.Objects.Status>("TestFailCurse", out var failXml),
            "Status TestFailCurse not found");

        Fixture.TestUser.Teleport("XUnit Test Realm", 10, 10);
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99) { X = 11, Y = 11 };
        Fixture.TestUser.Map.InsertMonster(monster);

        // Apply the curse with the monster as source so OriginSnapshotId is populated
        // and the RemoveChance roll actually evaluates.
        var status = new CreatureStatus(failXml, Fixture.TestUser, null, monster);
        Fixture.TestUser.ApplyStatus(status);
        Assert.Contains(Fixture.TestUser.CurrentStatuses.Values, s => s.Name == "TestFailCurse");

        // RemoveChance="0" means chance >= 0 is always true, so removal should always fail.
        var removed = Fixture.TestUser.RemoveStatus(status.Icon, true, Fixture.SecondTestUser);
        Assert.False(removed, "Removal should have failed but succeeded");
        Assert.Contains(Fixture.TestUser.CurrentStatuses.Values, s => s.Name == "TestFailCurse");
        Assert.Equal("You try your best, but nothing happens.", Fixture.SecondTestUser.LastSystemMessage);

        // Combat-log entry should reflect the failed roll
        Assert.True(Fixture.SecondTestUser.CombatEvents.TryPop(out var ev));
        var statusEvent = Assert.IsType<StatusEvent>(ev);
        Assert.Equal("TestFailCurse", statusEvent.StatusName);
        Assert.True(statusEvent.RemovalRoll >= statusEvent.RequiredRoll,
            "Failed removal should have RemovalRoll >= RequiredRoll");
    }

    [Fact]
    public void StatusExpiryIsSilent()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.RemoveAllStatuses();

        var invisible = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestAddInvisible")
            .FirstOrDefault();
        Assert.NotNull(invisible);

        Assert.True(Fixture.TestUser.UseCastable(invisible, Fixture.TestUser));
        Assert.True(Fixture.TestUser.Condition.IsInvisible);

        // Sentinel set before natural expiry so we can detect whether the
        // success message ("You succeed in removing the affliction.") fires.
        const string sentinel = "__expiry-sentinel__";
        Fixture.TestUser.SendSystemMessage(sentinel);

        // TestAddInvisible has Duration=1 — wait for it to expire, then drive
        // the tick manually since the StatusTickJob isn't scheduled in tests.
        Assert.True(TestHelpers.WaitFor(() =>
                Fixture.TestUser.CurrentStatuses.Values.All(s => s.Expired)),
            "Status did not reach Expired state within timeout");
        Fixture.TestUser.ProcessStatusTicks();

        // OnExpire should clear the Invisible condition, but no removal-success
        // system message should be emitted (remover is null on natural expiry).
        Assert.Equal(0, Fixture.TestUser.CurrentStatuses.Count);
        Assert.False(Fixture.TestUser.Condition.IsInvisible);
        Assert.NotEqual("You succeed in removing the affliction.", Fixture.TestUser.LastSystemMessage);
    }
}