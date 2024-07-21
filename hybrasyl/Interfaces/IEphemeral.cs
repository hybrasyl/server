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
        {
            EphemeralStore[key] = value;
        }
    }

    public List<Tuple<string, dynamic>> GetEphemeralValues()
    {
        var ret = new List<Tuple<string, dynamic>>();
        lock (StoreLock)
        {
            ret.AddRange(EphemeralStore.Select(selector: entry => new Tuple<string, dynamic>(entry.Key, entry.Value)));
        }

        return ret;
    }

    public dynamic GetEphemeral(string key)
    {
        lock (StoreLock)
        {
            return EphemeralStore.ContainsKey(key) ? EphemeralStore[key] : null;
        }
    }

    public bool ClearEphemeral(string key)
    {
        lock (StoreLock)
        {
            return EphemeralStore.ContainsKey(key) && EphemeralStore.Remove(key);
        }
    }

    public bool TryGetEphemeral(string key, out dynamic value)
    {
        lock (StoreLock)
        {
            return EphemeralStore.TryGetValue(key, out value);
        }
    }
}