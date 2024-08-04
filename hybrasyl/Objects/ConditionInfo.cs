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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Xml.Objects;
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

    private CreatureCondition _Conditions { get; set; }

    [JsonProperty]
    public CreatureCondition Conditions
    {
        get => _Conditions;
        set
        {
            var previous = _Conditions;
            _Conditions = value;
            if (User?.Map == null) return;
            Game.World.EnqueueUserUpdate(User.Guid);
            if ((value.HasFlag(CreatureCondition.Invisible) && !previous.HasFlag(CreatureCondition.Invisible)) ||
                (!value.HasFlag(CreatureCondition.Invisible) && previous.HasFlag(CreatureCondition.Invisible)))
                Game.World.EnqueueShowTo(User.Guid);
        }
    }

    [JsonProperty] public PlayerFlags Flags { get; set; }

    public bool CastingAllowed
    {
        get
        {
            var conditionCheck = Asleep || Stunned || Comatose;

            if (User != null)
                conditionCheck = conditionCheck || Flags.HasFlag(PlayerFlags.ProhibitCast);
            return !conditionCheck;
        }
    }

    public bool MovementAllowed
    {
        get
        {
            var conditionCheck = Asleep || Stunned || Rooted || Comatose;
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
        }
    }

    public bool Stunned
    {
        get => Conditions.HasFlag(CreatureCondition.Stun);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Stun;
            else
                Conditions |= CreatureCondition.Stun;
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
                Conditions |= CreatureCondition.Stun;
        }
    }

    public bool Rooted
    {
        get => Conditions.HasFlag(CreatureCondition.Root);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Root;
            else
                Conditions |= CreatureCondition.Root;
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
                Conditions &= ~CreatureCondition.Disoriented;
            else
                Conditions |= CreatureCondition.Disoriented;
        }
    }

    public bool Feared
    {
        get => Conditions.HasFlag(CreatureCondition.Fear);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Fear;
            else
                Conditions |= CreatureCondition.Fear;
        }
    }

    public bool Disarmed
    {
        get => Conditions.HasFlag(CreatureCondition.Disarm);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Disarm;
            else
                Conditions |= CreatureCondition.Disarm;
        }
    }

    public bool Charmed
    {
        get => Conditions.HasFlag(CreatureCondition.Charm);
        set
        {
            if (value == false)
                Conditions &= ~CreatureCondition.Charm;
            else
                Conditions |= CreatureCondition.Charm;
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

    public bool IsHpIncreaseProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitHpIncrease);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitHpIncrease;
            else
                Conditions |= CreatureCondition.ProhibitHpIncrease;
        }
    }

    public bool IsMpIncreaseProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitMpIncrease);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitMpIncrease;
            else
                Conditions |= CreatureCondition.ProhibitMpIncrease;
        }
    }

    public bool IsMpDecreaseProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitMpDecrease);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitMpDecrease;
            else
                Conditions |= CreatureCondition.ProhibitMpDecrease;
        }
    }

    public bool IsHpRegenProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitHpRegen);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitHpRegen;
            else
                Conditions |= CreatureCondition.ProhibitHpRegen;
        }
    }

    public bool IsMpRegenProhibited
    {
        get => User != null && Conditions.HasFlag(CreatureCondition.ProhibitMpRegen);
        set
        {
            if (User == null) return;
            if (value == false)
                Conditions &= CreatureCondition.ProhibitMpRegen;
            else
                Conditions |= CreatureCondition.ProhibitMpRegen;
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