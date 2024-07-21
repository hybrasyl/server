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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Internals;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using Newtonsoft.Json;

namespace Hybrasyl.Subsystems.Players;

[JsonConverter(typeof(InventoryConverter))]
public class Inventory : IEnumerable<ItemObject>
{
    public const byte DefaultSize = 59;

    protected readonly object ContainerLock = new();
    protected Dictionary<string, List<(byte Slot, ItemObject Item)>> CategoryIndex = new();
    protected Dictionary<string, List<(byte Slot, ItemObject Item)>> ItemIndex = new();

    protected Dictionary<byte, ItemObject> Items = new();

    public Inventory(byte size)
    {
        for (byte x = 1; x <= size; x++) Items[x] = null;

        _size = new Lockable<int>(size);
    }

    private HashSet<Guid> GuidIndex { get; } = new();

    protected Lockable<int> _size { get; }
    protected Lockable<int> _count { get; } = new(0);
    protected Lockable<int> _weight { get; } = new(0);

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

    public virtual bool IsEmpty => Count == 0;

    public ItemObject this[byte slot]
    {
        get
        {
            if (slot < 1 || slot > Size) throw new ArgumentException("Inventory slot does not exist");
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

    public IEnumerator<ItemObject> GetEnumerator()
    {
        return Items.Values.Where(predicate: x => x is not null).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public virtual void RecalculateWeight()
    {
        var newWeight = this.Sum(selector: obj => obj.Weight);
        Weight = newWeight;
    }

    private void _addToIndexes(byte slot, ItemObject obj)
    {
        lock (ContainerLock)
        {
            var index = (Slot: slot, Item: obj);

            if (ItemIndex.TryGetValue(obj.TemplateId, out var itemList))
                itemList.Add(index);
            else
                ItemIndex.Add(obj.TemplateId,
                    new List<(byte Slot, ItemObject Item)> { index });
            // Index by item categories

            foreach (var category in obj.Categories.Select(selector: x => x.ToLower()))
                if (CategoryIndex.TryGetValue(category, out var categoryList))
                    categoryList.Add(index);
                else
                    CategoryIndex.Add(category, new List<(byte Slot, ItemObject Item)> { index });
            GuidIndex.Add(obj.Guid);
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
            foreach (var category in obj.Categories) CategoryIndex[category].RemoveAll(match: x => x.Slot == slot);

            GuidIndex.Remove(obj.Guid);
        }
    }

    public bool TryGetValueByName(string name, out List<(byte slot, ItemObject obj)> itemList)
    {
        itemList = new List<(byte slot, ItemObject obj)>();

        var potentialIds = Item.GenerateIds(name);

        foreach (var id in potentialIds)
            if (ItemIndex.TryGetValue(id, out var foundItems))
                itemList.AddRange(foundItems);

        return itemList.Count != 0;
    }

    public bool TryRemoveQuantity(string id, out List<(byte Slot, int Quantity)> affectedSlots, int quantity = 1)
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
        foreach (var id in Item.GenerateIds(name)) ret.AddRange(GetSlotsById(id));

        return ret;
    }

    public List<byte> GetSlotsById(string id)
    {
        var ret = new List<byte>();
        if (ItemIndex.ContainsKey(id))
            ret.AddRange(ItemIndex[id].Select(selector: x => x.Slot));
        return ret;
    }

    public bool TryGetValue(string templateId, out ItemObject itemObject)
    {
        itemObject = null;
        if (!ItemIndex.TryGetValue(templateId, out var itemList)) return false;
        itemObject = itemList.First().Item;
        return true;
    }

    public bool Contains(ItemObject io) => GuidIndex.Contains(io.Guid);

    public bool ContainsName(string name, int quantity = 1)
    {
        return Item.GenerateIds(name).Any(predicate: x => ContainsId(x, quantity));
    }

    public bool ContainsId(string id, int quantity = 1)
    {
        return ItemIndex.ContainsKey(id) && ItemIndex[id].Sum(selector: x => x.Item.Count) >= quantity;
    }

    public byte FindEmptySlot()
    {
        return Items.First(predicate: x => x.Value == null).Key;
    }

    public byte SlotOfId(string id) =>
        ItemIndex.ContainsKey(id) ? ItemIndex[id].First().Slot : byte.MinValue;

    public byte SlotOfName(string name) =>
        (from id in Item.GenerateIds(name) where ItemIndex.ContainsKey(id) select ItemIndex[id].First().Slot)
        .FirstOrDefault();

    public List<byte> GetSlotsByCategory(params string[] categories)
    {
        var lower = categories.Select(selector: x => x.ToLower()).ToList();
        var ret = new List<byte>();
        foreach (var kvp in CategoryIndex.Where(predicate: kvp => lower.Contains(kvp.Key)))
            ret.AddRange(kvp.Value.Select(selector: x => x.Slot));

        return ret;
    }

    public ItemObject FindById(string id) => ItemIndex.ContainsKey(id) ? ItemIndex[id].First().Item : null;

    public ItemObject FindByName(string name) =>
        (from id in Item.GenerateIds(name) where ItemIndex.ContainsKey(id) select ItemIndex[id].First().Item)
        .FirstOrDefault();

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
            for (byte x = 1; x <= Size; x++) Items[x] = null;
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

    public override string ToString()
    {
        return Items.Where(predicate: x => x.Value != null).Aggregate(string.Empty, func: (current, item)
            => $"{current}\nslot {item.Key}: {item.Value.Name}, qty {item.Value.Count}");
    }
}