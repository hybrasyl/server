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

using Hybrasyl.Internals.Attributes;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Persistence;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Hybrasyl.Tests;

[Persistable]
internal class CaseCollidingWireType
{
    [Persist] private int value;
    [Persist] public int Value { get; set; }
}

[Collection("Hybrasyl")]
public class WirePlanTests
{
    [Fact]
    public void CaseInsensitiveNameCollisionThrowsExplicitly()
    {
        // Reads are case-insensitive, so two wire members differing only by case
        // cannot share a wire; the plan must say so, not throw an opaque
        // ArgumentException from dictionary construction
        var ex = Assert.Throws<InvalidOperationException>(() => WirePlan.For(typeof(CaseCollidingWireType)));
        Assert.Contains("Value", ex.Message);
        Assert.Contains(nameof(CaseCollidingWireType), ex.Message);
    }

    [Fact]
    public void PlansBuildAndMaterializeForAllPersistedRootTypes()
    {
        // Every [RedisType] root must have a working plan AND be deserializable
        var roots = typeof(RedisJsonSerializer).Assembly.GetTypes()
            .Where(predicate: t => t.GetCustomAttribute<RedisType>() is not null)
            .ToList();
        Assert.NotEmpty(roots);
        foreach (var root in roots)
            Assert.NotNull(WirePlan.For(root).CreateInstance());
    }

    [Fact]
    public void SerializeOnlyTypesWithoutParameterlessCtorStillGetPlans()
    {
        // Monster is [Persistable] via Creature but has no parameterless ctor and no
        // deserialize path; the plan must build (serialize side), and only the
        // deserialize side may throw
        var plan = WirePlan.For(typeof(Monster));
        Assert.NotEmpty(plan.Members);
        Assert.Throws<InvalidOperationException>(() => plan.CreateInstance());
    }
}
