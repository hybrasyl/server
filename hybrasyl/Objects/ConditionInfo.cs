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
using Newtonsoft.Json;

namespace Hybrasyl.Objects
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ConditionInfo
    {
        public Creature Creature { get; set; }
        public User User => Creature as User;

        [JsonProperty]
        public Xml.CreatureCondition Conditions { get; set; }

        [JsonProperty]
        public PlayerFlags Flags { get; set; }

        public ConditionInfo(Creature owner, Xml.CreatureCondition condition = 0, PlayerFlags flags=PlayerFlags.Alive)
        {
            Creature = owner;
            Conditions = condition;
            Flags = flags;
        }

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
                else
                // TODO: expand / refactor? We may want non-merchant mobs that can't be attacked?
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
            get => Conditions.HasFlag(Xml.CreatureCondition.Freeze);
            set
            {
                if (value == false)
                    Conditions &= ~Xml.CreatureCondition.Freeze;
                else
                    Conditions |= Xml.CreatureCondition.Freeze;
            }
        }

        public bool Asleep
        {
            get => Conditions.HasFlag(Xml.CreatureCondition.Sleep);
            set
            {
                if (value == false)
                    Conditions &= ~Xml.CreatureCondition.Sleep;
                else
                    Conditions |= Xml.CreatureCondition.Freeze;
            }
        }

        public bool Paralyzed
        {
            get => Conditions.HasFlag(Xml.CreatureCondition.Paralyze);
            set
            {
                if (value == false)
                    Conditions &= ~Xml.CreatureCondition.Paralyze;
                else
                    Conditions |= Xml.CreatureCondition.Paralyze;
                User?.UpdateAttributes(StatUpdateFlags.Secondary);
            }
        }

        public bool Blinded
        {
            get => Conditions.HasFlag(Xml.CreatureCondition.Blind);
            set
            {
                if (value == false)
                    Conditions &= ~Xml.CreatureCondition.Blind;
                else
                    Conditions |= Xml.CreatureCondition.Blind;
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
            get => Conditions.HasFlag(Xml.CreatureCondition.Mute);
            set
            {
                if (value == false)
                    Conditions &= ~Xml.CreatureCondition.Mute;
                else
                    Conditions |= Xml.CreatureCondition.Mute;
            }
        }

        public bool SeeInvisible
        {
            get => Conditions.HasFlag(Xml.CreatureCondition.Sight);
            set
            {
                if (value == false)
                    Conditions &= ~Xml.CreatureCondition.Sight;
                else
                    Conditions |= Xml.CreatureCondition.Sight;
            }
        }

        public bool IsInvisible
        {
            get => Conditions.HasFlag(Xml.CreatureCondition.Invisible);
            set
            {
                if (value == false)
                    Conditions &= ~Xml.CreatureCondition.Invisible;
                else
                    Conditions |= Xml.CreatureCondition.Invisible;
            }
        }

        public bool IsInvulnerable
        {
            get => Conditions.HasFlag(Xml.CreatureCondition.Invulnerable);
            set
            {
                if (value == false)
                    Conditions &= ~Xml.CreatureCondition.Invulnerable;
                else
                    Conditions |= Xml.CreatureCondition.Invulnerable;
            }
        }

        // The following apply to users only

        public bool Comatose
        {
            get => User != null && Conditions.HasFlag(Xml.CreatureCondition.Coma);
            set
            {
                if (User == null) return;
                if (value == false)
                {
                    Conditions &= ~Xml.CreatureCondition.Coma;
                }
                else
                    Conditions |= Xml.CreatureCondition.Coma;
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
            get => User != null && Conditions.HasFlag(Xml.CreatureCondition.ProhibitItemUse);
            set
            {
                if (User == null) return;
                if (value == false)
                    Conditions &= Xml.CreatureCondition.ProhibitItemUse;
                else
                    Conditions |= Xml.CreatureCondition.ProhibitItemUse;
            }
        }

        public bool IsEquipmentChangeProhibited
        {
            get => User != null && Conditions.HasFlag(Xml.CreatureCondition.ProhibitEquipChange);
            set
            {
                if (User == null) return;
                if (value == false)
                    Conditions &= Xml.CreatureCondition.ProhibitEquipChange;
                else
                    Conditions |= Xml.CreatureCondition.ProhibitEquipChange;
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
}
