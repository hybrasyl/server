using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Xml;

public partial class CreatureCastingSet : IEquatable<CreatureCastingSet>
{
    public Guid Guid { get; set; } = Guid.Empty;

    public List<string> CategoryList => string.IsNullOrEmpty(Categories)
        ? new List<string>()
        : Categories.Trim().Split(" ").Select(selector: x => x.ToLower()).ToList();

    public bool Equals(CreatureCastingSet set)
    {
        if (set is null) return false;
        if (ReferenceEquals(this, set)) return true;
        if (GetType() != set.GetType()) return false;
        return set.Guid == Guid;
    }

    public override bool Equals(object obj) => Equals(obj as CreatureCastingSet);

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

    public override int GetHashCode()
    {
        if (Guid == Guid.Empty)
            Guid = Guid.NewGuid();
        return Guid.GetHashCode();
    }
}