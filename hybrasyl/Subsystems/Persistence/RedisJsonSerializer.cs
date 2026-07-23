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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     The System.Text.Json serializer for Redis persistence. Wire format is plain JSON
///     trees (no reference metadata); the member contract comes from
///     [Persistable]/[Persist] attributes, compiled once per type into a
///     <see cref="WirePlan" /> and applied by <see cref="PersistenceContractResolver" />
///     (plain objects) or <see cref="OptInEnumerableConverterFactory" /> (types that
///     implement IEnumerable but persist as objects). The contract is pinned by
///     Hybrasyl.Tests/RedisSerialization.cs and the golden corpus.
/// </summary>
public static class RedisJsonSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new PersistenceContractResolver(),
        // Redis blobs are not HTML; relaxed escaping keeps game text legible
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new InventoryJsonConverter(),
            new EquipmentJsonConverter(),
            new SkillBookJsonConverter(),
            new SpellBookJsonConverter(),
            new OptInEnumerableConverterFactory()
        }
    };

    public static byte[] Serialize(object o) =>
        o == null ? null : JsonSerializer.SerializeToUtf8Bytes(o, o.GetType(), Options);

    public static T Deserialize<T>(byte[] data) =>
        data == null ? default : JsonSerializer.Deserialize<T>(data, Options);
}

/// <summary>
///     One wire member of a [Persistable] type: name, declared type, ordering, and
///     compiled accessors (expression-compiled so non-public members work at
///     near-direct-access speed; Set is null for members with no usable setter).
/// </summary>
internal sealed record WireMember(string Name, Type MemberType, int Order,
    Func<object, object> Get, Action<object, object> Set);

/// <summary>
///     The compiled persistence contract for one [Persistable] type: its ordered wire
///     members, a case-insensitive name index, and a compiled parameterless-constructor
///     factory. Built once per type on first use; both serialization mechanisms consume
///     the same plan, so the contract cannot drift between them.
/// </summary>
internal sealed class WirePlan
{
    private static readonly ConcurrentDictionary<Type, WirePlan> Cache = new();

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

    public static WirePlan For(Type type) => Cache.GetOrAdd(type, valueFactory: Build);

    private static WirePlan Build(Type type)
    {
        var members = GetWireMembers(type)
            .Select(selector: Describe)
            .OrderBy(keySelector: m => m.Order) // stable: declaration order within equal Order
            .ToArray();

        var ctor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, Type.EmptyTypes, modifiers: null);
        if (ctor is null)
            throw new InvalidOperationException(
                $"{type} is [Persistable] but has no parameterless constructor for deserialization");
        var createInstance = Expression.Lambda<Func<object>>(Expression.New(ctor)).Compile();

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
        var order = member.GetCustomAttribute<Persist>()!.Order;
        return new WireMember(member.Name, memberType, order, BuildGetter(member), BuildSetter(member));
    }

    private static Func<object, object> BuildGetter(MemberInfo member)
    {
        var instance = Expression.Parameter(typeof(object), "o");
        var access = Expression.MakeMemberAccess(
            Expression.Convert(instance, member.DeclaringType!), member);
        return Expression.Lambda<Func<object, object>>(
            Expression.Convert(access, typeof(object)), instance).Compile();
    }

    private static Action<object, object> BuildSetter(MemberInfo member)
    {
        if (member is PropertyInfo { SetMethod: null } or FieldInfo { IsInitOnly: true })
            return null;
        var instance = Expression.Parameter(typeof(object), "o");
        var value = Expression.Parameter(typeof(object), "v");
        var memberType = member is PropertyInfo pi ? pi.PropertyType : ((FieldInfo)member).FieldType;
        var assign = Expression.Assign(
            Expression.MakeMemberAccess(Expression.Convert(instance, member.DeclaringType!), member),
            Expression.Convert(value, memberType));
        return Expression.Lambda<Action<object, object>>(assign, instance, value).Compile();
    }
}

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

/// <summary>
///     Serializes [Persistable] types that implement IEnumerable (Legend, the
///     MessageStore family) as objects. STJ's metadata model classifies them as
///     collections with no override, so the object shape is written by hand from the
///     same <see cref="WirePlan" /> the resolver uses.
/// </summary>
public class OptInEnumerableConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        PersistenceContractResolver.IsOptInEnumerable(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(OptInEnumerableConverter<>).MakeGenericType(typeToConvert));
}

internal class OptInEnumerableConverter<T> : JsonConverter<T>
{
    private static readonly WirePlan Plan = WirePlan.For(typeof(T));

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var member in Plan.Members)
        {
            writer.WritePropertyName(member.Name);
            JsonSerializer.Serialize(writer, member.Get(value), member.MemberType, options);
        }

        writer.WriteEndObject();
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected object for {typeof(T)}, got {reader.TokenType}");

        var result = (T)Plan.CreateInstance();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();
            reader.Read();
            if (!Plan.ByName.TryGetValue(propertyName!, out var member) || member.Set is null)
            {
                reader.Skip();
                continue;
            }

            member.Set(result, JsonSerializer.Deserialize(ref reader, member.MemberType, options));
        }

        (result as IJsonOnDeserialized)?.OnDeserialized();
        return result;
    }
}
