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

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        // Formula/scripting can produce non-finite doubles; a save must never abort on one
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters =
        {
            new InventoryJsonConverter(),
            new EquipmentJsonConverter(),
            new SkillBookJsonConverter(),
            new SpellBookJsonConverter(),
            new OptInEnumerableConverterFactory()
        }
    };

    public static byte[]? Serialize(object? o) =>
        o == null ? null : JsonSerializer.SerializeToUtf8Bytes(o, o.GetType(), Options);

    public static T? Deserialize<T>(byte[]? data) =>
        data == null ? default : JsonSerializer.Deserialize<T>(data, Options);
}
