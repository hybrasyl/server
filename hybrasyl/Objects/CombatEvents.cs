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

using Hybrasyl.Enums;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using System.Collections.Generic;
using System.Linq;

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

    // Amount of healing buff / debuff from inbound modifiers
    public int BonusHeal { get; set; } = 0;

    public override string ToString() =>
        $"[combat] Heal: {SourceCastableName} on {TargetName}: {Amount} (modified {BonusHeal})";
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
    public int ElementalInteraction { get; set; } = 0;

    // The amount of additional resistance or penalty from equipment/statuses/etc
    public int ElementalResisted { get; set; } = 0;

    // The amount of additional amplification (augment) or penalty from the attacker's equipment/statuses/etc
    public int ElementalAugmented { get; set; } = 0;

    // The amount of damage resisted or amplified by MR 
    public int MagicResisted { get; set; } = 0;

    // Amount of damage reduced by AC
    public int ArmorReduction { get; set; } = 0;

    // Is this a critical hit?
    public bool Crit { get; set; } = false;

    // Is this a magic critical hit?
    public bool MagicCrit { get; set; } = false;

    // Amount of damage buffed or debuffed by DMG
    public int BonusDmg { get; set; } = 0;

    // Amount of damage buffed or debuffed by damage modifiers
    public int ModifierDmg { get; set; } = 0;

    // Was the damage actually applied or was the target immune?
    public bool Applied { get; set; } = true;

    // How much of the damage was shielded, if any?
    public uint Shielded { get; set; } = 0;

    public override string ToString()
    {
        var str =
            $"Damage: {SourceName}: {SourceCastableName} on {TargetName} ({Element}, {Type})\n  [{(Applied ? "Applied" : "Immune")}] {Amount} dmg";

        if (ElementalResisted > 0)
            str += $" EResist {ElementalResisted}";
        if (ElementalAugmented > 0)
            str += $" EAug {ElementalAugmented}";
        if (MagicResisted > 0)
            str += $" MR {MagicResisted}";
        if (ArmorReduction > 0)
            str += $" AC {ArmorReduction}";
        if (ModifierDmg > 0)
            str += $" Mod {ModifierDmg}";
        if (Shielded > 0)
            str += $" Shield {Shielded}";
        if (Crit || MagicCrit)
            str = $"CRIT {str}";
        return $"[combat] {str}";
    }
}

[MoonSharpUserData]
public record NoLootEvent : CombatEvent
{
    public string Reason { get; set; }
    public override CombatLogEventType EventType => CombatLogEventType.Loot;
    public override string ToString() => $"[LOOT DENIED]: {Target}: {Reason}";
}

[MoonSharpUserData]
public record LootEvent : CombatEvent
{
    public List<string> Items { get; set; } = new();
    public uint Xp { get; set; }
    public uint Gold { get; set; }
    public override CombatLogEventType EventType => CombatLogEventType.Loot;

    public override string ToString()
    {
        var ret = $"[combat] [Loot] XP {Xp} Gold {Gold}\n";
        return Items.Count <= 0 ? ret :
            // Deal with client vagaries
            Items.Aggregate(ret, (current, item) => current + $"[combat] [Loot] Item: {item}\n");
    }
}