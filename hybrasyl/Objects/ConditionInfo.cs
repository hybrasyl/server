using Hybrasyl.Enums;
using Hybrasyl.Statuses;
using Newtonsoft.Json;

namespace Hybrasyl.Objects
{
    public class ConditionInfo
    {
        public Creature Creature { get; set; }
        public User User => Creature as User;

        [JsonProperty]
        public CreatureCondition Condition { get; set; }

        [JsonProperty]
        public PlayerFlags Flags { get; set; }

        private void _initialize()
        {
            Condition = 0;
            Flags = PlayerFlags.Alive;
        }
        public ConditionInfo(Creature owner)
        {
            Creature = owner;
            _initialize();
        }

        public ConditionInfo(User user)
        {
            Creature = User as Creature;
            _initialize();
        }

        public bool CanCast()
        {
            var conditionCheck = Condition.HasFlag(CreatureCondition.Sleep) ||
            Condition.HasFlag(CreatureCondition.Freeze) ||
            Condition.HasFlag(CreatureCondition.Paralyze) ||
            Condition.HasFlag(CreatureCondition.Coma);

            if (User != null)
            {
                var flagCheck = Flags.HasFlag(PlayerFlags.InDialog) ||
                Flags.HasFlag(PlayerFlags.InExchange) ||
                Flags.HasFlag(PlayerFlags.AliveExchange);
                return conditionCheck || flagCheck;
            }
            return conditionCheck;
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
            get { return Condition.HasFlag(CreatureCondition.Freeze); }
            set
            {
                if (value == false)
                    Condition &= ~CreatureCondition.Freeze;
                else
                    Condition |= CreatureCondition.Freeze;
            }
        }

        public bool Asleep
        {
            get { return Condition.HasFlag(CreatureCondition.Sleep); }
            set
            {
                if (value == false)
                    Condition &= ~CreatureCondition.Sleep;
                else
                    Condition |= CreatureCondition.Freeze;
            }
        }

        public bool Paralyzed
        {
            get { return Condition.HasFlag(CreatureCondition.Paralyze); }
            set
            {
                if (value == false)
                    Condition &= ~CreatureCondition.Paralyze;
                else
                    Condition |= CreatureCondition.Paralyze;
                User?.UpdateAttributes(StatUpdateFlags.Secondary);
            }
        }

        public bool Blinded
        {
            get { return Condition.HasFlag(CreatureCondition.Blind); }
            set
            {
                if (value == false)
                    Condition &= ~CreatureCondition.Blind;
                else
                    Condition |= CreatureCondition.Blind;
                User?.UpdateAttributes(StatUpdateFlags.Secondary);
            }
        }

        public bool PvpEnabled
        {
            get { return Flags.HasFlag(PlayerFlags.Pvp); }
            set
            {
                if (value == false)
                    Flags &= PlayerFlags.Pvp;
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
                    Flags &= PlayerFlags.Casting;
                else
                    Flags |= PlayerFlags.Casting;
            }
        }

        // The following apply to users only

        public bool Comatose
        {
            get { return User != null ? Condition.HasFlag(CreatureCondition.Coma) : false; }
            set
            {
                if (User == null) return;
                if (value == false)
                {
                    Condition &= ~CreatureCondition.Coma;
                    User?.Group?.SendMessage($"{User.Name} has recovered!");
                }
                else
                    Condition |= CreatureCondition.Coma;
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
            Condition = 0;
        }
    }
}
