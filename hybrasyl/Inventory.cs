/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Enums;
using Hybrasyl.Objects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Hybrasyl.ChatCommands;
using Hybrasyl.Threading;
using Serilog;
using Hybrasyl.Xml;

namespace Hybrasyl;

public class Exchange
{
    private Inventory _sourceItems;
    private Inventory _targetItems;
    private uint _sourceGold;
    private uint _targetGold;
    private int _sourceSize;
    private int _targetSize;
    private User _source;
    private User _target;
    private bool _active;
    private int _sourceWeight;
    private int _targetWeight;
    private bool _sourceConfirmed;
    private bool _targetConfirmed;

    public Exchange(User source, User target)
    {
        _source = source;
        _target = target;
        _sourceItems = new Inventory(60);
        _targetItems = new Inventory(60);
        _sourceGold = 0;
        _targetGold = 0;
        _sourceWeight = 0;
        _targetWeight = 0;
        _sourceSize = source.Inventory.EmptySlots;
        _targetSize = target.Inventory.EmptySlots;
    }

    public static bool StartConditionsValid(User source, User target, out string errorMessage)
    {
        errorMessage = string.Empty;
        var locationCheck = source.Map == target.Map && source.IsInViewport(target) &&
                            target.IsInViewport(source) && target.Distance(source) <= Constants.EXCHANGE_DISTANCE;

        var flagCheck = source.Condition.NoFlags && target.Condition.NoFlags;

        if (!locationCheck)
            errorMessage = "They are too far away.";

        if (!flagCheck)
            errorMessage = "That is not possible now.";

        if (!source.GetClientSetting("exchange"))
            errorMessage = "You have exchange turned off.";

        if (!target.GetClientSetting("exchange"))
            errorMessage = "They do not wish to trade with you.";

        return errorMessage == string.Empty;
    }

    public bool ConditionsValid =>
        _source.Map == _target.Map && _source.IsInViewport(_target) &&
        _target.IsInViewport(_source) &&
        _source.Condition.InExchange &&
        _target.Condition.InExchange &&
        _active;

    public bool AddItem(User giver, byte slot, byte quantity = 1)
    {
        ItemObject toAdd;
        // Some sanity checks

        // Check if our "exchange" is full
        if (_sourceItems.IsFull || _targetItems.IsFull)
        {
            _source.SendMessage("Maximum exchange size reached. No more items can be added.", MessageTypes.SYSTEM);
            _target.SendMessage("Maximum exchange size reached. No more items can be added.", MessageTypes.SYSTEM);
            return false;
        }
        // Check if either participant's inventory would be full as a result of confirmation
        if (_sourceItems.Count == _sourceSize || _targetItems.Count == _targetSize)
        {
            _source.SendMessage("Inventory full.", MessageTypes.SYSTEM);
            _target.SendMessage("Inventory full.", MessageTypes.SYSTEM); 
            return false;
        }

        // OK - we have room, now what?
        var theItem = giver.Inventory[slot];

        // Further checks!
        // Is the ItemObject exchangeable?

        if (!theItem.Exchangeable)
        {
            giver.SendMessage("You can't trade this.", MessageTypes.SYSTEM);
            return false;
        }

        // Weight check

        if (giver == _source && _targetWeight + theItem.Weight > _target.MaximumWeight)
        {
            _source.SendSystemMessage("It's too heavy.");
            _target.SendSystemMessage("They can't carry any more.");
            return false;
        }

        if (giver == _target && _sourceWeight + theItem.Weight > _source.MaximumWeight)
        {
            _target.SendSystemMessage("It's too heavy.");
            _source.SendSystemMessage("They can't carry any more.");
            return false;
        }

        // Is the ItemObject stackable?

        if (theItem.Stackable && theItem.Count > 1)
        {
            var targetItem = giver == _target ? _source.Inventory.FindById(theItem.Name) : _target.Inventory.FindById(theItem.Name);

            // Check to see that giver has sufficient number of whatever, and also that the quantity is a positive number
            if (quantity <= 0)
            {
                giver.SendSystemMessage("You can't give zero of something, chief.");
                return false;
            }

            if (quantity > theItem.Count)
            {
                giver.SendSystemMessage($"You don't have that many {theItem.Name} to give!");
                return false;
            }

            // Check if the recipient already has this ItemObject - if they do, ensure the quantity proposed for trade
            // wouldn't put them over maxcount for the ItemObject in question

            if (targetItem != null && targetItem.Count + quantity > theItem.MaximumStack)
            {
                if (giver == _target)
                {
                    _target.SendSystemMessage($"They can't carry any more {theItem.Name}");
                    _source.SendSystemMessage($"You can't carry any more {theItem.Name}.");
                }
                else
                {
                    _source.SendSystemMessage($"They can't carry any more {theItem.Name}");
                    _target.SendSystemMessage($"You can't carry any more {theItem.Name}.");
                }
                return false;
            }
            giver.RemoveItem(theItem.Name, quantity);
            toAdd = new ItemObject(theItem);
            toAdd.Count = quantity;
        }
        else if (!theItem.Stackable || theItem.Count == 1)
        {
            // ItemObject isn't stackable or is a stack of one
            // Remove the ItemObject entirely from giver
            toAdd = theItem;
            giver.RemoveItem(slot);
        }
        else
        {
            GameLog.WarningFormat("exchange: Hijinx occuring: participants are {0} and {1}",
                _source.Name, _target.Name);
            _active = false;
            return false;
        }

        // Now add the ItemObject to the active exchange and make sure we update weight
        if (giver == _source)
        {
            var exchangeSlot = (byte)_sourceItems.Count;
            _sourceItems.AddItem(toAdd);
            _source.SendExchangeUpdate(toAdd, exchangeSlot);
            _target.SendExchangeUpdate(toAdd, exchangeSlot, false);
            _targetWeight += toAdd.Weight;
        }
        if (giver == _target)
        {
            var exchangeSlot = (byte) _targetItems.Count;
            _targetItems.AddItem(toAdd);
            _target.SendExchangeUpdate(toAdd, exchangeSlot);
            _source.SendExchangeUpdate(toAdd, exchangeSlot, false);
            _sourceWeight += toAdd.Weight;

        }

        return true;
    }

