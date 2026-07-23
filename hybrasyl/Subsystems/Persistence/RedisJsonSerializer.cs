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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Newtonsoft.Json;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     The System.Text.Json serializer for Redis persistence. Wire format is plain JSON
///     trees (no reference metadata); the member contract comes from the same
///     [JsonObject(OptIn)]/[JsonProperty] attributes Newtonsoft used, interpreted by
///     <see cref="NewtonsoftCompatResolver" />. The contract is pinned by
///     Hybrasyl.Tests/RedisSerialization.cs and the golden corpus.
/// </summary>
public static class RedisJsonSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new NewtonsoftCompatResolver(),
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
///     Gives System.Text.Json the three Newtonsoft behaviors the persistence contract
///     depends on, keyed off the existing Newtonsoft attributes so the ~200 attribute
///     sites stay untouched until the phase-2 re-attribution:
///     opt-in membership ([JsonObject(OptIn)] + [JsonProperty], walking the type
///     hierarchy and including non-public members), materialization through a
///     parameterless constructor of any visibility (so deserialization never re-runs
///     scripted constructor logic), and [JsonProperty] Order support.
/// </summary>
public class NewtonsoftCompatResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        // Opt-in enumerable types (Legend, MessageStore family) are handled by
        // OptInEnumerableConverterFactory - STJ metadata cannot make an object
        // out of a type it classifies as a collection
        if (!IsOptIn(type) || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            return base.GetTypeInfo(type, options);

        var typeInfo = base.GetTypeInfo(type, options);
        typeInfo.Properties.Clear();
        foreach (var member in GetWireMembers(type))
            typeInfo.Properties.Add(CreateProperty(typeInfo, member));

        var ctor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, Type.EmptyTypes, modifiers: null);
        if (ctor is not null)
            typeInfo.CreateObject = () => ctor.Invoke(null);

        return typeInfo;
    }

    /// <summary>
    ///     Newtonsoft resolves [JsonObject] from the nearest type in the hierarchy that
    ///     carries it; absence anywhere means the default (opt-out) contract applies.
    /// </summary>
    internal static bool IsOptIn(Type type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            var attr = t.GetCustomAttribute<JsonObjectAttribute>(inherit: false);
            if (attr is not null) return attr.MemberSerialization == MemberSerialization.OptIn;
        }

        return false;
    }

    /// <summary>
    ///     All [JsonProperty]-annotated fields and properties, any visibility, walking
    ///     the hierarchy most-derived first so overrides win the name dedup.
    /// </summary>
    internal static IEnumerable<MemberInfo> GetWireMembers(Type type)
    {
        var seen = new HashSet<string>();
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            foreach (var member in t.GetMembers(BindingFlags.Instance | BindingFlags.Public |
                                                BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (member is not (PropertyInfo or FieldInfo)) continue;
                if (member.GetCustomAttribute<JsonPropertyAttribute>() is null) continue;
                if (seen.Add(member.Name)) yield return member;
            }
    }

    private static JsonPropertyInfo CreateProperty(JsonTypeInfo typeInfo, MemberInfo member)
    {
        var attr = member.GetCustomAttribute<JsonPropertyAttribute>();
        switch (member)
        {
            case PropertyInfo pi:
            {
                var prop = typeInfo.CreateJsonPropertyInfo(pi.PropertyType, attr!.PropertyName ?? pi.Name);
                if (pi.GetMethod is not null) prop.Get = pi.GetValue;
                if (pi.SetMethod is not null) prop.Set = pi.SetValue;
                prop.Order = attr.Order;
                return prop;
            }
            case FieldInfo fi:
            {
                var prop = typeInfo.CreateJsonPropertyInfo(fi.FieldType, attr!.PropertyName ?? fi.Name);
                prop.Get = fi.GetValue;
                prop.Set = fi.SetValue;
                prop.Order = attr.Order;
                return prop;
            }
            default:
                throw new InvalidOperationException($"Unsupported wire member {member}");
        }
    }
}

/// <summary>
///     Serializes opt-in types that implement IEnumerable (Legend, the MessageStore
///     family) as objects, which is what Newtonsoft's [JsonObject] attribute means on
///     such types. STJ's metadata model classifies them as collections with no
///     override, so the object shape is written by hand from the wire-member list.
/// </summary>
public class OptInEnumerableConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        NewtonsoftCompatResolver.IsOptIn(typeToConvert) &&
        typeof(System.Collections.IEnumerable).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(OptInEnumerableConverter<>).MakeGenericType(typeToConvert));
}

internal class OptInEnumerableConverter<T> : System.Text.Json.Serialization.JsonConverter<T>
{
    private static readonly List<(MemberInfo Member, string Name, Type MemberType)> WireMembers = Describe();

    private static List<(MemberInfo, string, Type)> Describe()
    {
        var members = new List<(MemberInfo, string, Type)>();
        foreach (var member in NewtonsoftCompatResolver.GetWireMembers(typeof(T)))
        {
            var attr = member.GetCustomAttribute<JsonPropertyAttribute>();
            switch (member)
            {
                case PropertyInfo pi:
                    members.Add((pi, attr!.PropertyName ?? pi.Name, pi.PropertyType));
                    break;
                case FieldInfo fi:
                    members.Add((fi, attr!.PropertyName ?? fi.Name, fi.FieldType));
                    break;
            }
        }

        return members;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (member, name, memberType) in WireMembers)
        {
            writer.WritePropertyName(name);
            var memberValue = member is PropertyInfo pi ? pi.GetValue(value) : ((FieldInfo)member).GetValue(value);
            JsonSerializer.Serialize(writer, memberValue, memberType, options);
        }

        writer.WriteEndObject();
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected object for {typeof(T)}, got {reader.TokenType}");

        var ctor = typeof(T).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, Type.EmptyTypes, modifiers: null);
        if (ctor is null)
            throw new JsonException($"{typeof(T)} has no parameterless constructor for deserialization");
        var result = (T)ctor.Invoke(null);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();
            reader.Read();
            var match = WireMembers.FirstOrDefault(
                predicate: m => string.Equals(m.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            if (match.Member is null)
            {
                reader.Skip();
                continue;
            }

            var memberValue = JsonSerializer.Deserialize(ref reader, match.MemberType, options);
            if (match.Member is PropertyInfo { SetMethod: not null } prop)
                prop.SetValue(result, memberValue);
            else if (match.Member is FieldInfo field)
                field.SetValue(result, memberValue);
        }

        (result as IJsonOnDeserialized)?.OnDeserialized();
        return result;
    }
}
