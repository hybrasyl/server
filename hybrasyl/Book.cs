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
 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl
{
    public class BookConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {

            var book = (Book)value;
            var output = new object[book.Size];
            for (byte i = 0; i < book.Size; i++)
            {
                dynamic itemInfo = new JObject();
                if (book[i] == null) continue;
                itemInfo.Name = book[i].Name.ToLower();
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
                    dynamic item;
                    if (!TryGetValue(jArray[i], out item)) continue;
                    book[i] = Game.World.WorldData.Values<Xml.Castable>().SingleOrDefault(x => x.Name.ToLower() == (string)item.Name);
                    var castable = book[i];
                    if (castable != null)
                    {
                        castable.UseCount = (ushort)(item.TotalUses == null ? 0 : item.TotalUses);
                        castable.MasteryLevel = (byte)(item.MasteryLevel == null ? (byte)0 : item.MasteryLevel);
                    }
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
                    book[i] = Game.World.WorldData.Values<Xml.Castable>().SingleOrDefault(x => x.Name.ToLower() == (string)item.Name);
                    var castable = book[i];
                    if (castable != null)
                    {
                        castable.UseCount = Convert.ToUInt16(item.TotalUses == null ? 0 : item.TotalUses);
                        castable.MasteryLevel = Convert.ToByte(item.MasteryLevel == null ? (byte)0 : item.MasteryLevel);
                        if (item.GetType().GetProperty("LastCast") != null)
                            castable.LastCast = (DateTime)item.LastCast;
                        else
                            castable.LastCast = DateTime.MinValue;
                    }
                }
                return book;
            }
           
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

    [JsonConverter(typeof(BookConverter))]
    public class Book : IEnumerable<Xml.Castable>
    {
        private Xml.Castable[] _items;
        private Dictionary<int, Xml.Castable> _itemIndex;

        public bool IsFull(Xml.Book book)
        {
            if (book == Xml.Book.PrimarySkill || book == Xml.Book.PrimarySpell)
                return IsPrimaryFull;
            if (book == Xml.Book.SecondarySkill || book == Xml.Book.SecondarySpell)
                return IsSecondaryFull;
            if (book == Xml.Book.UtilitySkill || book == Xml.Book.UtilitySpell)
                return IsUtilityFull;
            throw new ArgumentException($"Unknown book value {book}");
        }

        // Slots 36, 72 and 90 are inexplicably unusable on client, and client
        // numbering for book slots is 1-based, so...
        //
        // Primary 0-34, 35 unusable
        // Secondary 36-70, 71 unusable
        // Utility 72-88, 89 unusable

        public bool IsPrimaryFull => _items[0..34].Where(x => x != null).Count() == 35;
        public bool IsSecondaryFull => _items[36..70].Where(x => x != null).Count() == 35;
        public bool IsUtilityFull => _items[72..88].Where(x => x != null).Count() == 17;

        public int EmptySlots => Size - Count;
        public int Size { get; private set; }
        public int Count { get; private set; }

        public Xml.Castable this[byte slot]
        {
            get
            {
                var index = slot - 1;
                if (index < 0 || index >= Size)
                    return null;
                return _items[index];
            }
            internal set
            {
                int index = slot - 1;
                if (index < 0 || index >= Size)
                    return;
                if (value == null)
                    _RemoveFromIndex(_items[index]);
                else
                    _AddToIndex(value);
                _items[index] = value;
            }
        }

        private void _AddToIndex(Xml.Castable item)
        {
            _itemIndex[item.Id] = item;
        }

        private void _RemoveFromIndex(Xml.Castable item)
        {
            if (item != null)
            {
                if (_itemIndex.Keys.Contains(item.Id))
                    _itemIndex.Remove(item.Id);
            }
        }

        public Book()
        {
            this._items = new Xml.Castable[90];
            Size = 90;
            this._itemIndex = new Dictionary<int, Xml.Castable>();
        }

        public Book(int size)
        {
            this._items = new Xml.Castable[size];
            Size = size;
            this._itemIndex = new Dictionary<int, Xml.Castable>();
        }

        public bool Contains(int id)
        {
            return _itemIndex.ContainsKey(id);
        }

        public int FindEmptyIndex(int begin=0, int end=0)
        {
            for (var i = begin; i < (end == 0 ? Size : end); ++i)
            {
                if (_items[i] == null)
                    return i;
            }
            return -2;
        }

        public byte FindEmptyPrimarySlot() => (byte)(FindEmptyIndex(0, 34) + 1);
        public byte FindEmptySecondarySlot() => (byte)(FindEmptyIndex(36, 70) + 1);
        public byte FindEmptyUtilitySlot() => (byte)(FindEmptyIndex(72, 88) + 1);

        public byte FindEmptySlot(Xml.Book book)
        {
            if (book == Xml.Book.PrimarySkill || book == Xml.Book.PrimarySpell)
                return FindEmptyPrimarySlot();
            if (book == Xml.Book.SecondarySkill || book == Xml.Book.SecondarySpell)
                return FindEmptySecondarySlot();
            if (book == Xml.Book.UtilitySkill || book == Xml.Book.UtilitySpell)
                return FindEmptyUtilitySlot();
            throw new ArgumentException($"Unknown book value {book}");
        }

        public int IndexOf(int id)
        {
            for (var i = 0; i < Size; ++i)
            {
                if (_items[i] != null && _items[i].Id == id)
                    return i;
            }
            return -1;
        }
        public int IndexOf(string name)
        {
            for (var i = 0; i < Size; ++i)
            {
                if (_items[i] != null && _items[i].Name == name)
                    return i;
            }
            return -1;
        }

        public byte SlotOf(int id)
        {
            return (byte)(IndexOf(id) + 1);
        }
        public byte SlotOf(string name)
        {
            return (byte)(IndexOf(name) + 1);
        }

        public bool Add(Xml.Castable item)
        {
            if (IsFull(item.Book)) return false;
            var slot = FindEmptySlot(item.Book);
            return Insert(slot, item);
        }

        public bool Insert(byte slot, Xml.Castable item)
        {
            var index = slot - 1;
            if (index < 0 || index >= Size || _items[index] != null)
                return false;
            _items[index] = item;
            Count += 1;
            _AddToIndex(item);

            return true;
        }
        public bool Remove(byte slot)
        {
            var index = slot - 1;
            if (index < 0 || index >= Size || _items[index] == null)
                return false;
            var item = _items[index];
            _items[index] = null;
            Count -= 1;
            _RemoveFromIndex(item);

            return true;
        }

        public bool Swap(byte slot1, byte slot2)
        {
            int index1 = slot1 - 1, index2 = slot2 - 1;
            if (index1 < 0 || index1 >= Size || index2 < 0 || index2 >= Size)
                return false;
            var item = _items[index1];
            _items[index1] = _items[index2];
            _items[index2] = item;
            return true;
        }

        public void Clear()
        {
            for (var i = 0; i < Size; ++i)
                _items[i] = null;
            Count = 0;
            _itemIndex.Clear();
        }

        public IEnumerator<Xml.Castable> GetEnumerator()
        {
            for (var i = 0; i < Size; ++i)
            {
                if (_items[i] != null)
                    yield return _items[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }
    [JsonConverter(typeof(BookConverter))]
    public sealed class SkillBook : Book { }
    [JsonConverter(typeof(BookConverter))]
    public sealed class SpellBook : Book { }
}
