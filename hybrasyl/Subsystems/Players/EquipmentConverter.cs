using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Xml.Objects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Subsystems.Players;

public class EquipmentConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var equip = (Equipment)value;
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
        var equipment = new Equipment(Equipment.DefaultSize);

        for (byte i = 1; i <= Equipment.DefaultSize; i++)
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

    public override bool CanConvert(Type objectType) => objectType == typeof(Equipment);
}