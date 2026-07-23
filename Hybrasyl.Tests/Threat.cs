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
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using System;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Threat(HybrasylFixture fixture)
{
    public HybrasylFixture Fixture { get; set; } = fixture;

    // Fixture users are constructed directly (not World.Insert-ed), so they are not in
    // the world object guid index that HighestThreat / GetTargets resolve through.
    private void RegisterUsers()
    {
        Game.World.WorldState.SetWorldObject(Fixture.TestUser.Guid, Fixture.TestUser);
        Game.World.WorldState.SetWorldObject(Fixture.SecondTestUser.Guid, Fixture.SecondTestUser);
    }

    [Fact]
    public void HighestThreatIsActuallyHighest()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        RegisterUsers();

        var threatInfo = new ThreatInfo(Guid.NewGuid());
        threatInfo.AddNewThreat(Fixture.TestUser, 1);
        threatInfo.AddNewThreat(Fixture.SecondTestUser, 50);

        Assert.Equal(Fixture.SecondTestUser.Guid, threatInfo.HighestThreat?.Guid);
        Assert.Equal(50u, threatInfo.HighestThreatEntry.Threat);
    }

    // Threat is the SortedDictionary comparison key; mutating it in place used to leave
    // the ordering index stale (wrong HighestThreat) and break removal (tree lookup
    // follows the new value through a tree built from the old one).
    [Fact]
    public void ThreatMutationReordersIndex()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        RegisterUsers();

        var threatInfo = new ThreatInfo(Guid.NewGuid());
        threatInfo.AddNewThreat(Fixture.TestUser, 1);
        threatInfo.AddNewThreat(Fixture.SecondTestUser, 50);
        threatInfo.IncreaseThreat(Fixture.TestUser, 100); // 1 -> 101, now highest

        Assert.Equal(Fixture.TestUser.Guid, threatInfo.HighestThreat?.Guid);
        Assert.Equal(101u, threatInfo.HighestThreatEntry.Threat);
    }

    [Fact]
    public void ThreatRemovalAfterMutationWorks()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        RegisterUsers();

        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99);

        // Three entries so the mutated node is not the tree root: a stale key at the
        // root is still found by accident, masking the corruption.
        var threatInfo = new ThreatInfo(Guid.NewGuid());
        threatInfo.AddNewThreat(Fixture.TestUser, 1);
        threatInfo.AddNewThreat(Fixture.SecondTestUser, 50);
        threatInfo.AddNewThreat(monster, 100);
        threatInfo.IncreaseThreat(Fixture.TestUser, 200); // 1 -> 201, re-sorts past both

        threatInfo.RemoveThreat(Fixture.TestUser);

        Assert.False(threatInfo.ContainsThreat(Fixture.TestUser));
        Assert.Equal(2, threatInfo.ThreatTableByThreat.Count);
    }

    [Fact]
    public void IncreaseThreatOnNewThreatCountsOnce()
    {
        Fixture.ResetTestUserStats();
        RegisterUsers();

        var threatInfo = new ThreatInfo(Guid.NewGuid());
        threatInfo.IncreaseThreat(Fixture.TestUser, 10);

        Assert.Equal(10u, threatInfo[Fixture.TestUser]);
    }

    [Fact]
    public void TargetPriorityRespectsThreatOrder()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        RegisterUsers();

        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        // Far corner: keep the fixture users out of the viewport so AoiEntry doesn't
        // add threat entries of its own before the explicit ones below.
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99) { X = 45, Y = 45 };
        Fixture.Map.InsertMonster(monster);

        monster.ThreatInfo.AddNewThreat(Fixture.TestUser, 1);
        monster.ThreatInfo.AddNewThreat(Fixture.SecondTestUser, 50);

        var highest = monster.ThreatInfo.GetTargets(CreatureTargetPriority.HighThreat);
        var lowest = monster.ThreatInfo.GetTargets(CreatureTargetPriority.LowThreat);

        Assert.Single(highest);
        Assert.Single(lowest);
        Assert.Equal(Fixture.SecondTestUser.Guid, highest[0].Guid);
        Assert.Equal(Fixture.TestUser.Guid, lowest[0].Guid);
    }
}
