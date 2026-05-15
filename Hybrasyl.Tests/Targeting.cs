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
using System.Linq;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Targeting(HybrasylFixture fixture)
{
    public HybrasylFixture Fixture { get; set; } = fixture;


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
                X = (byte) (Fixture.TestUser.X - i),
                Y = Fixture.TestUser.Y
            };
            Fixture.Map.InsertMonster(bait);
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

        Fixture.Map.InsertMonster(bait);
        Fixture.Map.InsertMonster(bait2);

        var castable = Game.World.WorldData.GetByIndex<Castable>("athar meall");
        Assert.NotNull(castable);

        var targets = Fixture.TestUser.GetTargets(castable, bait);
        Assert.Equal(2, targets.Count);
        Fixture.Map.Clear();
        Fixture.Map.InsertMonster(bait2);
        var targets2 = Fixture.TestUser.GetTargets(castable, bait2);
        Assert.Single(targets2);
    }
}