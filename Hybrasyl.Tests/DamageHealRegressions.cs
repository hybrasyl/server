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
using Hybrasyl.Subsystems.Formulas;
using Hybrasyl.Xml.Objects;
using System.Collections.Generic;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Tests;

/// <summary>
///     Regressions for the damage/heal application bugs found alongside HS-1505. Each test
///     pins a behaviour that was wrong in a way the suite could not see: the shield branch
///     looked symmetric, the status-intensity lookup looked null-guarded, and the heal
///     modifier looked gated. All three were green before the fixes.
/// </summary>
[Collection("Hybrasyl")]
public class DamageHealRegressions
{
    public DamageHealRegressions(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    public HybrasylFixture Fixture { get; set; }

    private Monster NewGabbaghoul()
    {
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        return new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
    }

    // A castable whose status list deliberately does NOT contain the status name the
    // NumberCruncher tick will be asked about.
    private static Castable CastableWithUnrelatedStatus() => new()
    {
        Name = "Test Castable",
        Effects = new CastableEffects
        {
            Statuses = new Statuses
            {
                Add = new List<AddStatus>
                {
                    new() { Value = "SomeOtherStatus", Intensity = 3.0f }
                }
            }
        }
    };

    private static ModifierEffect HealEffect(uint amount) => new()
    {
        Heal = new StatusHeal { Simple = new SimpleQuantity { Value = amount } }
    };

    private static ModifierEffect DamageEffect(uint amount) => new()
    {
        Damage = new StatusDamage { Simple = new SimpleQuantity { Value = amount } }
    };

    // Site: Creature.Damage shield block. The full-absorption branch zeroed damage and then
    // subtracted it from the shield, always subtracting zero, so a shield large enough to
    // absorb a hit never depleted and was effectively infinite.
    [Fact]
    public void ShieldDepletesWhenItFullyAbsorbsDamage()
    {
        var monster = NewGabbaghoul();
        monster.X = 31;
        monster.Y = 31;
        Fixture.Map.InsertMonster(monster);

        monster.Stats.Shield = 100;
        var hpBefore = monster.Stats.Hp;

        monster.Damage(40, damageType: DamageType.Direct);

        Assert.Equal(60, monster.Stats.Shield);
        Assert.Equal(hpBefore, monster.Stats.Hp);

        // A second absorbed hit must draw the shield down further, not leave it untouched.
        monster.Damage(40, damageType: DamageType.Direct);

        Assert.Equal(20, monster.Stats.Shield);
        Assert.Equal(hpBefore, monster.Stats.Hp);
    }

    // The partial-absorption branch was always correct; pinned so a rewrite of the block
    // cannot regress it while fixing the full-absorption case.
    [Fact]
    public void ShieldPartiallyAbsorbsAndRemainderReachesHp()
    {
        var monster = NewGabbaghoul();
        monster.X = 32;
        monster.Y = 32;
        Fixture.Map.InsertMonster(monster);

        monster.Stats.Shield = 30;
        var hpBefore = monster.Stats.Hp;

        monster.Damage(50, damageType: DamageType.Direct);

        Assert.Equal(0, monster.Stats.Shield);
        Assert.Equal(hpBefore - 20, monster.Stats.Hp);
    }

    // Site: NumberCruncher status ticks. Where(...).ToList() returns an EMPTY list, never
    // null, so the `statusAdd != null` guard did not protect statusAdd[0] and the lookup
    // threw whenever the status name matched no Add entry.
    [Fact]
    public void StatusTickWithNoMatchingAddEntryDoesNotThrow()
    {
        var monster = NewGabbaghoul();
        var castable = CastableWithUnrelatedStatus();

        var damageEx = Record.Exception(() => NumberCruncher.CalculateDamage(
            castable, DamageEffect(50), monster, Fixture.TestUser, "StatusNotInAddList"));
        Assert.Null(damageEx);

        var healEx = Record.Exception(() => NumberCruncher.CalculateHeal(
            castable, HealEffect(50), monster, Fixture.TestUser, "StatusNotInAddList"));
        Assert.Null(healEx);
    }

    // Intensity must still be picked up when the name DOES match, so the FirstOrDefault
    // rewrite cannot pass by always returning the default.
    [Fact]
    public void StatusTickAppliesIntensityFromMatchingAddEntry()
    {
        var monster = NewGabbaghoul();
        Fixture.ResetTestUserStats();

        var castable = new Castable
        {
            Name = "Test Castable",
            Effects = new CastableEffects
            {
                Statuses = new Statuses
                {
                    Add = new List<AddStatus>
                    {
                        new() { Value = "OtherStatus", Intensity = 9.0f },
                        new() { Value = "ScaledStatus", Intensity = 3.0f }
                    }
                }
            }
        };

        var heal = NumberCruncher.CalculateHeal(
            castable, HealEffect(50), Fixture.TestUser, monster, "ScaledStatus");

        // 50 base * intensity 3. Asserts the SECOND entry is selected, so a fix that simply
        // took element [0] unconditionally would report 9x and fail here.
        Assert.Equal(150, heal);
    }

    // Site: NumberCruncher.CalculateHeal (status tick). Gated on the target's Inbound heal
    // modifier but multiplied by the target's OUTBOUND modifier, so a target with inbound
    // set and outbound zero had its status heals scaled to nothing.
    [Fact]
    public void StatusHealUsesTargetInboundModifierNotOutbound()
    {
        var monster = NewGabbaghoul();
        Fixture.ResetTestUserStats();

        Fixture.TestUser.Stats.BaseInboundHealModifier = 2.0;
        Fixture.TestUser.Stats.BaseOutboundHealModifier = 0.0;

        // Deliberately uses a MATCHING status entry with intensity 1, so this test isolates
        // the modifier and does not also depend on the statusAdd lookup fix.
        var castable = new Castable
        {
            Name = "Test Castable",
            Effects = new CastableEffects
            {
                Statuses = new Statuses
                {
                    Add = new List<AddStatus> { new() { Value = "NeutralStatus", Intensity = 1.0f } }
                }
            }
        };

        try
        {
            var heal = NumberCruncher.CalculateHeal(
                castable, HealEffect(50), Fixture.TestUser, monster, "NeutralStatus");

            // Pre-fix this multiplied by the target's outbound modifier (0.0) and returned 0.
            Assert.Equal(100, heal);
        }
        finally
        {
            Fixture.TestUser.Stats.BaseInboundHealModifier = 0.0;
            Fixture.TestUser.Stats.BaseOutboundHealModifier = 0.0;
        }
    }
}
