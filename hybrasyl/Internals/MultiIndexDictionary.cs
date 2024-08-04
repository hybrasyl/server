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

using System.Collections.Generic;

namespace Hybrasyl.Internals;

public class MultiIndexDictionary<TKey1, TKey2, TValue>
{
    private Dictionary<TKey1, KeyValuePair<TKey2, TValue>> _dict1 = new();
    private Dictionary<TKey2, KeyValuePair<TKey1, TValue>> _dict2 = new();

    public int Count => _dict1.Count;

    public void Add(TKey1 k1, TKey2 k2, TValue value)
    {
        _dict1.Add(k1, new KeyValuePair<TKey2, TValue>(k2, value));
        _dict2.Add(k2, new KeyValuePair<TKey1, TValue>(k1, value));
    }

    public void AddOrUpdate(TKey1 k1, TKey2 k2, TValue value)
    {
        _dict1[k1] = new KeyValuePair<TKey2, TValue>(k2, value);
        _dict2[k2] = new KeyValuePair<TKey1, TValue>(k1, value);
    }
    public void Clear()
    {
        _dict1 = new Dictionary<TKey1, KeyValuePair<TKey2, TValue>>();
        _dict2 = new Dictionary<TKey2, KeyValuePair<TKey1, TValue>>();
    }

    public bool ContainsKey(TKey1 k1) => _dict1.ContainsKey(k1);

    public bool ContainsKey(TKey2 k2) => _dict2.ContainsKey(k2);

    public bool Remove(TKey1 k1)
    {
        if (!_dict1.ContainsKey(k1)) return false;
        var k2obj = _dict1[k1];
        return _dict1.Remove(k1) && _dict2.Remove(k2obj.Key);

    }

    public bool Remove(TKey2 k2)
    {
        if (!_dict2.ContainsKey(k2)) return false;
        var k1obj = _dict2[k2];
        return _dict2.Remove(k2) && _dict1.Remove(k1obj.Key);

    }

    public bool TryGetValue(TKey1 k1, out TValue value)
    {
        value = default;
        if (!_dict1.TryGetValue(k1, out var kvp)) return false;
        value = kvp.Value;
        return true;

    }

    public bool TryGetValue(TKey2 k2, out TValue value)
    {
        value = default;
        if (!_dict2.TryGetValue(k2, out var kvp)) return false;
        value = kvp.Value;
        return true;

    }
}