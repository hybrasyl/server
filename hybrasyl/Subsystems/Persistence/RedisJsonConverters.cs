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

using Hybrasyl.Casting;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Players;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Book = Hybrasyl.Casting.Book;
using Equipment = Hybrasyl.Subsystems.Players.Equipment;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     System.Text.Json ports of the Newtonsoft persistence converters. The wire shapes
///     (slot dictionaries for inventory/equipment, null-padded fixed-size arrays for
///     books, including the 1-based indexer quirk that leaves array position 0 null)
///     are load-bearing contract; see the golden corpus.
/// </summary>
public class InventoryJsonConverter : JsonConverter<Inventory>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Inventory);

    public override void Write(Utf8JsonWriter writer, Inventory value, JsonSerializerOptions options) =>
        SlotConverter.Write(writer, value, options);

    public override Inventory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        SlotConverter.Read(ref reader, new Inventory(Inventory.DefaultSize), options, "Inventory");
}

public class EquipmentJsonConverter : JsonConverter<Equipment>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Equipment);

    public override void Write(Utf8JsonWriter writer, Equipment value, JsonSerializerOptions options) =>
        SlotConverter.Write(writer, value, options);

    public override Equipment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        SlotConverter.Read(ref reader, new Equipment(Equipment.DefaultSize), options, "Equipment");
}

internal static class SlotConverter
{
    public static void Write(Utf8JsonWriter writer, Inventory value, JsonSerializerOptions options)
    {
        var output = new Dictionary<byte, InventorySlot>();
        for (byte i = 1; i <= value.Size; i++)
        {
            if (value[i] == null) continue;
            output[i] = new InventorySlot
            {
                Count = value[i].Count,
                Id = value[i].TemplateId,
                Name = value[i].Name,
                Durability = value[i].Durability,
                Guid = value[i].Guid.ToString()
            };
        }

        JsonSerializer.Serialize(writer, output, options);
    }

    public static T Read<T>(ref Utf8JsonReader reader, T target, JsonSerializerOptions options, string context)
        where T : Inventory
    {
        var slots = JsonSerializer.Deserialize<Dictionary<byte, InventorySlot>>(ref reader, options);
        for (byte i = 1; i <= target.Size; i++)
            if (slots != null && slots.TryGetValue(i, out var slot))
            {
                if (Game.World.WorldData.TryGetValue(slot.Id, out Item _))
                    target[i] = new ItemObject(slot.Id, Game.GetDefaultServerGuid<World>(), new Guid(slot.Guid))
                    {
                        Count = slot.Count,
                        Durability = slot.Durability
                    };
                else
                {
                    GameLog.Error("{Context} deserializer error: item {ItemId} not found in index, skipping",
                        context, slot.Id);
                    target[i] = null;
                }
            }
            else
            {
                target[i] = null;
            }

        return target;
    }
}

public class SkillBookJsonConverter : JsonConverter<SkillBook>
{
    public override void Write(Utf8JsonWriter writer, SkillBook value, JsonSerializerOptions options) =>
        BookSlotConverter.Write(writer, value);

    public override SkillBook Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        BookSlotConverter.Read(ref reader, new SkillBook());
}

public class SpellBookJsonConverter : JsonConverter<SpellBook>
{
    public override void Write(Utf8JsonWriter writer, SpellBook value, JsonSerializerOptions options) =>
        BookSlotConverter.Write(writer, value);

    public override SpellBook Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        BookSlotConverter.Read(ref reader, new SpellBook());
}

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
        byte i = 0;
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var slotIndex = i;
            i++;
            if (element.ValueKind != JsonValueKind.Object) continue;
            var name = element.GetProperty("Name").GetString();
            book[slotIndex] = new BookSlot
            {
                Castable = Game.World.WorldData.Values<Castable>()
                    .SingleOrDefault(predicate: x => x.Name.ToLower() == name)
            };
            var bookSlot = book[slotIndex];
            if (bookSlot == null) continue;
            bookSlot.UseCount = element.TryGetProperty("TotalUses", out var uses) ? uses.GetUInt32() : 0;
            bookSlot.MasteryLevel = element.TryGetProperty("MasteryLevel", out var mastery) ? mastery.GetByte() : (byte)0;
            bookSlot.LastCast = element.GetProperty("LastCast").GetDateTime();
        }

        return book;
    }
}
