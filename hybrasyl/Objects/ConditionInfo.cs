using Hybrasyl.Enums;
using Hybrasyl.Statuses;
using Newtonsoft.Json;

namespace Hybrasyl.Objects
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ConditionInfo
    {
        public Creature Creature { get; set; }
        public User User => Creature as User;

        [JsonProperty]
        public CreatureCondition Conditions { get; set; }

        [JsonProperty]
        public PlayerFlags Flags { get; set; }

        public ConditionInfo(Creature owner, CreatureCondition condition = 0, PlayerFlags flags=PlayerFlags.Alive)
        {
            Creature = owner;
            Conditions = condition;
            Flags = flags;
        }

        public bool CastingAllowed
        {
            get
            {
                var conditionCheck = Asleep || Frozen || Paralyzed || Comatose;

                if (User != null)
                    conditionCheck = conditionCheck || Flags.HasFlag(PlayerFlags.ProhibitCast);
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
            get { return Conditions.HasFlag(CreatureCondition.Freeze); }
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
            get { return Conditions.HasFlag(CreatureCondition.Sleep); }
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
            get { return Conditions.HasFlag(CreatureCondition.Paralyze); }
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
            get { return Conditions.HasFlag(CreatureCondition.Blind); }
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
            get { return User != null ? Conditions.HasFlag(CreatureCondition.Coma) : false; }
            set
            {
                if (User == null) return;
                if (value == false)
                {
                    Conditions &= ~CreatureCondition.Coma;
                    User?.Group?.SendMessage($"{User.Name} has recovered!");
                }
                else
                    Conditions |= CreatureCondition.Coma;
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
