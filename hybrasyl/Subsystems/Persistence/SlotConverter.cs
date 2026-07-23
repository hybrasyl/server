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

using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Players;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     Shared wire logic for the Inventory/Equipment slot-dictionary shape. The wire
///     shape is load-bearing contract; see the golden corpus.
/// </summary>
internal static class SlotConverter
{
    public static void Write(Utf8JsonWriter writer, Inventory value, JsonSerializerOptions options)
    {
        var output = new Dictionary<byte, InventorySlot>();
        for (byte i = 1; i <= value.Size; i++)
        {
            var item = value[i];
            if (item == null) continue;
            output[i] = new InventorySlot
            {
                Count = item.Count,
                Id = item.TemplateId,
                Name = item.Name,
                Durability = item.Durability,
                Guid = item.Guid.ToString()
            };
        }

        JsonSerializer.Serialize(writer, output, options);
    }

    public static T Read<T>(ref Utf8JsonReader reader, T target, JsonSerializerOptions options)
        where T : Inventory
    {
        var slots = JsonSerializer.Deserialize<Dictionary<byte, InventorySlot>>(ref reader, options);
        if (slots == null) return target;
        for (byte i = 1; i <= target.Size; i++)
            if (slots.TryGetValue(i, out var slot))
            {
                if (Game.World.WorldData.TryGetValue(slot.Id, out Item _))
                    target[i] = new ItemObject(slot.Id, Game.GetDefaultServerGuid<World>(), new Guid(slot.Guid))
                    {
                        Count = slot.Count,
                        Durability = slot.Durability
                    };
                else
                    GameLog.Error("{Type} deserializer error: item {ItemId} not found in index, skipping",
                        target.GetType().Name, slot.Id);
            }

        (target as IJsonOnDeserialized)?.OnDeserialized();
        return target;
    }
}
