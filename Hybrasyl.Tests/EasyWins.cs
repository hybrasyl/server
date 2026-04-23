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
using Hybrasyl.Subsystems.Formulas;
using Hybrasyl.Xml.Objects;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class EasyWins
{
    public HybrasylFixture Fixture { get; }

    public EasyWins(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    // --- Recursive creature HP allocation ---

    [Fact]
    public void MonsterAllocateStats_LevelIsRestoredAfterAllocation()
    {
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 50);

        // After AllocateStats, the monster's level should be the target level
        Assert.Equal(50, monster.Stats.Level);
    }

    [Fact]
    public void MonsterAllocateStats_HigherLevel_HasMoreHp()
    {
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");

        var low = new Monster(monsterXml, SpawnFlags.AiDisabled, 10);
        var high = new Monster(monsterXml, SpawnFlags.AiDisabled, 50);

        // A level 50 monster should have more HP than a level 10 monster
        Assert.True(high.Stats.MaximumHp > low.Stats.MaximumHp,
            $"Level 50 HP ({high.Stats.MaximumHp}) should exceed level 10 HP ({low.Stats.MaximumHp})");
    }

    [Fact]
    public void MonsterAllocateStats_StatsIncrease_WithLevel()
    {
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");

        var low = new Monster(monsterXml, SpawnFlags.AiDisabled, 5);
        var high = new Monster(monsterXml, SpawnFlags.AiDisabled, 50);

        // Total stat points should be higher for a higher level monster
        var lowTotal = low.Stats.BaseStr + low.Stats.BaseInt + low.Stats.BaseWis +
                       low.Stats.BaseCon + low.Stats.BaseDex;
        var highTotal = high.Stats.BaseStr + high.Stats.BaseInt + high.Stats.BaseWis +
                        high.Stats.BaseCon + high.Stats.BaseDex;

        Assert.True(highTotal > lowTotal,
            $"Level 50 total stats ({highTotal}) should exceed level 5 ({lowTotal})");
    }

    // --- ItemObject FormulaVariables ---

    [Fact]
    public void ItemObject_Has12_FormulaVariables()
    {
        var props = typeof(ItemObject).GetProperties()
            .Where(p => p.IsDefined(typeof(FormulaVariable), false))
            .ToList();

        Assert.Equal(12, props.Count);
    }

    [Fact]
    public void ItemObject_FormulaVariables_IncludeExpectedProperties()
    {
        var props = typeof(ItemObject).GetProperties()
            .Where(p => p.IsDefined(typeof(FormulaVariable), false))
            .Select(p => p.Name)
            .ToHashSet();

        var expected = new[]
        {
            "Weight", "MaximumDurability", "MinLevel", "MinAbility",
            "MaxLevel", "MaxAbility", "MinLDamage", "MaxLDamage",
            "MinSDamage", "MaxSDamage", "Value", "Durability"
        };

        foreach (var name in expected)
            Assert.Contains(name, props);
    }

    [Fact]
    public void FormulaParser_ScansItemObject_ForVariables()
    {
        // FormulaParser's static constructor populates tokens for ItemObject
        var tokensField = typeof(FormulaParser)
            .GetField("FormulaTokens", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(tokensField);

        var tokens = tokensField.GetValue(null) as Dictionary<System.Type, List<PropertyInfo>>;
        Assert.NotNull(tokens);
        Assert.True(tokens.ContainsKey(typeof(ItemObject)));
        Assert.True(tokens[typeof(ItemObject)].Count >= 12,
            $"FormulaParser found {tokens[typeof(ItemObject)].Count} ItemObject variables, expected at least 12");
    }

    // --- Cone radius cap removal ---

    [Fact]
    public void ConeRadius_NotCappedByViewport()
    {
        // The cone intent processing in Creature.cs should use tile.Radius directly,
        // not Math.Min(tile.Radius, ViewportSize / 2). Verify by checking the source
        // doesn't contain the viewport cap pattern for cones.
        var source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "hybrasyl", "Objects", "Creature.cs"));

        // The old code: Math.Min(tile.Radius, Game.ActiveConfiguration.Constants.ViewportSize / 2)
        // should not appear near "foreach.*Cone" anymore
        Assert.DoesNotContain("Math.Min(tile.Radius, Game.ActiveConfiguration.Constants.ViewportSize / 2)", source);
    }
}
