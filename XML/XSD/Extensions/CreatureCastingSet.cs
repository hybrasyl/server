using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Xml;

public partial class CreatureCastingSet : IEquatable<CreatureCastingSet>
{
    public Guid Guid { get; set; } = new Guid();
    public bool Active { get; set; } = false;
    public DateTime LastUsed { get; set; } = DateTime.MinValue;
    public double SecondsSinceLastUse => (DateTime.Now - LastUsed).TotalSeconds;
    public List<string> CategoryList
    {
        get
        {
            if (string.IsNullOrEmpty(Categories))
                return new List<string>();
            else
                return Categories.Trim().Split(" ").ToList();
        }
    }

    public override bool Equals(object obj) => Equals(obj as CreatureCastingSet);

    public bool Equals(CreatureCastingSet set)
    {
        if (set is null) return false;
        if (ReferenceEquals(this, set)) return true;
        if (GetType() != set.GetType()) return false;
        return set.Guid == Guid;
    }

    public static bool operator ==(CreatureCastingSet lhs, CreatureCastingSet rhs)
    {
        return lhs switch
        {
            null when rhs is null => true,
            null => false,
            _ => lhs.Equals(rhs)
        };
    }

    public static bool operator !=(CreatureCastingSet lhs, CreatureCastingSet rhs) => !(lhs == rhs);


}