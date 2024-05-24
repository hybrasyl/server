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

using System;
using System.Linq;
using Hybrasyl.Subsystems.Players;
using Hybrasyl.Xml.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hybrasyl.Casting;

public class BookConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var book = (Book) value;
        var output = new object[book.Size];
        for (byte i = 0; i < book.Size; i++)
        {
            dynamic itemInfo = new JObject();
            if (book[i] == null || book[i].Castable == null) continue;
            itemInfo.Name = book[i].Castable.Name.ToLower();
            itemInfo.LastCast = book[i].LastCast;
            itemInfo.TotalUses = book[i].UseCount;
            itemInfo.MasteryLevel = book[i].MasteryLevel;
            output[i] = itemInfo;
        }

        var ja = JArray.FromObject(output);
        serializer.Serialize(writer, ja);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var jArray = JArray.Load(reader);
        if (objectType.Name == "SkillBook")
        {
            var book = new SkillBook();

            for (byte i = 0; i < jArray.Count; i++)
            {
                if (!TryGetValue(jArray[i], out var item)) continue;
                book[i] = new BookSlot
                {
                    Castable = Game.World.WorldData.Values<Castable>()
                        .SingleOrDefault(predicate: x => x.Name.ToLower() == (string) item.Name)
                };
                var bookSlot = book[i];
                if (bookSlot == null) continue;
                bookSlot.UseCount = (uint) (item.TotalUses ?? 0);
                bookSlot.MasteryLevel = (byte) (item.MasteryLevel == null ? (byte) 0 : item.MasteryLevel);
                bookSlot.LastCast = (DateTime) item.LastCast;
            }

            return book;
        }
        else
        {
            var book = new SpellBook();

            for (byte i = 0; i < jArray.Count; i++)
            {
                dynamic item;
                if (!TryGetValue(jArray[i], out item)) continue;
                book[i] = new BookSlot
                {
                    Castable = Game.World.WorldData.Values<Castable>()
                        .SingleOrDefault(predicate: x => x.Name.ToLower() == (string) item.Name)
                };
                var castable = book[i];
                var bookSlot = book[i];
                if (bookSlot == null) continue;
                bookSlot.UseCount = (uint) (item.TotalUses ?? 0);
                bookSlot.MasteryLevel = (byte) (item.MasteryLevel == null ? (byte) 0 : item.MasteryLevel);
                bookSlot.LastCast = (DateTime) item.LastCast;
            }

            return book;
        }
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