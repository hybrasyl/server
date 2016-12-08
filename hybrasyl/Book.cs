using Hybrasyl.Castables;
using log4net;
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
                var itemInfo = new string[book.Size];
                if (book[i] != null)
                {
                    itemInfo[i] = book[i].Name.ToLower();
                    output[i] = itemInfo;
                }
            }
            var ja = JArray.FromObject(output);
            serializer.Serialize(writer, ja);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            
            JArray jArray = JArray.Load(reader);
            if (objectType.Name == "SkillBook")
            {
               var book = new SkillBook();

                for (byte i = 0; i < jArray.Count; i++)
                {
                    string[] item;
                    if (TryGetValue(jArray[i], out item))
                    {
                        book[i] = Game.World.WorldData.Values<Castable>().SingleOrDefault(x => x.Name.ToLower() == item[i]);
                    }
                }
                return book;
            }
            else
            {
                var book = new SpellBook();

                for (byte i = 0; i < jArray.Count; i++)
                {
                    string[] item;
                    if (TryGetValue(jArray[i], out item))
                    {
                        book[i] =
                            Game.World.WorldData.Values<Castable>().SingleOrDefault(x => x.Name.ToLower() == item[i]);
                    }
                }
                return book;
            }
           
        }


        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Inventory);
        }

        public bool TryGetValue(JToken token, out string[] item)
        {
            item = null;
            if (!token.HasValues) return false;

            item = token.ToObject<string[]>();
            return true;
        }
    }

    [JsonConverter(typeof(BookConverter))]
    public class Book : IEnumerable<Castable>
    {
        private Castable[] _items;
        private Dictionary<int, Castable> _itemIndex;
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool IsFull => Count == Size;

        public int EmptySlots => Size - Count;
        public int Size { get; private set; }
        public int Count { get; private set; }


        public Castable this[byte slot]
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

        private void _AddToIndex(Castable item)
        {
            _itemIndex[item.Id] = item;
        }

        private void _RemoveFromIndex(Castable item)
        {
            if (item != null)
            {
                if (_itemIndex.Keys.Contains(item.Id))
                    _itemIndex.Remove(item.Id);
            }
        }

        public Book()
        {
            this._items = new Castable[90];
            Size = 90;
            this._itemIndex = new Dictionary<int, Castable>();
        }

        public Book(int size)
        {
            this._items = new Castable[size];
            Size = size;
            this._itemIndex = new Dictionary<int, Castable>();
        }

        public bool Contains(int id)
        {
            return _itemIndex.ContainsKey(id);
        }

        

        public int FindEmptyIndex()
        {
            for (var i = 0; i < Size; ++i)
            {
                if (_items[i] == null)
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

        public bool Add(Castable item)
        {
            if (IsFull) return false;
            var slot = FindEmptySlot();
            return Insert(slot, item);
        }

        public bool Insert(byte slot, Castable item)
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

        public IEnumerator<Castable> GetEnumerator()
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
