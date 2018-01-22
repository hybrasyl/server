﻿/*
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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using System.Runtime.Serialization;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Properties;
using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Hybrasyl.Items;
using System.Collections.Concurrent;
using Hybrasyl.Threading;

namespace Hybrasyl
{

    public class Exchange
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

        public static bool StartConditionsValid(User source, User target)
        {
            return source.Map == target.Map && source.IsInViewport(target) &&
                   target.IsInViewport(source) &&
                   source.Condition.NoFlags &&
                   target.Condition.NoFlags && 
                   target.Distance(source) <= Constants.EXCHANGE_DISTANCE;

        }

        public bool ConditionsValid
        {
            get
            {
                return _source.Map == _target.Map && _source.IsInViewport(_target) &&
                       _target.IsInViewport(_source) &&
                       _source.Condition.InExchange &&
                       _target.Condition.InExchange &&
                       _active;
            }
        }

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
                var targetItem = giver == _target ? _source.Inventory.Find(theItem.Name) : _target.Inventory.Find(theItem.Name);

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
                theItem.Count -= quantity;
                giver.SendItemUpdate(theItem, slot);
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
                Logger.WarnFormat("exchange: Hijinx occuring: participants are {0} and {1}",
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
            Logger.InfoFormat("Starting exchange between {0} and {1}", _source.Name, _target.Name);
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
            Logger.Info("Performing exchange");
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
                Logger.InfoFormat("Exchange: source ({0}) confirmed", _source.Name);
                _sourceConfirmed = true;
                _target.SendExchangeConfirmation(false);
            }
            if (_target == requestor)
            {
                Logger.InfoFormat("Exchange: target ({0}) confirmed", _target.Name);
                _targetConfirmed = true;
                _source.SendExchangeConfirmation(false);
            }
            if (_sourceConfirmed && _targetConfirmed)
            {
                Logger.Info("Exchange: Both sides confirmed");
                _source.SendExchangeConfirmation();
                _target.SendExchangeConfirmation();
                PerformExchange();
            }
        }
    }

    public class InventoryConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {

            var inventory = (Inventory) value;
            var output = new object[inventory.Size];
            for (byte i = 0; i < inventory.Size; i++)
            {
                dynamic itemInfo = new JObject();
                if (inventory[i] != null)
                {
                    itemInfo.Name = inventory[i].Name;
                    itemInfo.Count = inventory[i].Count;
                    itemInfo.Id = inventory[i].TemplateId;
                    output[i] = itemInfo;
                }               
            }
            var ja = JArray.FromObject(output);
            serializer.Serialize(writer, ja);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JArray jArray = JArray.Load(reader);
            var inv = new Inventory(jArray.Count);

            for (byte i = 0; i < jArray.Count; i++)
            {
                Item itmType = null;
                dynamic item;
                if (TryGetValue(jArray[i], out item))
                {
                    itmType = Game.World.WorldData.Get<Item>(item.Id);
                    //itmType = Game.World.WorldData.Values<Item>().Where(x => x.Name == (string)item.FirstOrDefault().Value).FirstOrDefault().Name;
                    if (itmType != null)
                    {
                        inv[i] = new ItemObject(itmType.Id, Game.World)
                        {
                            Count = item.Count ?? 1
                        };
                            //this will need to be expanded later based on ItemObject properties being saved back to the database.
                    }
                }
            }

            return inv;
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
        public DateTime LastSaved { get; set; }

        private object _lock = new object();

        private Lockable<ItemObject[]> _itemsObject;
        private ConcurrentDictionary<int, List<ItemObject>> _inventoryIndex;

        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Lockable<int> _size { get; set; }
        private Lockable<int> _count { get; set; }
        private Lockable<int> _weight { get; set; }
        public int Size
        {
            get { return _size.Value; }
            private set { _size.Value = value; }
        }
        public int Count
        {
            get { return _count.Value; }
            private set { _count.Value = value; }
        }
        public int Weight
        {
            get { return _weight.Value; }
            private set { _weight.Value = value; }
        }


        #region Equipment Properties

        public ItemObject Weapon
        {
            get { return _itemsObject.Value[ServerItemSlots.Weapon]; }
        }

        public ItemObject Armor
        {
            get { return _itemsObject.Value[ServerItemSlots.Armor]; }
        }

        public ItemObject Shield
        {
            get { return _itemsObject.Value[ServerItemSlots.Shield]; }
        }

        public ItemObject Helmet
        {
            get { return _itemsObject.Value[ServerItemSlots.Helmet]; }
        }

        public ItemObject Earring
        {
            get { return _itemsObject.Value[ServerItemSlots.Earring]; }
        }

        public ItemObject Necklace
        {
            get { return _itemsObject.Value[ServerItemSlots.Necklace]; }
        }

        public ItemObject LRing
        {
            get { return _itemsObject.Value[ServerItemSlots.LHand]; }
        }

        public ItemObject RRing
        {
            get { return _itemsObject.Value[ServerItemSlots.RHand]; }
        }

        public ItemObject LGauntlet
        {
            get { return _itemsObject.Value[ServerItemSlots.LArm]; }
        }

        public ItemObject RGauntlet
        {
            get { return _itemsObject.Value[ServerItemSlots.RArm]; }
        }

        public ItemObject Belt
        {
            get { return _itemsObject.Value[ServerItemSlots.Waist]; }
        }

        public ItemObject Greaves
        {
            get { return _itemsObject.Value[ServerItemSlots.Leg]; }
        }

        public ItemObject Boots
        {
            get { return _itemsObject.Value[ServerItemSlots.Foot]; }
        }

        public ItemObject FirstAcc
        {
            get { return _itemsObject.Value[ServerItemSlots.FirstAcc]; }
        }

        public ItemObject Overcoat
        {
            get { return _itemsObject.Value[ServerItemSlots.Trousers]; }
        }

        public ItemObject DisplayHelm
        {
            get { return _itemsObject.Value[ServerItemSlots.Coat]; }
        }

        public ItemObject SecondAcc
        {
            get { return _itemsObject.Value[ServerItemSlots.SecondAcc]; }
        }

        public ItemObject ThirdAcc
        {
            get { return _itemsObject.Value[ServerItemSlots.ThirdAcc]; }
        }
        #endregion Equipment Properties

        public bool IsFull
        {
            get { return Count == Size; }
        }
        public int EmptySlots
        {
            get { return Size - Count; }
        }

        public void RecalculateWeight()
        {
            var newWeight = 0;
            foreach (var obj in this)
            {
                newWeight += obj.Weight;
            }
            Weight = newWeight;
        }

        public ItemObject this[byte slot]
        {
            get
            {
                var index = slot - 1;
                if (index < 0 || index >= Size)
                    return null;
                return _itemsObject.Value[index];
            }
            internal set
            {
                lock (_lock)
                {
                    int index = slot - 1;
                    if (index < 0 || index >= Size)
                        return;
                    if (value == null)
                        _RemoveFromIndex(_itemsObject.Value[index]);
                    else
                        _AddToIndex(value);
                    _itemsObject.Value[index] = value;
                }
            }   
        }

        private void _AddToIndex(ItemObject itemObject)
        {
            List<ItemObject> itemList;
            if (_inventoryIndex.TryGetValue(itemObject.TemplateId, out itemList))
            {
                itemList.Add(itemObject);
            }
            else
                _inventoryIndex.TryAdd(itemObject.TemplateId, new List<ItemObject> { itemObject });
        }

        private void _RemoveFromIndex(ItemObject itemObject)
        {
            List<ItemObject> itemList;
            lock (_lock)
            {
                if (_inventoryIndex.TryGetValue(itemObject.TemplateId, out itemList))
                {
                    _inventoryIndex[itemObject.TemplateId] = itemList.Where(x => x.Id != itemObject.Id).ToList();
                    if (_inventoryIndex[itemObject.TemplateId].Count == 0)
                        _inventoryIndex.TryRemove(itemObject.TemplateId, out List<ItemObject> value);
                }
            }
        }

        public bool TryGetValue(string name, out ItemObject itemObject)
        {
            itemObject = null;
            List<ItemObject> itemList;
            Item theItem;
            if (!Game.World.TryGetItemTemplate(name, out theItem) ||
                !_inventoryIndex.TryGetValue(theItem.Id, out itemList)) return false;
            itemObject = itemList.First();
            return true;
        }

        public bool TryGetValue(int templateId, out ItemObject itemObject)
        {
            itemObject = null;
            List<ItemObject> itemList;
            if (!_inventoryIndex.TryGetValue(templateId, out itemList)) return false;
            itemObject = itemList.First();
            return true;
        }

        public Inventory(int size)
        {
            _itemsObject = new Lockable<ItemObject[]>(new ItemObject[size]);
            _size = new Lockable<int>(size);
            _count = new Lockable<int>(0);
            _weight = new Lockable<int>(0);
            _inventoryIndex = new ConcurrentDictionary<int, List<ItemObject>>();
        }

        public bool Contains(int id)
        {
            return _inventoryIndex.ContainsKey(id);
        }

        public bool Contains(string name)
        {
            Item theItem = Game.World.WorldData.GetByIndex<Item>(name);
            return _inventoryIndex.ContainsKey(theItem.Id);
        }

        public bool Contains(string name, int quantity)
        {
            Item theItem = Game.World.WorldData.GetByIndex<Item>(name); 
            
            var hasItem = _inventoryIndex.ContainsKey(theItem.Id);
            if (!hasItem) return false;
            var inv = _inventoryIndex.Where(x=> x.Key == theItem.Id).ToList();

            var quant = 0;
            foreach (var itm in inv)
            {
                quant += itm.Value.Count;
            }

            var hasQuantity = quant >= quantity;
            return hasQuantity;
        }



        public int FindEmptyIndex()
        {
            for (var i = 0; i < Size; ++i)
            {
                if (_itemsObject.Value[i] == null)
                    return i;
            }
            return -1;
        }
        public byte FindEmptySlot()
        {
            return (byte)(FindEmptyIndex() + 1);
        }

        public int IndexOf(int id)
        {
            for (var i = 0; i < Size; ++i)
            {
                if (_itemsObject.Value[i] != null && _itemsObject.Value[i].TemplateId == id)
                    return i;
            }
            return -1;
        }

        public int[] IndexOf(string name)
        {
            var indices = new List<int>();
            for (var i = 0; i < Size; i++)
            {
                if (_itemsObject.Value[i] != null && _itemsObject.Value[i].Name == name) indices.Add(i);
            }
            if (indices.Count == 0) indices.Add(-1);
            return indices.ToArray();
        }

        public byte SlotOf(int id)
        {
            return (byte)(IndexOf(id) + 1);
        }
        public byte[] SlotOf(string name)
        {
            var slotsInt = IndexOf(name);
            var slotsByte = new byte[slotsInt.Length];
            for(int i = 0; i < slotsInt.Length; i++)
            {
                slotsByte[i] = (byte)(slotsInt[i] + 1);
            }
            return slotsByte;
        }

        public ItemObject Find(int id)
        {
            return _inventoryIndex.ContainsKey(id) ? _inventoryIndex[id].First() : null;
        }

        public ItemObject Find(string name)
        {
            Item theItem;
            return Game.World.TryGetItemTemplate(name, out theItem) && _inventoryIndex.ContainsKey(theItem.Id)
                ? _inventoryIndex[theItem.Id].First()
                : null;
        }

        public bool AddItem(ItemObject itemObject)
        {
            if (IsFull) return false;
            var slot = FindEmptySlot();
            return Insert(slot, itemObject);
        }

        public bool Insert(byte slot, ItemObject itemObject)
        {
            var index = slot - 1;
            if (index < 0 || index >= Size || _itemsObject.Value[index] != null)
                return false;
            _itemsObject.Value[index] = itemObject;
            Count += 1;
            Weight += itemObject.Weight;
            _AddToIndex(itemObject);

            return true;
        }

        public bool Remove(byte slot)
        {
            var index = slot - 1;
            if (index < 0 || index >= Size || _itemsObject.Value[index] == null)
                return false;
            var item = _itemsObject.Value[index];
            _itemsObject.Value[index] = null;
            Count -= 1;
            Weight -= item.Weight;
            _RemoveFromIndex(item);

            return true;
        }

        public bool Swap(byte slot1, byte slot2)
        {
            int index1 = slot1 - 1, index2 = slot2 - 1;
            if (index1 < 0 || index1 >= Size || index2 < 0 || index2 >= Size)
                return false;
            var item = _itemsObject.Value[index1];
            _itemsObject.Value[index1] = _itemsObject.Value[index2];
            _itemsObject.Value[index2] = item;
            return true;
        }

        public void Clear()
        {
            for (var i = 0; i < Size; ++i)
                _itemsObject.Value[i] = null;
            Count = 0;
            Weight = 0;
            _inventoryIndex.Clear();
        }

        public bool Increase(byte slot, int amount)
        {
            var index = slot - 1;
            if (index < 0 || index >= Size || _itemsObject.Value[index] == null)
                return false;
            var item = _itemsObject.Value[index];
            if (item.Count + amount > item.MaximumStack)
                return false;
            item.Count += amount;
            return true;
        }

        public bool Decrease(byte slot, int amount)
        {
            var index = slot - 1;
            if (index < 0 || index >= Size || _itemsObject.Value[index] == null)
                return false;
            var item = _itemsObject.Value[index];
            if (item.Count < amount)
                return false;
            item.Count -= amount;
            if (item.Count != 0) return true;
            _itemsObject.Value[index] = null;
            Count -= 1;
            Weight -= item.Weight;
            return true;
        }

        public IEnumerator<ItemObject> GetEnumerator()
        {
            for (var i = 0; i < Size; ++i)
            {
                if (_itemsObject.Value[i] != null)
                    yield return _itemsObject.Value[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public List<Tuple<ushort,byte>> GetEquipmentDisplayList()
        {
            var returnList = new List<Tuple<ushort, byte>>();

            for (var x = 0; x < 18; ++x)
            {
                // This is fucking bullshit. Why would you even do this? HEY I KNOW KOREAN INTERN DESIGNING
                // THIS PROTOCOL, LET'S RANDOMLY SWAP ITEM SLOTS FOR NO REASON!!11`1`
                if (x == ServerItemSlots.Foot)
                    returnList.Add(_itemsObject.Value[ServerItemSlots.FirstAcc] == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort)(0x8000 + _itemsObject.Value[ServerItemSlots.FirstAcc].EquipSprite), _itemsObject.Value[ServerItemSlots.FirstAcc].Color));
                else if (x == ServerItemSlots.FirstAcc)
                    returnList.Add(_itemsObject.Value[ServerItemSlots.Foot] == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort)(0x8000 + _itemsObject.Value[ServerItemSlots.Foot].EquipSprite), _itemsObject.Value[ServerItemSlots.Foot].Color));
                else
                    returnList.Add(_itemsObject.Value[x] == null
                        ? new Tuple<ushort, byte>(0, 0)
                        : new Tuple<ushort, byte>((ushort) (0x8000 + _itemsObject.Value[x].EquipSprite), _itemsObject.Value[x].Color));
            }

            return returnList;
        }

    }
}