    public bool AddGold(User giver, uint amount)
    {
        if (giver == _source)
        {
            if (amount > uint.MaxValue - _sourceGold)
            {
                _source.SendMessage("No more gold can be added to this exchange.", MessageTypes.SYSTEM);
                return false;
            }
            if (amount > _source.Gold)
            {
                _source.SendMessage("You don't have that much gold.", MessageTypes.SYSTEM);
                return false;
            }
            _sourceGold += amount;
            _source.SendExchangeUpdate(amount);
            _target.SendExchangeUpdate(amount, false);
            _source.Gold -= amount;
            _source.UpdateAttributes(StatUpdateFlags.Experience);

        }
        else if (giver == _target)
        {
            if (amount > uint.MaxValue - _targetGold)
            {
                _target.SendMessage("No more gold can be added to this exchange.", MessageTypes.SYSTEM);
                return false;
            }
            _targetGold += amount;
            _target.SendExchangeUpdate(amount);
            _source.SendExchangeUpdate(amount, false);
            _target.Gold -= amount;
            _target.UpdateAttributes(StatUpdateFlags.Experience);
        }
        else
            return false;

        return true;

    }

    public bool StartExchange()
    {
        GameLog.InfoFormat("Starting exchange between {0} and {1}", _source.Name, _target.Name);
        _active = true;
        _source.Condition.InExchange = true;
        _target.Condition.InExchange = true;
        _source.ActiveExchange = this;
        _target.ActiveExchange = this;
        // Send "open window" packet to both clients
        _target.SendExchangeInitiation(_source);
        _source.SendExchangeInitiation(_target);
        return true;
    }

    /// <summary>
    /// Cancel the exchange, returning all items from the window back to each player.
    /// </summary>
    /// <returns>Boolean indicating success. Better hope this is always true.</returns>
    public bool CancelExchange(User requestor)
    {
        foreach (var item in _sourceItems)
        {
            _source.AddItem(item);
        }
        foreach (var item in _targetItems)
        {
            _target.AddItem(item);
        }
        _source.AddGold(_sourceGold);
        _target.AddGold(_targetGold);
        _source.SendExchangeCancellation(requestor == _source);
        _target.SendExchangeCancellation(requestor == _target);
        _source.ActiveExchange = null;
        _target.ActiveExchange = null;
        _source.Condition.InExchange = false;
        _target.Condition.InExchange = false;
        return true;
    }

