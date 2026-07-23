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

using Hybrasyl.Casting;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Xml.Objects;
using System.Text.Json;
using System.Text.Json.Serialization;
using Book = Hybrasyl.Casting.Book;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     Shared wire logic for the SkillBook/SpellBook fixed-size array shape, including
///     the 1-based indexer quirk that leaves array position 0 null. The wire shape is
///     load-bearing contract; see the golden corpus.
/// </summary>
internal static class BookSlotConverter
{
    public static void Write(Utf8JsonWriter writer, Book book)
    {
        writer.WriteStartArray();
        // book[i] uses the 1-based client indexer, so array position 0 is always null
        // and position Size-1 never carries the last internal slot - a quirk of the
        // original converter, preserved because it is the on-disk shape
        for (byte i = 0; i < book.Size; i++)
        {
            var slot = book[i];
            if (slot?.Castable == null)
            {
                writer.WriteNullValue();
                continue;
            }

            writer.WriteStartObject();
            writer.WriteString("Name", slot.Castable.Name.ToLower());
            writer.WriteString("LastCast", slot.LastCast);
            writer.WriteNumber("TotalUses", slot.UseCount);
            writer.WriteNumber("MasteryLevel", slot.MasteryLevel);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    public static T Read<T>(ref Utf8JsonReader reader, T book) where T : Book
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var i = 0;
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var slotIndex = i++;
            if (slotIndex > book.Size) break;
            if (element.ValueKind != JsonValueKind.Object) continue;
            // A malformed slot entry costs that slot, not the whole character load
            if (!element.TryGetProperty("Name", out var nameProperty) ||
                nameProperty.ValueKind != JsonValueKind.String)
            {
                GameLog.Error("{Type} deserializer error: slot {Slot} has no castable name, dropping",
                    book.GetType().Name, slotIndex);
                continue;
            }

            // Non-null: ValueKind is String
            var name = nameProperty.GetString()!;
            // Castables are indexed by exact and lowercase name; the wire is lowercase,
            // so the first lookup hits - any other casing falls back through ToLower
            if (!Game.World.WorldData.TryGetValueByIndex(name, out Castable castable))
                Game.World.WorldData.TryGetValueByIndex(name.ToLower(), out castable);
            book[(byte)slotIndex] = new BookSlot
            {
                Castable = castable,
                UseCount = element.TryGetProperty("TotalUses", out var uses) ? uses.GetUInt32() : 0,
                MasteryLevel = element.TryGetProperty("MasteryLevel", out var mastery) ? mastery.GetByte() : (byte)0,
                LastCast = element.TryGetProperty("LastCast", out var lastCast) &&
                           lastCast.ValueKind == JsonValueKind.String &&
                           lastCast.TryGetDateTime(out var cast)
                    ? cast
                    : default
            };
        }

        (book as IJsonOnDeserialized)?.OnDeserialized();
        return book;
    }
}
