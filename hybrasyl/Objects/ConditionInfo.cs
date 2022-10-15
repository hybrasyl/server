/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Enums;
using Hybrasyl.Xml;
using Newtonsoft.Json;

namespace Hybrasyl.Objects;

[JsonObject(MemberSerialization.OptIn)]
public class ConditionInfo
{
    public ConditionInfo(Creature owner, CreatureCondition condition = 0, PlayerFlags flags = PlayerFlags.Alive)
    {
        Creature = owner;
        Conditions = condition;
        Flags = flags;
    }

    public Creature Creature { get; set; }
    public User User => Creature as User;

    [JsonProperty] public CreatureCondition Conditions { get; set; }

    [JsonProperty] public PlayerFlags Flags { get; set; }

    public bool CastingAllowed
    {
        get
        {
            var conditionCheck = Asleep || Frozen || Comatose;

            if (User != null)
                conditionCheck = conditionCheck || Flags.HasFlag(PlayerFlags.ProhibitCast);
            return !conditionCheck;
        }
    }

    public bool MovementAllowed
    {
        get
        {
            var conditionCheck = Asleep || Frozen || Paralyzed || Comatose;
            return !conditionCheck;
        }
    }

    public bool IsAttackable
    {
        get
        {
            if (User != null)
                return PvpEnabled;
            if (Creature is Merchant) return false;
            return true;
        }
    }

    public bool Alive
    {
        get => Flags.HasFlag(PlayerFlags.Alive);
        set
        {
            if (value == false)
                Flags &= ~PlayerFlags.Alive;
            else
                Flags |= PlayerFlags.Alive;
            User?.UpdateAttributes(StatUpdateFlags.Secondary);
        }
    }

    public bool Frozen
    {
        get => Conditions.HasFlag(CreatureCondition.Freeze);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Freeze;
            else
                Conditions |= CreatureCondition.Freeze;
        }
    }

    public bool Asleep
    {
        get => Conditions.HasFlag(CreatureCondition.Sleep);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Sleep;
            else
                Conditions |= CreatureCondition.Freeze;
        }
    }

    public bool Paralyzed
    {
        get => Conditions.HasFlag(CreatureCondition.Paralyze);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Paralyze;
            else
                Conditions |= CreatureCondition.Paralyze;
            User?.UpdateAttributes(StatUpdateFlags.Secondary);
        }
    }

    public bool Blinded
    {
        get => Conditions.HasFlag(CreatureCondition.Blind);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Blind;
            else
                Conditions |= CreatureCondition.Blind;
            User?.UpdateAttributes(StatUpdateFlags.Secondary);
        }
    }

    public bool PvpEnabled
    {
        get => Flags.HasFlag(PlayerFlags.Pvp);
        set
        {
            if (value == false)
                Flags &= ~PlayerFlags.Pvp;
            else
                Flags |= PlayerFlags.Pvp;
        }
    }

    public bool Casting
    {
        get => Flags.HasFlag(PlayerFlags.Casting);
        set
        {
            if (value == false)
                Flags &= ~PlayerFlags.Casting;
            else
                Flags |= PlayerFlags.Casting;
        }
    }

    public bool Muted
    {
        get => Conditions.HasFlag(CreatureCondition.Mute);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Mute;
            else
                Conditions |= CreatureCondition.Mute;
        }
    }

    public bool SeeInvisible
    {
        get => Conditions.HasFlag(CreatureCondition.Sight);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Sight;
            else
                Conditions |= CreatureCondition.Sight;
        }
    }

    public bool IsInvisible
    {
        get => Conditions.HasFlag(CreatureCondition.Invisible);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Invisible;
            else
                Conditions |= CreatureCondition.Invisible;
        }
    }

    public bool IsInvulnerable
    {
        get => Conditions.HasFlag(CreatureCondition.Invulnerable);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Invulnerable;
            else
                Conditions |= CreatureCondition.Invulnerable;
        }
    }

    public bool Disoriented
    {
        get => Conditions.HasFlag(CreatureCondition.Disoriented);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Invulnerable;
            else
                Conditions |= CreatureCondition.Invulnerable;
        }
    }

    // The following apply to users only

    public bool Comatose
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.Coma);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= ~CreatureCondition.Coma;
            else
                Conditions |= CreatureCondition.Coma;
        }
    }

    public bool InExchange
    {
        get => User != null && Flags.HasFlag(PlayerFlags.InExchange);
        set
        {
            if (User == null) return;
            if (value == false)
                Flags &= ~PlayerFlags.InExchange;
            else
                Flags |= PlayerFlags.InExchange;
        }
    }

    public bool IsItemUseProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitItemUse);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitItemUse;
            else
                Conditions |= CreatureCondition.ProhibitItemUse;
        }
    }

    public bool IsSayProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitSpeech);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitSpeech;
            else
                Conditions |= CreatureCondition.ProhibitSpeech;
        }
    }

    public bool IsShoutProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitEquipChange);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitShout;
            else
                Conditions |= CreatureCondition.ProhibitShout;
        }
    }

    public bool IsWhisperProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitWhisper);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitWhisper;
            else
                Conditions |= CreatureCondition.ProhibitWhisper;
        }
    }

    public bool IsEquipmentChangeProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitEquipChange);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitEquipChange;
            else
                Conditions |= CreatureCondition.ProhibitEquipChange;
        }
    }

    public bool NoFlags => Flags == PlayerFlags.Alive;

    public void ClearFlags()
    {
        Flags = PlayerFlags.Alive;
    }

    public void ClearConditions()
    {
        Conditions = 0;
    }
}