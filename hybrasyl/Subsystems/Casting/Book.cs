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

using Hybrasyl.Xml.Objects;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using XmlBook = Hybrasyl.Xml.Objects.Book;

namespace Hybrasyl.Casting;

[JsonConverter(typeof(BookConverter))]
public class Book : IEnumerable<BookSlot>
{
    private readonly Dictionary<int, BookSlot> _itemIndex;
    private readonly BookSlot[] _items;

    public Book()
    {
        _items = new BookSlot[90];
        Size = 90;
        _itemIndex = new Dictionary<int, BookSlot>();
    }

    public Book(int size)
    {
        _items = new BookSlot[size];
        Size = size;
        _itemIndex = new Dictionary<int, BookSlot>();
    }

    // Slots 36, 72 and 90 are inexplicably unusable on client, and client
    // numbering for book slots is 1-based, so...
    //
    // Primary 0-34, 35 unusable
    // Secondary 36-70, 71 unusable
    // Utility 72-88, 89 unusable

    public bool IsPrimaryFull => _items[..34].Count(predicate: x => x is not null) == 35;
    public bool IsSecondaryFull => _items[36..70].Count(predicate: x => x is not null) == 35;
    public bool IsUtilityFull => _items[72..88].Count(predicate: x => x is not null) == 17;

    public int Size { get; }
    public int Count { get; private set; }

    public BookSlot this[byte slot]
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
            var index = slot - 1;
            if (index < 0 || index >= Size)
                return;
            if (value == null)
                _RemoveFromIndex(_items[index]);
            else
                _AddToIndex(value);
            _items[index] = value;
        }
    }

    public IEnumerator<BookSlot> GetEnumerator()
    {
        for (var i = 0; i < Size; ++i)
            if (_items[i] != null)
            {
                _items[i].Slot = i;
                yield return _items[i];
            }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool IsFull(XmlBook book)
    {
        if (book == XmlBook.PrimarySkill || book == XmlBook.PrimarySpell)
            return IsPrimaryFull;
        if (book == XmlBook.SecondarySkill || book == XmlBook.SecondarySpell)
            return IsSecondaryFull;
        if (book == XmlBook.UtilitySkill || book == XmlBook.UtilitySpell)
            return IsUtilityFull;
        throw new ArgumentException($"Unknown book value {book}");
    }

    private void _AddToIndex(BookSlot item)
    {
        if (item.Castable != null)
            _itemIndex[item.Castable.Id] = item;
    }

    private void _RemoveFromIndex(BookSlot item)
    {
        if (item != null)
            if (_itemIndex.Keys.Contains(item.Castable.Id))
                _itemIndex.Remove(item.Castable.Id);
    }

    public bool Contains(int id) => _itemIndex.ContainsKey(id);

    public int FindEmptyIndex(int begin = 0, int end = 0)
    {
        for (var i = begin; i < (end == 0 ? Size : end); ++i)
            if (_items[i] == null)
                return i;
        return -2;
    }

    public byte FindEmptyPrimarySlot() => (byte) (FindEmptyIndex(0, 34) + 1);

    public byte FindEmptySecondarySlot() => (byte) (FindEmptyIndex(36, 70) + 1);

    public byte FindEmptyUtilitySlot() => (byte) (FindEmptyIndex(72, 88) + 1);

    public byte FindEmptySlot(XmlBook book)
    {
        if (book == XmlBook.PrimarySkill || book == XmlBook.PrimarySpell)
            return FindEmptyPrimarySlot();
        if (book == XmlBook.SecondarySkill || book == XmlBook.SecondarySpell)
            return FindEmptySecondarySlot();
        if (book == XmlBook.UtilitySkill || book == XmlBook.UtilitySpell)
            return FindEmptyUtilitySlot();
        throw new ArgumentException($"Unknown book value {book}");
    }

    public int IndexOf(int id)
    {
        for (var i = 0; i < Size; ++i)
            if (_items[i] != null && _items[i].Castable.Id == id)
                return i;
        return -1;
    }

    public int IndexOf(string name)
    {
        for (var i = 0; i < Size; ++i)
            if (_items[i] != null && _items[i].Castable.Name == name)
                return i;
        return -1;
    }

    public byte SlotOf(int id) => (byte) (IndexOf(id) + 1);

    public byte SlotOf(string name) => (byte) (IndexOf(name) + 1);

    public bool Add(Castable item)
    {
        if (IsFull(item.Book)) return false;
        var slot = FindEmptySlot(item.Book);
        return Insert(slot, item);
    }

    public bool Insert(byte slot, Castable item)
    {
        var index = slot - 1;
        if (index < 0 || index >= Size || _items[index] != null)
            return false;
        var bookSlot = new BookSlot { Castable = item, UseCount = 0, LastCast = default };
        _items[index] = bookSlot;
        Count += 1;
        _AddToIndex(bookSlot);

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
        (_items[index1], _items[index2]) = (_items[index2], _items[index1]);
        return true;
    }

    public void Clear()
    {
        for (var i = 0; i < Size; ++i)
            _items[i] = null;
        Count = 0;
        _itemIndex.Clear();
    }

    public BookSlot GetSlotByName(string name)
    {
        var i = IndexOf(name);
        return i == -1 ? null : _items[i];
    }
}