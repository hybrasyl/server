using Hybrasyl.Enums;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Objects;

public interface ICombatEvent
{
    public Creature Source { get; set; }
    public Creature Target { get; set; }
    public Castable SourceCastable { get; set; }
    public CombatLogEventType EventType { get; }
    public string SourceName { get; }
    public string TargetName { get; }
    public string SourceCastableName { get; }
}

public record CombatEvent : ICombatEvent
{
    public Creature Source { get; set; }
    public Creature Target { get; set; }
    public Castable SourceCastable { get; set; }
    public virtual CombatLogEventType EventType { get; set; }
    public string SourceName => Source?.Name ?? "unknown";
    public string TargetName => Target?.Name ?? "unknown";
    public string SourceCastableName => SourceCastable?.Name ?? "unknown";
}

[MoonSharpUserData]
public record StatChangeEvent : CombatEvent
{
    public uint Amount;

    public override string ToString()
    {
        return EventType switch
        {
            CombatLogEventType.ReflectMagical =>
                $"[combat] Reflected magical damage ({SourceName} - {SourceCastableName}): {Amount}",
            CombatLogEventType.ReflectPhysical =>
                $"[combat] Reflected magical damage ({SourceName} - {SourceCastableName}): {Amount}",
            CombatLogEventType.LifeSteal => $"[combat] Life steal ({SourceName} - {SourceCastableName}): {Amount}",
            CombatLogEventType.ManaSteal => $"[combat] Mana steal ({SourceName} - {SourceCastableName}): {Amount}",
            _ => string.Empty
        };
    }

}
[MoonSharpUserData]
public record DodgeEvent : CombatEvent
{
    public override CombatLogEventType EventType => CombatLogEventType.Dodge;
}

[MoonSharpUserData]
public record HealEvent : StatChangeEvent
{
    public override CombatLogEventType EventType => CombatLogEventType.Heal;
    public override string ToString() => $"[combat] Heal: {SourceCastableName} on {TargetName}: {Amount}";
}

[MoonSharpUserData]
public record DamageEvent : StatChangeEvent
{
    public DamageFlags Flags;
    public DamageType Type;

    public override CombatLogEventType EventType => CombatLogEventType.Damage;

    // Element of incoming damage
    public ElementType Element { get; set; } = ElementType.None;

    // The "natural interaction" bonus or penalty (say air v fire)
    public uint ElementalInteraction { get; set; } = 0;

    // The amount of additional resistance or penalty from equipment/statuses/etc
    public uint ElementalResisted { get; set; } = 0;

    // The amount of damage resisted or amplified by MR 
    public uint MagicResisted { get; set; } = 0;

    // Amount of damage reduced by AC
    public uint ArmorReduction { get; set; } = 0;

    // Is this a critical hit?
    public bool Crit { get; set; } = false;

    // Is this a magic critical hit?
    public bool MagicCrit { get; set; } = false;

    // Amount of damage buffed or debuffed by DMG
    public uint BonusDmg { get; set; } = 0;

    // Was the damage actually applied or was the target immune?
    public bool Applied { get; set; } = true;

    public override string ToString()
    {
        var str =
            $"Damage: {SourceName}: {SourceCastableName} on {TargetName} ({Element}, {Type})\n  [{(Applied ? "Applied" : "Immune")}] {Amount} dmg, ER {ElementalResisted}, MR {MagicResisted}, AC {ArmorReduction}";
        if (Crit || MagicCrit)
            str = $"CRIT {str}";
        return $"[combat] {str}";
    }
}