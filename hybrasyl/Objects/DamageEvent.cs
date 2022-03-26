using Hybrasyl.Xml;

namespace Hybrasyl.Objects;

public class DamageEvent
{
    public Creature Attacker { get; set; } = null;
    public Creature Target { get; set; } = null;
    public uint Damage { get; set; } = 0;
    public DamageType Type;
    public DamageFlags Flags;
}