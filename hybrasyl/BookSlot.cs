using System;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl;

public class BookSlot
{
    public Castable Castable { get; set; }
    public uint UseCount { get; set; }
    public uint MasteryLevel { get; set; }
    public DateTime LastCast { get; set; }

    public bool OnCooldown => Castable.Cooldown > 0 &&
                              (DateTime.Now - LastCast).TotalSeconds < Castable.Cooldown;

    public bool HasBeenUsed => LastCast != default;
    public double SecondsSinceLastUse => (DateTime.Now - LastCast).TotalSeconds;
}