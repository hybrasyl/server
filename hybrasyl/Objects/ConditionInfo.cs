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
            get { return Flags.HasFlag(PlayerFlags.Alive); }
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
            get { return Conditions.HasFlag(Xml.CreatureCondition.Freeze); }
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
            get { return Conditions.HasFlag(Xml.CreatureCondition.Sleep); }
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
            get { return Conditions.HasFlag(Xml.CreatureCondition.Paralyze); }
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
            get { return Conditions.HasFlag(Xml.CreatureCondition.Blind); }
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
            get { return Flags.HasFlag(PlayerFlags.Pvp); }
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
            get { return Flags.HasFlag(PlayerFlags.Casting); }
            set
            {
                if (value == false)
                    Flags &= ~PlayerFlags.Casting;
                else
                    Flags |= PlayerFlags.Casting;
            }
        }

        // The following apply to users only

        public bool Comatose
        {
            get { return User != null ? Conditions.HasFlag(Xml.CreatureCondition.Coma) : false; }
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
            get { return User != null ? Flags.HasFlag(PlayerFlags.InExchange) : false; }
            set
            {
                if (User == null) return;
                if (value == false)
                    Flags &= ~PlayerFlags.InExchange;
                else
                    Flags |= PlayerFlags.InExchange;
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
