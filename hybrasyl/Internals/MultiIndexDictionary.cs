using System.Collections.Generic;

namespace Hybrasyl.Internals;

public class MultiIndexDictionary<TKey1, TKey2, TValue>
{
    private Dictionary<TKey1, KeyValuePair<TKey2, TValue>> _dict1;
    private Dictionary<TKey2, KeyValuePair<TKey1, TValue>> _dict2;

    public MultiIndexDictionary()
    {
        _dict1 = new Dictionary<TKey1, KeyValuePair<TKey2, TValue>>();
        _dict2 = new Dictionary<TKey2, KeyValuePair<TKey1, TValue>>();
    }

    public int Count => _dict1.Count;

    public void Add(TKey1 k1, TKey2 k2, TValue value)
    {
        _dict1.Add(k1, new KeyValuePair<TKey2, TValue>(k2, value));
        _dict2.Add(k2, new KeyValuePair<TKey1, TValue>(k1, value));
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
        if (_dict1.ContainsKey(k1))
        {
            var k2obj = _dict1[k1];
            return _dict1.Remove(k1) && _dict2.Remove(k2obj.Key);
        }

        return false;
    }

    public bool Remove(TKey2 k2)
    {
        if (_dict2.ContainsKey(k2))
        {
            var k1obj = _dict2[k2];
            return _dict2.Remove(k2) && _dict1.Remove(k1obj.Key);
        }

        return false;
    }

    public bool TryGetValue(TKey1 k1, out TValue value)
    {
        value = default;
        if (_dict1.TryGetValue(k1, out var kvp))
        {
            value = kvp.Value;
            return true;
        }

        return false;
    }

    public bool TryGetValue(TKey2 k2, out TValue value)
    {
        value = default;
        if (_dict2.TryGetValue(k2, out var kvp))
        {
            value = kvp.Value;
            return true;
        }

        return false;
    }
}
