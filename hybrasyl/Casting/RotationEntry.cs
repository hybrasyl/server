using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Objects;
using Hybrasyl.Xml;

namespace Hybrasyl.Casting;

public class RotationEntry
{
    public BookSlot Slot { get; }
    public string Name => Slot.Castable.Name;
    public CreatureCastable Directive { get; }
    public DateTime LastUse { get; set; } = DateTime.MinValue;
    public long SecondsSinceLastUse => (long) (DateTime.Now - LastUse).TotalSeconds;
    public bool UseOnce => Directive.UseOnce;
    public bool Expired => SecondsSinceLastUse >= Directive.Interval;
    public bool ThresholdTriggered { get; set; } = false;
    public Rotation Parent { get; set; }
    public CreatureTargetPriority DefaultTargetPriority => Directive.TargetPriority;
    public int Threshold => Directive.HealthPercentage;
    private CreatureTargetPriority currentPriority { get; set; } = CreatureTargetPriority.None;
    // TODO: hardcoded
    public double CastingTime => Slot.Castable.Lines * 1.25;

    public CreatureTargetPriority CurrentPriority
    {
        get => currentPriority != CreatureTargetPriority.None ? currentPriority :
            DefaultTargetPriority == CreatureTargetPriority.None ? Parent.TargetPriority : DefaultTargetPriority;
        set => currentPriority = value;
    }

    public RotationEntry(BookSlot slot, CreatureCastable directive)
    {
        Slot = slot;
        Directive = directive;
    }

    public override string ToString()
    {
        var ret = $"{Name} ({DefaultTargetPriority})";
        if (Directive.HealthPercentage != -1)
            ret += $" at {Directive.HealthPercentage}";
        if (Directive.UseOnce)
            ret += " (once)";
        else
            ret += $" every {Directive.Interval}s";
        return ret;
    }

    public void Use() => Parent.Use();

}