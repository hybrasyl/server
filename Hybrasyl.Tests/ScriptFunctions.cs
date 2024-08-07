﻿// This file is part of Project Hybrasyl.
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
using System.Threading;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class ScriptFunctions
{
    private static HybrasylFixture Fixture;

    public ScriptFunctions(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void HasKilled()
    {
        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.SpellBook.Clear();
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Map.Clear();

        var assail = Game.World.WorldData.GetByIndex<Castable>("Assail");
        Assert.NotNull(assail);
        Fixture.TestUser.SkillBook.Add(assail);
        Assert.NotEmpty(Fixture.TestUser.SkillBook);

        Fixture.TestUser.Teleport(Fixture.Map.Id, 15, 15);
        Fixture.TestUser.Stats.Level = 99;
        // Let's put one of those beefy arms on him for good measure
        Fixture.TestUser.Stats.BaseStr = 255;

        var baitTemplate = Game.World.WorldData.Get<Creature>("Honey Bee");
        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 400,
                Hp = 400
            },
            Name = "Bee Bait",
            X = (byte)(Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };
        Game.World.Insert(bait);
        Fixture.Map.Insert(bait, bait.X, bait.Y);
        Fixture.TestUser.Turn(Direction.West);
        Assert.NotEmpty(Fixture.TestUser.GetFacingObjects());
        Fixture.TestUser.AssailAttack(Direction.West);

        Assert.Equal((uint)0, bait.Stats.Hp);
        // Wait for bee to be properly dead
        Thread.Sleep(1000);
        Assert.True(bait.DeathProcessed);

        var scriptObject = new HybrasylUser(Fixture.TestUser);

        Assert.True(scriptObject.HasKilled("Bee Bait"));

        var bait2 = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 400,
                Hp = 400
            },
            Name = "Bee Bait",
            X = (byte)(Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };
        Game.World.Insert(bait2);
        Fixture.Map.Insert(bait2, bait2.X, bait2.Y);
        Fixture.TestUser.Turn(Direction.West);
        Assert.NotEmpty(Fixture.TestUser.GetFacingObjects());
        Fixture.TestUser.AssailAttack(Direction.West);

        Assert.Equal((uint)0, bait2.Stats.Hp);
        // Wait for bee to be properly dead
        Thread.Sleep(1);
        Assert.True(bait2.DeathProcessed);
        Assert.True(scriptObject.HasKilled("Bee Bait", 2));
        Assert.True(scriptObject.HasKilled("Bee Bait", 2, 5));
    }

    [Fact]
    public void HasKilledSince()
    {
        Fixture.TestUser.SkillBook.Clear();
        Fixture.TestUser.SpellBook.Clear();
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Map.Clear();

        var assail = Game.World.WorldData.GetByIndex<Castable>("Assail");
        Assert.NotNull(assail);
        Fixture.TestUser.SkillBook.Add(assail);
        Assert.NotEmpty(Fixture.TestUser.SkillBook);

        Fixture.TestUser.Teleport(Fixture.Map.Id, 15, 15);
        Fixture.TestUser.Stats.Level = 99;
        // Let's put one of those beefy arms on him for good measure
        Fixture.TestUser.Stats.BaseStr = 255;

        var baitTemplate = Game.World.WorldData.Get<Creature>("Honey Bee");
        var bait = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 400,
                Hp = 400
            },
            Name = "Bee Bait",
            X = (byte)(Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };
        Game.World.Insert(bait);
        Fixture.Map.Insert(bait, bait.X, bait.Y);
        Fixture.TestUser.Turn(Direction.West);
        Assert.NotEmpty(Fixture.TestUser.GetFacingObjects());
        Fixture.TestUser.AssailAttack(Direction.West);

        Assert.Equal((uint)0, bait.Stats.Hp);
        // Wait for bee to be properly dead
        Thread.Sleep(1000);
        Assert.True(bait.DeathProcessed);

        var scriptObject = new HybrasylUser(Fixture.TestUser);

        var ts = new DateTimeOffset(DateTime.Now - TimeSpan.FromSeconds(30));

        Assert.True(scriptObject.HasKilledSince("Bee Bait",
            (int)new DateTimeOffset(DateTime.Now - TimeSpan.FromSeconds(30)).ToUnixTimeSeconds()));


        // Create a buffer of time we can use for the next series of checks
        Thread.Sleep(5000);

        var bait2 = new Monster(baitTemplate, SpawnFlags.AiDisabled, 99)
        {
            Stats =
            {
                BaseHp = 400,
                Hp = 400
            },
            Name = "Bee Bait",
            X = (byte)(Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };

        Game.World.Insert(bait2);
        Fixture.Map.Insert(bait2, bait2.X, bait2.Y);
        Fixture.TestUser.Turn(Direction.West);
        Assert.NotEmpty(Fixture.TestUser.GetFacingObjects());
        Fixture.TestUser.AssailAttack(Direction.West);

        Assert.Equal((uint)0, bait2.Stats.Hp);
        // Wait for bee to be properly dead
        Thread.Sleep(1);
        Assert.True(bait2.DeathProcessed);
        Assert.True(scriptObject.HasKilledSince("Bee Bait",
            (int)new DateTimeOffset(DateTime.Now - TimeSpan.FromSeconds(30)).ToUnixTimeSeconds(), 2));
        Assert.False(scriptObject.HasKilledSince("Bee Bait",
            (int)new DateTimeOffset(DateTime.Now - TimeSpan.FromSeconds(1)).ToUnixTimeSeconds(), 2));
        Assert.True(scriptObject.HasKilledSince("Bee Bait",
            (int)new DateTimeOffset(DateTime.Now - TimeSpan.FromSeconds(1)).ToUnixTimeSeconds()));
    }
}