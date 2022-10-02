using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Interfaces;

public interface IEphemeral
{
    public Dictionary<string, dynamic> EphemeralStore { get; set; }
    public object StoreLock { get; }

    public void SetEphemeral(string key, dynamic value)
    {
        lock (StoreLock)
            EphemeralStore[key] = value;
    }

    public List<Tuple<string, dynamic>> GetEphemeralValues()
    {
        var ret = new List<Tuple<string, dynamic>>();
        lock (StoreLock)
        {
            ret.AddRange(EphemeralStore.Select(entry => new Tuple<string, dynamic>(entry.Key, entry.Value)));
        }
        return ret;
    }

    public dynamic GetEphemeral(string key)
    {
        lock (StoreLock)
            return EphemeralStore.ContainsKey(key) ? EphemeralStore[key] : null;
    }

    public bool ClearEphemeral(string key)
    {
        lock (StoreLock)
            return EphemeralStore.ContainsKey(key) && EphemeralStore.Remove(key);
    }

    public bool TryGetEphemeral(string key, out dynamic value)
    {
        lock (StoreLock)
            return EphemeralStore.TryGetValue(key, out value);
    }


}