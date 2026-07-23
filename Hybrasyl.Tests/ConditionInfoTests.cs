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

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class ConditionInfoTests(HybrasylFixture fixture)
{
    public HybrasylFixture Fixture { get; set; } = fixture;

    [Fact]
    public void AsleepSetterSetsSleepNotStun()
    {
        var user = Fixture.CreateUser("ConditionAsleepUser");
        user.Condition.Asleep = true;

        Assert.True(user.Condition.Conditions.HasFlag(CreatureCondition.Sleep));
        Assert.False(user.Condition.Conditions.HasFlag(CreatureCondition.Stun));

        user.Condition.Asleep = false;
        Assert.Equal((CreatureCondition)0, user.Condition.Conditions);
    }

    [Fact]
    public void ShoutProhibitionGetterReadsProhibitShout()
    {
        var user = Fixture.CreateUser("ConditionShoutUser");

        user.Condition.Conditions = CreatureCondition.ProhibitShout;
        Assert.True(user.Condition.IsShoutProhibited);

        user.Condition.Conditions = CreatureCondition.ProhibitEquipChange;
        Assert.False(user.Condition.IsShoutProhibited);
    }

    [Fact]
    public void ClearingOneProhibitionPreservesOtherConditions()
    {
        var user = Fixture.CreateUser("ConditionClearUser");
        var cases = new (string Name, CreatureCondition Flag, Action<ConditionInfo, bool> Set)[]
        {
            ("IsItemUseProhibited", CreatureCondition.ProhibitItemUse, (c, v) => c.IsItemUseProhibited = v),
            ("IsSayProhibited", CreatureCondition.ProhibitSpeech, (c, v) => c.IsSayProhibited = v),
            ("IsShoutProhibited", CreatureCondition.ProhibitShout, (c, v) => c.IsShoutProhibited = v),
            ("IsWhisperProhibited", CreatureCondition.ProhibitWhisper, (c, v) => c.IsWhisperProhibited = v),
            ("IsEquipmentChangeProhibited", CreatureCondition.ProhibitEquipChange,
                (c, v) => c.IsEquipmentChangeProhibited = v),
            ("IsHpIncreaseProhibited", CreatureCondition.ProhibitHpIncrease, (c, v) => c.IsHpIncreaseProhibited = v),
            ("IsMpIncreaseProhibited", CreatureCondition.ProhibitMpIncrease, (c, v) => c.IsMpIncreaseProhibited = v),
            ("IsMpDecreaseProhibited", CreatureCondition.ProhibitMpDecrease, (c, v) => c.IsMpDecreaseProhibited = v),
            ("IsHpRegenProhibited", CreatureCondition.ProhibitHpRegen, (c, v) => c.IsHpRegenProhibited = v),
            ("IsMpRegenProhibited", CreatureCondition.ProhibitMpRegen, (c, v) => c.IsMpRegenProhibited = v)
        };

        foreach (var (name, flag, set) in cases)
        {
            // Clearing one prohibition must clear exactly that flag
            user.Condition.Conditions = flag | CreatureCondition.Sleep;
            set(user.Condition, false);
            Assert.False(user.Condition.Conditions.HasFlag(flag), $"{name}: flag not cleared");
            Assert.True(user.Condition.Conditions.HasFlag(CreatureCondition.Sleep),
                $"{name}: clearing the prohibition wiped unrelated condition flags");

            user.Condition.Conditions = 0;
            set(user.Condition, true);
            Assert.True(user.Condition.Conditions.HasFlag(flag), $"{name}: flag not set");
        }
    }
}
