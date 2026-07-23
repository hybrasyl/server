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

#nullable enable

using Hybrasyl.Internals.Attributes;
using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     Gives System.Text.Json the behaviors the persistence contract depends on that it
///     lacks natively: opt-in membership ([Persistable] + [Persist], including non-public
///     members), materialization through a parameterless constructor of any visibility
///     (so deserialization never re-runs scripted constructor logic), and
///     [Persist(Order = N)] support. Types that implement IEnumerable are deferred to
///     <see cref="OptInEnumerableConverterFactory" />.
/// </summary>
public class PersistenceContractResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (!IsOptIn(type) || IsOptInEnumerable(type))
            return base.GetTypeInfo(type, options);

        var typeInfo = base.GetTypeInfo(type, options);
        var plan = WirePlan.For(type);
        typeInfo.Properties.Clear();
        foreach (var member in plan.Members)
        {
            var prop = typeInfo.CreateJsonPropertyInfo(member.MemberType, member.Name);
            prop.Get = member.Get;
            if (member.Set is not null) prop.Set = member.Set;
            prop.Order = member.Order;
            typeInfo.Properties.Add(prop);
        }

        typeInfo.CreateObject = plan.CreateInstance;
        return typeInfo;
    }

    /// <summary>
    ///     A type is opt-in when [Persistable] appears anywhere in its hierarchy;
    ///     absence means the default contract applies.
    /// </summary>
    internal static bool IsOptIn(Type type) =>
        type.GetCustomAttribute<Persistable>(inherit: true) is not null;

    /// <summary>
    ///     [Persistable] types that implement IEnumerable persist as objects but are
    ///     classified as collections by STJ's metadata model, so they serialize through
    ///     OptInEnumerableConverterFactory rather than the resolver.
    /// </summary>
    internal static bool IsOptInEnumerable(Type type) =>
        IsOptIn(type) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
}
