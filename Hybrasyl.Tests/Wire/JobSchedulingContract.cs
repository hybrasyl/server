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

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Timers;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Every job in the jobs namespace is actually schedulable.
/// </summary>
/// <remarks>
///     <c>World.StartTimers</c> discovers jobs by namespace and binds them by reflection: it needs
///     a public static <c>Interval</c> field and an <c>Execute</c> whose signature can become an
///     <c>ElapsedEventHandler</c>. A class that gets any of that wrong is not a build error and not
///     a test failure — <c>StartTimers</c> logs a line and moves on, and the job simply never runs.
///     A reaper that never runs looks exactly like a reaper with nothing to reap.
/// </remarks>
/// <remarks>
///     In the Hybrasyl collection because several jobs read <c>Game.ActiveConfiguration</c> in
///     their static initializer, which throws until the fixture has stood the game up.
/// </remarks>
[Collection("Hybrasyl")]
public class JobSchedulingContract
{
    public JobSchedulingContract(HybrasylFixture fixture) { }

    public static TheoryData<Type> JobClasses
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var type in typeof(Game).Assembly.GetTypes()
                         .Where(t => t.Namespace == "Hybrasyl.Subsystems.Jobs")
                         .Where(t => Attribute.GetCustomAttribute(t, typeof(CompilerGeneratedAttribute)) is null))
                data.Add(type);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(JobClasses))]
    public void EveryJob_SatisfiesWhatStartTimersBindsAgainst(Type jobClass)
    {
        var interval = jobClass.GetField("Interval");
        Assert.True(interval is not null, $"{jobClass.Name} has no Interval field; it would never be scheduled");
        Assert.True(interval!.GetValue(null) is int seconds && seconds > 0,
            $"{jobClass.Name}.Interval must be a positive int of seconds");

        var execute = jobClass.GetMethod("Execute");
        Assert.True(execute is not null, $"{jobClass.Name} has no Execute method; it would never be scheduled");

        // The bind StartTimers actually performs. It throws rather than returning null on a
        // signature mismatch, which is the failure this test exists to surface.
        var handler = Delegate.CreateDelegate(typeof(ElapsedEventHandler), execute!);
        Assert.NotNull(handler);
    }

    /// <summary>The discovery itself finds something, so an empty sweep cannot pass this file.</summary>
    [Fact]
    public void TheJobsNamespace_IsNotEmpty()
    {
        Assert.NotEmpty(JobClasses);
    }
}