    /// <summary>
    /// Perform the exchange once confirmation from both sides is received.
    /// </summary>
    /// <returns></returns>
    public void PerformExchange()
    {
        GameLog.Info("Performing exchange");
        foreach (var item in _sourceItems)
        {
            _target.AddItem(item);
        }
        foreach (var item in _targetItems)
        {
            _source.AddItem(item);
        }
        _source.AddGold(_targetGold);
        _target.AddGold(_sourceGold);

        _source.ActiveExchange = null;
        _target.ActiveExchange = null;
        _source.Condition.InExchange = false;
        _target.Condition.InExchange = false;
    }

    /// <summary>
    /// Confirm the exchange. Once both sides confirm, perform the exchange.
    /// </summary>
    /// <returns>Boolean indicating success.</returns>
    public void ConfirmExchange(User requestor)
    {
        if (_source == requestor)
        {
            GameLog.InfoFormat("Exchange: source ({0}) confirmed", _source.Name);
            _sourceConfirmed = true;
            _target.SendExchangeConfirmation(false);
        }
        if (_target == requestor)
        {
            GameLog.InfoFormat("Exchange: target ({0}) confirmed", _target.Name);
            _targetConfirmed = true;
            _source.SendExchangeConfirmation(false);
        }
        if (_sourceConfirmed && _targetConfirmed)
        {
            GameLog.Info("Exchange: Both sides confirmed");
            _source.SendExchangeConfirmation();
            _target.SendExchangeConfirmation();
            PerformExchange();
        }
    }
}

[JsonObject(MemberSerialization.OptIn)]
[RedisType]
public class Vault
{
    [JsonProperty]
    public Guid OwnerGuid { get; set; }
    [JsonProperty]
    public uint GoldLimit { get; private set; }
    [JsonProperty]
    public uint CurrentGold { get; private set; }
    [JsonProperty]
    public ushort ItemLimit { get; private set; }
    [JsonProperty]
    public ushort CurrentItemCount => (ushort)Items.Count;
    public bool CanDepositGold => CurrentGold != GoldLimit;
    public uint RemainingGold => GoldLimit - CurrentGold;
    public ushort RemainingItems => (ushort)(ItemLimit - CurrentItemCount);
    public bool IsSaving;

    public string StorageKey => string.Concat(GetType(), ':', OwnerGuid);

    [JsonProperty]
    public Dictionary<string, uint> Items { get; private set; } //item name, quantity

    public Vault() { }

    public Vault(Guid ownerGuid)
    {
        GoldLimit = uint.MaxValue;
        ItemLimit = ushort.MaxValue;
        CurrentGold = 0;
        Items = new Dictionary<string, uint>();
        OwnerGuid = ownerGuid;
    }

    public Vault(Guid ownerGuid, uint goldLimit, ushort itemLimit)
    {
        GoldLimit = goldLimit;
        ItemLimit = itemLimit;
        Items = new Dictionary<string, uint>();
        OwnerGuid = ownerGuid;
    }
        
    public bool AddGold(uint gold)
    {
        if (gold <= RemainingGold)
        {
            CurrentGold += gold;

            GameLog.Info($"{gold} gold added to vault {OwnerGuid}");
            return true;
        }
        else
        {
            GameLog.Info($"Attempt to add {gold} gold to vault {OwnerGuid}, but only {RemainingGold} available");
            return false;
        }
    }

    public bool RemoveGold(uint gold)
    {
        if (gold <= CurrentGold)
        {
            CurrentGold -= gold;
            GameLog.Info($"{gold} gold removed from vault {OwnerGuid}");
            return true;
        }
        else
        {
            GameLog.Info($"Attempt to remove {gold} gold from vault {OwnerGuid}, but only {CurrentGold} available");
            return false;
        }
    }

