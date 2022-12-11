
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Xml;

public partial class Castable
{
    public int Id
    {
        get
        {
            unchecked
            {
                return 31 * (Name.GetHashCode() + 1);
            }
        }
    }

    public Guid Guid { get; set; }

    // Helper functions to deal with xml vagaries
    public List<AddStatus> AddStatuses => Effects.Statuses?.Add ?? new List<AddStatus>();
    public List<string> RemoveStatuses => Effects.Statuses?.Remove ?? new List<string>();
    public List<CastableReactor> Reactors => Effects?.Reactors ?? new List<CastableReactor>();
    public byte CastableLevel { get; set; }

    public ElementType Element
    {
        get
        {
            return Elements.Count switch
            {
                1 => Elements.First(),
                > 1 => Elements.PickRandom(),
                _ => ElementType.None
            };
        }
    }
    public DateTime LastCast { get; set; }

    public bool IsSkill => Book is Book.PrimarySkill or Book.SecondarySkill or Book.UtilitySkill;
    public bool IsSpell => Book is Book.PrimarySpell or Book.SecondarySpell or Book.UtilitySpell;

    public bool OnCooldown => Cooldown > 0 && (DateTime.Now - LastCast).TotalSeconds < Cooldown;

    public List<string> CategoryList
    {
        get
        {
            return Categories.Count > 0
                ? Categories.Select(selector: x => x.Value.ToLower()).ToList()
                : new List<string>();
        }
    }

    public byte GetMaxLevelByClass(Class castableClass)
    {
        var maxLevelProperty = MaxLevel.GetType().GetProperty(castableClass.ToString());
        return (byte) (maxLevelProperty != null ? maxLevelProperty.GetValue(MaxLevel, null) : 0);
    }

    public bool TryGetMotion(Class castClass, out CastableMotion motion)
    {
        motion = null;

        if (Effects?.Animations?.OnCast?.Player == null) return false;

        var m = Effects.Animations.OnCast.Player.Where(predicate: e => e.Class.Contains(castClass));
        if (!m.Any())
            m = Effects.Animations.OnCast.Player.Where(predicate: e => e.Class.Count == 0);

        if (!m.Any())
            return false;
        motion = m.First();

        return true;
    }
}

public class CastableComparer : IEqualityComparer<Castable>
{
    public bool Equals(Castable c1, Castable c2)
    {
        if (c1 == null && c2 == null) return true;
        if (c1 == null || c2 == null) return false;

        return c1.Name.Trim().ToLower() == c2.Name.Trim().ToLower() &&
               c1.Book == c2.Book;
    }

    public int GetHashCode(Castable c)
    {
        var hCode = $"{c.Name.Trim().ToLower()}-{c.Book}";
        return hCode.GetHashCode();
    }
}

