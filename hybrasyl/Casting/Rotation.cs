using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Xml;

namespace Hybrasyl.Casting;

public class Rotation : IList<RotationEntry>
{
    private List<RotationEntry> Castables { get; } = new();
    private HashSet<RotationEntry> CastablesIndex { get; } = new();
    private HashSet<RotationEntry> ThresholdCasts { get; } = new();
    private int CurrentIndex { get; set; }

    public DateTime LastUse { get; set; } = DateTime.MinValue;
    public int Interval { get; set; }
    public double SecondsSinceLastUse => (DateTime.Now - LastUse).TotalSeconds;
    public int Priority => (int) (SecondsSinceLastUse - Interval);
    public RotationType Type => CastingSet.Type;
    public CreatureCastingSet CastingSet { get; set; }

    public bool Random => CastingSet.Random;
    public bool Active { get; set; } = true;
    public int Count => Castables.Count;

    public bool IsReadOnly => false;

    public Rotation(CreatureCastingSet set)
    {
        CastingSet = set;
        Interval = set.Interval;
    }

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

    public void Insert(int index, RotationEntry item) => Castables.Insert(index, item);

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

    public void Use(RotationEntry item)
    {
        if (!CastablesIndex.Contains(item)) return;
        LastUse = DateTime.Now;
        item.LastUse = DateTime.Now;
        CurrentIndex = IndexOf(item);
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

    public bool Remove(RotationEntry item)
    {
        return Castables.Remove(item) && CastablesIndex.Remove(item);
    }

    public IEnumerator<RotationEntry> GetEnumerator() => Castables.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public RotationEntry GetNextCastable()
    {
        if (Castables.Count == 0) return null;
        // Deal with expirations first
        var firstExpired = ThresholdCasts.Where(x => x.SecondsSinceLastUse >= x.Directive.Interval)
            .OrderByDescending(x => x.Directive.Interval).FirstOrDefault();
        if (firstExpired != null) return firstExpired;
        if (CastingSet.Random) return Castables.PickRandom();
        if (CurrentIndex == Castables.Count - 1) return Castables[0];
        CurrentIndex++;
        return Castables[CurrentIndex];
    }

    public override string ToString() => string.Join(", ", Castables.Select(x => x.ToString()));
}