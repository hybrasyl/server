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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Casting;

public class Rotation : IList<RotationEntry>
{
    public Rotation(CreatureCastingSet set)
    {
        CastingSet = set;
        Interval = set.Interval;
    }

    private List<RotationEntry> Castables { get; } = new();
    private HashSet<RotationEntry> CastablesIndex { get; } = new();
    private HashSet<RotationEntry> ThresholdCasts { get; } = new();
    private int CurrentIndex { get; set; }

    public DateTime LastUse { get; set; } = DateTime.MinValue;
    public int Interval { get; set; }
    public long SecondsSinceLastUse => (long)(DateTime.Now - LastUse).TotalSeconds;
    public long Priority => SecondsSinceLastUse - Interval;
    public RotationType Type => CastingSet.Type;
    public CreatureCastingSet CastingSet { get; set; }

    public CreatureTargetPriority TargetPriority => CastingSet.TargetPriority;
    public bool Random => CastingSet.Random;
    public bool Active { get; set; } = true;

    public RotationEntry LastCastable { get; set; }

    public RotationEntry CurrentCastable => Castables[CurrentIndex];

    public RotationEntry NextCastable
    {
        get
        {
            if (Castables.Count == 0) return null;
            // Deal with expirations first
            var firstExpired = ThresholdCasts.Where(predicate: x => x.SecondsSinceLastUse >= x.Directive.Interval)
                .OrderByDescending(keySelector: x => x.Directive.Interval).FirstOrDefault();
            if (firstExpired != null) return firstExpired;
            if (CastingSet.Random)
                return Castables.PickRandom();
            return CurrentIndex == Castables.Count - 1 ? Castables[0] : Castables[CurrentIndex + 1];
        }
    }

    public int Count => Castables.Count;

    public bool IsReadOnly => false;

    public RotationEntry this[int index]
    {
        get => Castables[index];
        set
        {
            if (!CastablesIndex.Contains(value))
                CastablesIndex.Add(value);
            Castables[index] = value;
        }
    }

    public int IndexOf(RotationEntry item) => Castables.IndexOf(item);

    public void Insert(int index, RotationEntry item)
    {
        Castables.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException("index");
        var item = Castables[index];
        Castables.RemoveAt(index);
        CastablesIndex.Remove(item);
    }

    public void Add(RotationEntry item)
    {
        if (item == null) return;
        item.Parent = this;
        Castables.Add(item);
        CastablesIndex.Add(item);
    }

    public void Clear()
    {
        Castables.Clear();
        CastablesIndex.Clear();
    }

    public bool Contains(RotationEntry item) => CastablesIndex.Contains(item);

    public void CopyTo(RotationEntry[] array, int arrayIndex)
    {
        Castables.CopyTo(array, arrayIndex);
        foreach (var entry in array)
            CastablesIndex.Add(entry);
    }

    public bool Remove(RotationEntry item) => Castables.Remove(item) && CastablesIndex.Remove(item);

    public IEnumerator<RotationEntry> GetEnumerator() => Castables.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Use()
    {
        LastCastable = CurrentCastable;
        if (Castables.Count > 1)
            CurrentIndex = CurrentIndex + 1 == Castables.Count ? 0 : CurrentIndex + 1;
        CurrentCastable.LastUse = DateTime.Now;
        LastUse = DateTime.Now;
    }

    public void Use(RotationEntry item)
    {
        if (!CastablesIndex.Contains(item)) return;
        LastUse = DateTime.Now;
        item.LastUse = DateTime.Now;
        CurrentIndex = IndexOf(item);
    }

    public override string ToString()
    {
        return string.Join(", ", Castables.Select(selector: x => x.ToString()));
    }
}