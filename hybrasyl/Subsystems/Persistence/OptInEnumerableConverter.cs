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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hybrasyl.Subsystems.Persistence;

internal class OptInEnumerableConverter<T> : JsonConverter<T>
{
    private static readonly WirePlan _plan = WirePlan.For(typeof(T));

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var member in _plan.Members)
        {
            writer.WritePropertyName(member.Name);
            JsonSerializer.Serialize(writer, member.Get(value!), member.MemberType, options);
        }

        writer.WriteEndObject();
    }

    public override T Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected object for {typeof(T)}, got {reader.TokenType}");

        var result = (T)_plan.CreateInstance();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            // Non-null: the reader is positioned on a property name token
            var propertyName = reader.GetString()!;
            reader.Read();
            if (!_plan.ByName.TryGetValue(propertyName, out var member) || member.Set is null)
            {
                reader.Skip();
                continue;
            }

            member.Set(result!, JsonSerializer.Deserialize(ref reader, member.MemberType, options));
        }

        (result as IJsonOnDeserialized)?.OnDeserialized();
        return result;
    }
}
