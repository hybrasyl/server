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

            GameLog.Info($"{gold} gold added to vault {OwnerGuid}");
            return true;
        }

        GameLog.Info($"Attempt to add {gold} gold to vault {OwnerGuid}, but only {RemainingGold} available");
        return false;
    }

    public bool RemoveGold(uint gold)
    {
        if (gold <= CurrentGold)
        {
            CurrentGold -= gold;
            GameLog.Info($"{gold} gold removed from vault {OwnerGuid}");
            return true;
        }

        GameLog.Info($"Attempt to remove {gold} gold from vault {OwnerGuid}, but only {CurrentGold} available");
        return false;
    }

    public bool AddItem(string itemName, ushort quantity = 1)
    {
        if (CurrentItemCount < ItemLimit)
        {
            if (Items.ContainsKey(itemName))
            {
                Items[itemName] += quantity;
                GameLog.Info($"{itemName} [{quantity}] added to existing item in vault {OwnerGuid}");
            }
            else
            {
                Items.Add(itemName, quantity);
                GameLog.Info($"{itemName} [{quantity}] added as new item in vault {OwnerGuid}");
            }

            return true;
        }

        GameLog.Info($"Attempt to add {itemName} [{quantity}] to vault {OwnerGuid}, but user doesn't have it?");
        return false;
    }

    public bool RemoveItem(string itemName, ushort quantity = 1)
    {
        if (Items.ContainsKey(itemName))
        {
            if (Items[itemName] > quantity)
            {
                Items[itemName] -= quantity;
                GameLog.Info($"{itemName} [{quantity}] removed from existing item in vault {OwnerGuid}");
            }
            else
            {
                Items.Remove(itemName);
                GameLog.Info($"{itemName} removed from vault {OwnerGuid}");
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