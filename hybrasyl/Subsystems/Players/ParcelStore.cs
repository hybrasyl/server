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

using Hybrasyl.Extensions;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Attributes;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Subsystems.Players;

[JsonObject(MemberSerialization.OptIn)]
[RedisType]
public class ParcelStore : IStateStorable
{
    private readonly object _lock = new();

    public bool IsSaving;

    public ParcelStore() { }

    public ParcelStore(Guid ownerGuid)
    {
        Items = new List<Parcel>();
        Gold = new List<Moneygram>();
        OwnerGuid = ownerGuid;
    }

    [JsonProperty] public Guid OwnerGuid { get; set; }
    [JsonProperty] public List<Parcel> Items { get; set; } //storage id, named tuple
    [JsonProperty] public List<Moneygram> Gold { get; set; } //storage id, named tuple

    public string StorageKey => string.Concat(GetType(), ':', OwnerGuid);

    public void Save()
    {
        if (IsSaving) return;
        lock (_lock)
        {
            IsSaving = true;
            var cache = World.DatastoreConnection.GetDatabase();
            cache.Set(StorageKey, this);
            Game.World.WorldState.Set(OwnerGuid, this);
            IsSaving = false;
        }
    }

    public void AddItem(string sender, string item, uint quantity = 1)
    {
        lock (_lock)
        {
            Items.Add(new Parcel(sender, item, quantity));
        }

        Save();
    }

    public void RemoveItem(User receiver)
    {
        lock (_lock)
        {
            if (Items.Count == 0) return;
            var parcel = Items.First();
            if (receiver.AddItem(parcel.Item, (ushort)parcel.Quantity))
            {
                receiver.SendSystemMessage($"Your package from {parcel.Sender} has been delivered.");
                Items.RemoveAt(0);
            }
            else
            {
                receiver.SendSystemMessage($"Sorry, you can't receive the package from {parcel.Sender} right now.");
            }
        }

        Save();
    }

    public void AddGold(string sender, uint quantity)
    {
        lock (_lock)
        {
            Gold.Add(new Moneygram(sender, quantity));
        }

        Save();
    }

    public void RemoveGold(User receiver)
    {
        lock (_lock)
        {
            var gold = Gold.FirstOrDefault();

            if (receiver.AddGold(gold.Amount))
            {
                receiver.SendSystemMessage($"Your gold from {gold.Sender} has been delivered.");
                Items.RemoveAt(0);
            }
        }

        Save();
    }
}