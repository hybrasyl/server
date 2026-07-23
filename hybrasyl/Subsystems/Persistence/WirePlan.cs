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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     The compiled persistence contract for one [Persistable] type: its ordered wire
///     members, a case-insensitive name index, and a compiled parameterless-constructor
///     factory. Built once per type on first use; both serialization mechanisms consume
///     the same plan, so the contract cannot drift between them.
/// </summary>
internal sealed class WirePlan
{
    private static readonly ConcurrentDictionary<Type, WirePlan> _cache = new();

    private WirePlan(WireMember[] members, Func<object> createInstance)
    {
        Members = members;
        CreateInstance = createInstance;
        ByName = members.ToDictionary(keySelector: m => m.Name, elementSelector: m => m,
            StringComparer.OrdinalIgnoreCase);
    }

    public WireMember[] Members { get; }
    public Dictionary<string, WireMember> ByName { get; }
    public Func<object> CreateInstance { get; }

    public static WirePlan For(Type type) => _cache.GetOrAdd(type, valueFactory: Build);

    private static WirePlan Build(Type type)
    {
        var members = GetWireMembers(type)
            .Select(selector: Describe)
            .OrderBy(keySelector: m => m.Order) // stable: declaration order within equal Order
            .ToArray();

        // Reads resolve names case-insensitively, so case-only duplicates cannot share a wire
        var collisions = members
            .GroupBy(keySelector: m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Where(predicate: g => g.Count() > 1)
            .Select(selector: g => string.Join(", ", g.Select(selector: m => m.Name)))
            .ToList();
        if (collisions.Count > 0)
            throw new InvalidOperationException(
                $"{type.Name}: [Persist] member names collide case-insensitively ({string.Join("; ", collisions)}); " +
                "deserialization matches names case-insensitively, so these are ambiguous on the wire");

        // Serialize-only types (e.g. Monster) legitimately lack a parameterless ctor;
        // the plan must still build, so only the deserialize side may throw
        var ctor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, Type.EmptyTypes, modifiers: null);
        var createInstance = ctor is null
            ? () => throw new InvalidOperationException(
                $"{type} is [Persistable] but has no parameterless constructor for deserialization")
            : Expression.Lambda<Func<object>>(Expression.New(ctor)).Compile();

        return new WirePlan(members, createInstance);
    }

    /// <summary>
    ///     All [Persist]-annotated fields and properties, any visibility, walking the
    ///     hierarchy most-derived first so overrides win the name dedup.
    /// </summary>
    private static IEnumerable<MemberInfo> GetWireMembers(Type type)
    {
        var seen = new HashSet<string>();
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            foreach (var member in t.GetMembers(BindingFlags.Instance | BindingFlags.Public |
                                                BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (member is not (PropertyInfo or FieldInfo)) continue;
                if (member.GetCustomAttribute<Persist>() is null) continue;
                if (seen.Add(member.Name)) yield return member;
            }
    }

    private static WireMember Describe(MemberInfo member)
    {
        var memberType = member switch
        {
            PropertyInfo pi => pi.PropertyType,
            FieldInfo fi => fi.FieldType,
            _ => throw new InvalidOperationException($"Unsupported wire member {member}")
        };
        // Non-null: GetWireMembers only yields [Persist]-annotated members
        var order = member.GetCustomAttribute<Persist>()!.Order;
        return new WireMember(member.Name, memberType, order, BuildGetter(member), BuildSetter(member));
    }

    private static Func<object, object?> BuildGetter(MemberInfo member)
    {
        var instance = Expression.Parameter(typeof(object), "o");
        // DeclaringType is non-null: members come from Type.GetMembers, never module-level
        var access = Expression.MakeMemberAccess(
            Expression.Convert(instance, member.DeclaringType!), member);
        return Expression.Lambda<Func<object, object?>>(
            Expression.Convert(access, typeof(object)), instance).Compile();
    }

    private static Action<object, object?>? BuildSetter(MemberInfo member)
    {
        if (member is PropertyInfo { SetMethod: null } or FieldInfo { IsInitOnly: true })
            return null;
        var instance = Expression.Parameter(typeof(object), "o");
        var value = Expression.Parameter(typeof(object), "v");
        var memberType = member is PropertyInfo pi ? pi.PropertyType : ((FieldInfo)member).FieldType;
        // DeclaringType is non-null: members come from Type.GetMembers, never module-level
        var assign = Expression.Assign(
            Expression.MakeMemberAccess(Expression.Convert(instance, member.DeclaringType!), member),
            Expression.Convert(value, memberType));
        return Expression.Lambda<Action<object, object?>>(assign, instance, value).Compile();
    }
}
