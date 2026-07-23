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
using Hybrasyl.Internals.Logging;
using Hybrasyl.Servers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Subsystems.Players;

[JsonObject(MemberSerialization.OptIn)]
[RedisType]
public class Vault : IStateStorable
{
    public bool IsSaving;

    public Vault() { }

    public Vault(Guid ownerGuid)
    {
        GoldLimit = uint.MaxValue;
        ItemLimit = ushort.MaxValue;
        CurrentGold = 0;
        Items = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        OwnerGuid = ownerGuid;
    }

    public Vault(Guid ownerGuid, uint goldLimit, ushort itemLimit)
    {
        GoldLimit = goldLimit;
        ItemLimit = itemLimit;
        Items = new Dictionary<string, uint>();
        OwnerGuid = ownerGuid;
    }

    [JsonProperty] public Guid OwnerGuid { get; set; }

    [JsonProperty] public uint GoldLimit { get; private set; }

    [JsonProperty] public uint CurrentGold { get; private set; }

    [JsonProperty] public ushort ItemLimit { get; private set; }

    [JsonProperty] public ushort CurrentItemCount => (ushort)Items.Count;

    public bool CanDepositGold => CurrentGold != GoldLimit;
    public uint RemainingGold => GoldLimit - CurrentGold;
    public ushort RemainingItems => (ushort)(ItemLimit - CurrentItemCount);
    public bool IsFull => CurrentItemCount == ItemLimit;

    public string StorageKey => string.Concat(GetType(), ':', OwnerGuid);

    [JsonProperty] public Dictionary<string, uint> Items { get; private set; } //item name, quantity

    public void Clear()
    {
        CurrentGold = 0;
        Items = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
    }

    public bool AddGold(uint gold)
    {
        if (gold <= RemainingGold)
        {
            CurrentGold += gold;

            GameLog.Info("{Gold} gold added to vault {OwnerGuid}", gold, OwnerGuid);
            return true;
        }

        GameLog.Info("Attempt to add {Gold} gold to vault {OwnerGuid}, but only {RemainingGold} available", gold,
            OwnerGuid, RemainingGold);
        return false;
    }

    public bool RemoveGold(uint gold)
    {
        if (gold <= CurrentGold)
        {
            CurrentGold -= gold;
            GameLog.Info("{Gold} gold removed from vault {OwnerGuid}", gold, OwnerGuid);
            return true;
        }

        GameLog.Info("Attempt to remove {Gold} gold from vault {OwnerGuid}, but only {CurrentGold} available", gold,
            OwnerGuid, CurrentGold);
        return false;
    }

    public bool AddItem(string itemName, ushort quantity = 1)
    {
        if (CurrentItemCount < ItemLimit)
        {
            if (Items.ContainsKey(itemName))
            {
                Items[itemName] += quantity;
                GameLog.Info("{Item} [{Quantity}] added to existing item in vault {OwnerGuid}", itemName, quantity,
                    OwnerGuid);
            }
            else
            {
                Items.Add(itemName, quantity);
                GameLog.Info("{Item} [{Quantity}] added as new item in vault {OwnerGuid}", itemName, quantity,
                    OwnerGuid);
            }

            return true;
        }

        GameLog.Info("Attempt to add {Item} [{Quantity}] to vault {OwnerGuid}, but user doesn't have it?", itemName,
            quantity, OwnerGuid);
        return false;
    }

    public bool RemoveItem(string itemName, ushort quantity = 1)
    {
        if (Items.ContainsKey(itemName))
        {
            if (Items[itemName] > quantity)
            {
                Items[itemName] -= quantity;
                GameLog.Info("{Item} [{Quantity}] removed from existing item in vault {OwnerGuid}", itemName, quantity,
                    OwnerGuid);
            }
            else
            {
                Items.Remove(itemName);
                GameLog.Info("{Item} removed from vault {OwnerGuid}", itemName, OwnerGuid);
            }

            return true;
        }

        return false;
    }

    public void Save()
    {
        if (IsSaving) return;
        IsSaving = true;
        var cache = World.DatastoreConnection.GetDatabase();
        cache.Set(StorageKey, this);
        Game.World.WorldState.Set(OwnerGuid, this);
        IsSaving = false;
    }
}