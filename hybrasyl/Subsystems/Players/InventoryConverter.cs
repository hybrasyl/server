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
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Xml.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Subsystems.Players;

public class InventoryConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var equip = (Inventory)value;
        var output = new Dictionary<byte, object>();
        for (byte i = 1; i <= equip.Size; i++)
        {
            if (equip[i] == null) continue;
            var slot = new InventorySlot
            {
                Count = equip[i].Count,
                Id = equip[i].TemplateId,
                Name = equip[i].Name,
                Durability = equip[i].Durability,
                Guid = equip[i].Guid.ToString()
            };
            output[i] = slot;
        }

        serializer.Serialize(writer, output);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var equipJObj = serializer.Deserialize<Dictionary<byte, InventorySlot>>(reader);
        var equipment = new Inventory(Inventory.DefaultSize);

        for (byte i = 1; i <= equipment.Size; i++)
            if (equipJObj.TryGetValue(i, out var slot))
            {
                if (Game.World.WorldData.TryGetValue(slot.Id, out Item ItemTemplate))
                {
                    equipment[i] = new ItemObject(slot.Id, Game.GetDefaultServerGuid<World>(), new Guid(slot.Guid))
                    {
                        Count = slot.Count,
                        Durability = slot.Durability
                    };
                }
                else
                {
                    GameLog.Error($"Inventory deserializer error: item {slot.Id} not found in index, skipping");
                    equipment[i] = null;
                }
            }
            else
            {
                equipment[i] = null;
            }

        return equipment;
    }


    public override bool CanConvert(Type objectType) => objectType == typeof(Inventory);

    public bool TryGetValue(JToken token, out dynamic item)
    {
        item = null;
        if (!token.HasValues) return false;

        item = token.ToObject<dynamic>();
        return true;
    }
}