    public bool AddItem(string itemName, ushort quantity = 1)
    {
        if(CurrentItemCount < ItemLimit)
        {
            if(Items.ContainsKey(itemName))
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
        else
        {
            GameLog.Info($"Attempt to add {itemName} [{quantity}] to vault {OwnerGuid}, but user doesn't have it?");
            return false;
        }
    }

    public bool RemoveItem(string itemName, ushort quantity = 1)
    {
        if(Items.ContainsKey(itemName))
        {
            if(Items[itemName] > quantity)
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
        else
        {
            return false;
        }
    }

    public void Save()
    {
        if (IsSaving) return;
        IsSaving = true;
        var cache = World.DatastoreConnection.GetDatabase();
        cache.Set(StorageKey, this);
        Game.World.WorldData.Set<Vault>(OwnerGuid, this);
        IsSaving = false;
    }
}


[JsonObject(MemberSerialization.OptIn)]
[RedisType]
public class GuildVault : Vault
{
    //strings are guid identifiers
    [JsonProperty]
    public Guid GuildMasterGuid { get; private set; } //no restrictions
    [JsonProperty]
    public List<Guid> AuthorizedViewerGuids { get; private set; } //authorized to see what is stored, but cannot withdraw
    [JsonProperty]
    public List<Guid> AuthorizedWithdrawalGuids { get; private set; } //authorized to withdraw,  up to limit
    [JsonProperty]
    public List<Guid> CouncilMemberGuids { get; private set; } //possible restrictions?
    [JsonProperty]
    public int AuthorizedWithdrawalLimit { get;  private set; }
    [JsonProperty]
    public int CouncilMemberLimit { get; private set; }

    public GuildVault() : base()
    { }
    public GuildVault(Guid ownerGuid) : base(ownerGuid)
    { }

    public GuildVault(Guid ownerGuid, uint goldLimit, ushort itemLimit) : base(ownerGuid, goldLimit, itemLimit) { }
}

public class Parcel
{
    public string Sender { get; set; }
    public string Item { get; set; }
    public uint Quantity { get; set; }

    public Parcel() { }

    public Parcel(string sender, string item, uint quantity)
    {
        Sender = sender;
        Item = item;
        Quantity = quantity;
    }
}

public class Moneygram
{
    public string Sender { get; set; }
    public uint Amount { get; set; }

    public Moneygram() { }
    public Moneygram(string sender, uint amount)
    {
        Sender = sender;
        Amount = amount;
    }
}

[JsonObject(MemberSerialization.OptIn)]
[RedisType]
public class ParcelStore
{
    private readonly object _lock = new object();

    [JsonProperty] public Guid OwnerGuid { get; set; }
    [JsonProperty] public List<Parcel> Items { get; set; } //storage id, named tuple
    [JsonProperty] public List<Moneygram> Gold { get; set; } //storage id, named tuple

    public bool IsSaving;

    public string StorageKey => string.Concat(GetType(), ':', OwnerGuid);

    public ParcelStore()
    {
    }

    public ParcelStore(Guid ownerGuid)
    {
        Items = new List<Parcel>();
        Gold = new List<Moneygram>();
        OwnerGuid = ownerGuid;
    }

    public void Save()
    {
        if (IsSaving) return;
        lock (_lock)
        {
            IsSaving = true;
            var cache = World.DatastoreConnection.GetDatabase();
            cache.Set(StorageKey, this);
            Game.World.WorldData.Set(OwnerGuid, this);
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
            if (receiver.AddItem(parcel.Item, (ushort) parcel.Quantity))
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

public class InventorySlot
{
    public string Name { get; set; }
    public int Count { get; set; }
    public string Id { get; set; }
    public double Durability { get; set; }
    public string Guid { get; set; }
}

public class EquipmentConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var equip = (Equipment) value;
        var output = new Dictionary<byte, object>();
        for (byte i = 1; i <= equip.Size; i++)
        {
            if (equip[i] == null) continue;
            var slot = new InventorySlot
            {
                Count = equip[i].Count, Id = equip[i].TemplateId, Name = equip[i].Name,
                Durability = equip[i].Durability, Guid = equip[i].Guid.ToString()
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
        {
            if (equipJObj.TryGetValue(i, out var slot))
            {
                if (Game.World.WorldData.TryGetValue<Item>(slot.Id, out Item ItemTemplate))
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
                equipment[i] = null;
        }

        return equipment;
    }

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Equipment);
    }

}

public class InventoryConverter : JsonConverter
{

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var equip = (Inventory) value;
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
        {
            if (equipJObj.TryGetValue(i, out var slot))
            {
                if (Game.World.WorldData.TryGetValue<Item>(slot.Id, out Item ItemTemplate))
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
                equipment[i] = null;
        }

        return equipment;
    }


    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Inventory);
    }

    public bool TryGetValue(JToken token, out dynamic item)
    {
        item = null;
        if (!token.HasValues) return false;

        item = token.ToObject<dynamic>();
        return true;
    }
}

[JsonConverter(typeof(InventoryConverter))]
public class Inventory : IEnumerable<ItemObject>
{
    public const byte DefaultSize = 59;

    protected readonly object ContainerLock = new();

    protected Dictionary<byte, ItemObject> Items = new();
    protected Dictionary<string, List<(byte Slot, ItemObject Item)>> ItemIndex = new();
    protected Dictionary<string, List<(byte Slot, ItemObject Item)>> CategoryIndex = new();

    protected Lockable<int> _size { get; }
    protected Lockable<int> _count { get; } = new(0);
    protected Lockable<int> _weight { get; } = new(0);

    public Inventory(byte size)
    {
        for (byte x = 1; x <= size; x++)
        {
            Items[x] = null;
        }

        _size = new Lockable<int>(size);
    }

    public int Size => _size.Value;

    public int Count
    {
        get => _count.Value;
        private set => _count.Value = value;
    }

    public int Weight
    {
        get => _weight.Value;
        private set => _weight.Value = value;
    }

    public virtual bool IsFull => Count == Size;

    public int EmptySlots => Size - Count;

    public virtual void RecalculateWeight()
    {
        var newWeight = this.Sum(obj => obj.Weight);
        Weight = newWeight;
    }

    public ItemObject this[byte slot]
    {
        get
        {
            if (slot < 1 || slot > Size)
            {
                throw new ArgumentException("Inventory slot does not exist");
            }
            return Items[slot];
        }
        internal set
        {
            lock (ContainerLock)
            {
                if (slot > Size)
                    throw new ArgumentException("Requested slot is greater than inventory size");
                if (value == null && Items[slot] != null)
                    _removeFromIndexes(slot, Items[slot]);
                else if (value != null)
                    _addToIndexes(slot, value);
                Items[slot] = value;
            }
        }   
    }

    private void _addToIndexes(byte slot, ItemObject obj)
    {
        lock (ContainerLock)
        {
            var index = (Slot: slot, Item: obj);

            if (ItemIndex.TryGetValue(obj.TemplateId, out var itemList))
            {
                itemList.Add(index);
            }
            else
            {
                ItemIndex.Add(obj.TemplateId,
                    new List<(byte Slot, ItemObject Item)> { index });
            }
            // Index by item categories

            foreach (var category in obj.Categories.Select(x => x.ToLower()))
            {
                if (CategoryIndex.TryGetValue(category, out var categoryList))
                {
                    categoryList.Add(index);
                }
                else
                {
                    CategoryIndex.Add(category, new List<(byte Slot, ItemObject Item)> {index});
                }
            }
        }
    }

    private void _removeFromIndexes(byte slot, ItemObject obj)
    {
        lock (ContainerLock)
        {
            if (!ItemIndex.TryGetValue(obj.TemplateId, out var itemList)) return;
            itemList.Remove((slot, obj));
            if (itemList.Count == 0)
                ItemIndex.Remove(obj.TemplateId);
            foreach (var category in obj.Categories)
            {
                CategoryIndex[category].RemoveAll(x => x.Slot == slot);
            }
        }
    }

    public bool TryGetValueByName(string name, out List<(byte slot, ItemObject obj)> itemList)
    {
        itemList = new List<(byte slot, ItemObject obj)>();

        var potentialIds = Item.GenerateIds(name);

        foreach (var id in potentialIds)
        {
            if (ItemIndex.TryGetValue(id, out var foundItems))
            {
                itemList.AddRange(foundItems);
            }
        }

        return itemList.Count != 0; 
    }

    public bool TryRemoveQuantity(string id, out List<(byte Slot, int Quantity)> affectedSlots, int quantity=1)
    {
        var removed = 0;
        affectedSlots = new List<(byte Slot, int Quantity)>();
        if (quantity < 1 || !ContainsId(id, quantity)) return false;
        var need = quantity;
        lock (ContainerLock)
        {
            foreach (var (slot, item) in ItemIndex[id].ToList())
            {
                if (need == 0) break;
                if (item.Count <= need)
                {
                    var count = item.Count;
                    need -= count;
                    removed += count;
                    affectedSlots.Add((slot, item.Count));
                    Remove(slot);
                }
                else
                {
                    Items[slot].Count -= need;
                    removed += need;
                    affectedSlots.Add((slot, need));
                    return true;
                }
            }
        }

        return removed == quantity;
    }
        
    public List<byte> GetSlotsByName(string name)
    {
        var ret = new List<byte>();
        foreach (var id in Item.GenerateIds(name))
        {
            ret.AddRange(GetSlotsById(id));
        }

        return ret;
    }

    public List<byte> GetSlotsById(string id)
    {
        var ret = new List<byte>();
        if (ItemIndex.ContainsKey(id))
            ret.AddRange(ItemIndex[id].Select(x => x.Slot));
        return ret;
    }

    public bool TryGetValue(string templateId, out ItemObject itemObject)
    {
        itemObject = null;
        if (!ItemIndex.TryGetValue(templateId, out var itemList)) return false;
        itemObject = itemList.First().Item;
        return true;
    }

    public bool ContainsName(string name, int quantity = 1) => Item.GenerateIds(name).Any(x => ContainsId(x, quantity));

    public bool ContainsId(string id, int quantity = 1) => ItemIndex.ContainsKey(id) && ItemIndex[id].Sum(x => x.Item.Count) >= quantity;

    public byte FindEmptySlot() => Items.First(x => x.Value == null).Key;

    public byte SlotOfId(string id) => ItemIndex.ContainsKey(id) ? ItemIndex[id].First().Slot : byte.MinValue;

    public byte SlotOfName(string name) => (from id in Item.GenerateIds(name) where ItemIndex.ContainsKey(id) select ItemIndex[id].First().Slot).FirstOrDefault();

    public List<byte> GetSlotsByCategory(params string[] categories)
    {
        var lower = categories.Select(x => x.ToLower()).ToList();
        var ret = new List<byte>();
        foreach (var kvp in CategoryIndex.Where(kvp => lower.Contains(kvp.Key)))
        {
            ret.AddRange(kvp.Value.Select(x => x.Slot));
        }

        return ret;
    }

    public ItemObject FindById(string id) => ItemIndex.ContainsKey(id) ? ItemIndex[id].First().Item : null;

    public ItemObject FindByName(string name) => (from id in Item.GenerateIds(name) where ItemIndex.ContainsKey(id) select ItemIndex[id].First().Item).FirstOrDefault();

    public bool AddItem(ItemObject itemObject)
    {
        if (IsFull) return false;
        var slot = FindEmptySlot();
        return Insert(slot, itemObject);
    }

    public bool Insert(byte slot, ItemObject itemObject)
    {
        if (slot == 0 || slot > Size || Items[slot] != null)
            return false;
        lock (ContainerLock)
        {
            Items[slot] = itemObject;
            Count += 1;
            Weight += itemObject.Weight;
            _addToIndexes(slot, itemObject);
        }

        return true;
    }

    public bool Remove(byte slot)
    {
        if (slot == 0 || slot > Size || Items[slot] == null)
            return false;
        lock (ContainerLock)
        {
            var item = Items[slot];
            Items[slot] = null;
            Count--;
            Weight -= item.Weight;
            _removeFromIndexes(slot, item);
        }

        return true;
    }

    public bool Swap(byte slot1, byte slot2)
    {
        lock (ContainerLock)
        {
            if (slot1 == 0 || slot1 > Size || slot2 == 0 || slot2 > Size)
                return false;
            if (Items[slot1] != null) _removeFromIndexes(slot1, Items[slot1]);
            if (Items[slot2] != null) _removeFromIndexes(slot2, Items[slot2]);
            (Items[slot1], Items[slot2]) = (Items[slot2], Items[slot1]);
            if (Items[slot1] != null) _addToIndexes(slot1, Items[slot1]);
            if (Items[slot2] != null) _addToIndexes(slot2, Items[slot2]);
            return true;
        }
    }

    public void Clear()
    {
        lock (ContainerLock)
        {
            Items.Clear();
            ItemIndex.Clear();
            CategoryIndex.Clear();
            for (byte x = 1; x <= Size; x++)
            {
                Items[x] = null;
            }
            Count = 0;
            Weight = 0;
        }
    }

    public bool Increase(byte slot, int amount)
    {
        if (slot == 0 || slot > Size || Items[slot] == null)
            return false;

        if (Items[slot].Count + amount > Items[slot].MaximumStack)
            return false;

        lock (ContainerLock)
        {
            Items[slot].Count += amount;
        }
        return true;
    }

    public bool Decrease(byte slot, int amount)
    {
        if (slot == 0 || slot > Size || Items[slot] == null || Items[slot].Count < amount)  
            return false;
        return Items[slot].Count > 0 || Remove(slot);
    }

    public IEnumerator<ItemObject> GetEnumerator() => Items.Values.Where(x => x is not null).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString() => Items.Where(x => x.Value != null).Aggregate(string.Empty, (current, item) 
        => $"{current}\nslot {item.Key}: {item.Value.Name}, qty {item.Value.Count}");
}

[JsonConverter(typeof(EquipmentConverter))]
public class Equipment : Inventory
{
    public new const byte DefaultSize = 18;

    #region Equipment Properties

    public ItemObject Weapon => Items[(byte) ItemSlots.Weapon];

    public ItemObject Armor => Items[(byte) ItemSlots.Armor];

    public ItemObject Shield => Items[(byte) ItemSlots.Shield];

    public ItemObject Helmet => Items[(byte) ItemSlots.Helmet];

    public ItemObject Earring => Items[(byte) ItemSlots.Earring];

    public ItemObject Necklace => Items[(byte) ItemSlots.Necklace];

    public ItemObject LRing => Items[(byte) ItemSlots.LHand];

    public ItemObject RRing => Items[(byte) ItemSlots.RHand];

    public ItemObject LGauntlet => Items[(byte) ItemSlots.LArm];

    public ItemObject RGauntlet => Items[(byte) ItemSlots.RArm];

    public ItemObject Belt => Items[(byte) ItemSlots.Waist];

    public ItemObject Greaves => Items[(byte) ItemSlots.Leg];

    public ItemObject Boots => Items[(byte) ItemSlots.Foot];

    public ItemObject FirstAcc => Items[(byte) ItemSlots.FirstAcc];

    public ItemObject Overcoat => Items[(byte) ItemSlots.Trousers];

    public ItemObject DisplayHelm => Items[(byte) ItemSlots.Coat];

    public ItemObject SecondAcc => Items[(byte) ItemSlots.SecondAcc];

    public ItemObject ThirdAcc => Items[(byte) ItemSlots.ThirdAcc];

    #endregion Equipment Properties

    public Equipment(byte size) : base(size) {}

    public List<Tuple<ushort, byte>> GetEquipmentDisplayList()
    {
        var returnList = new List<Tuple<ushort, byte>>();

        foreach (var slot in Enum.GetValues(typeof(ItemSlots)))
        {
            switch (slot)
            {
                // Work around a very weird edge case in the client
                case ItemSlots.Foot:
                    returnList.Add(Items[(byte) ItemSlots.FirstAcc] == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort)(0x8000 + Items[(byte) ItemSlots.FirstAcc].EquipSprite), Items[
                            (byte) ItemSlots.FirstAcc].Color));
                    break;
                case ItemSlots.FirstAcc:
                    returnList.Add(Items[(byte) ItemSlots.Foot] == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort)(0x8000 + Items[(byte) ItemSlots.Foot].EquipSprite), Items[
                            (byte)ItemSlots.Foot].Color));
                    break;
                case ItemSlots.None:
                case ItemSlots.Ring:
                case ItemSlots.Gauntlet:
                    break;
                default:
                    returnList.Add(Items[(byte) slot] == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort)(0x8000 + Items[(byte) slot].EquipSprite), Items[(byte) slot].Color));
                    break;
            }
        }

        return returnList;
    }

}