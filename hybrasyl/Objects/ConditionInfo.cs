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

        public bool Alive => Flags.HasFlag(PlayerFlags.Alive);
        public bool Frozen => Condition.HasFlag(CreatureCondition.Freeze);
        public bool Asleep => Condition.HasFlag(CreatureCondition.Sleep);
        public bool Paralyzed => Condition.HasFlag(CreatureCondition.Paralyze);
        public bool Poisoned => Condition.HasFlag(CreatureCondition.Poison);
        public bool Blinded => Condition.HasFlag(CreatureCondition.Blind);
        public bool NoFlags => Flags == PlayerFlags.Alive;
        public bool PvpEnabled => Flags.HasFlag(PlayerFlags.Pvp);
        public bool Casting => Flags.HasFlag(PlayerFlags.Casting);

        // The following apply to users only
        public bool Comatose => User != null ? Condition.HasFlag(CreatureCondition.Coma) : false;
        public bool InExchange => User != null ? Flags.HasFlag(PlayerFlags.InExchange) : false;

        public void ClearFlags()
        {
            Flags = PlayerFlags.Alive;
        }

        public void ClearConditions()
        {
            Condition = 0;
        }

        /// <summary>
        /// Toggle whether or not the creature is blind.
        /// </summary>
        public void ToggleBlind()
        {
            Condition ^= CreatureCondition.Blind;
            User?.UpdateAttributes(StatUpdateFlags.Secondary);
        }

        public void ToggleCasting() => Flags ^= PlayerFlags.Casting;

        /// <summary>
        /// Toggle whether or not the creature is paralyzed.
        /// </summary>
        public void ToggleParalyzed()
        {
            Condition ^= CreatureCondition.Paralyze;
            User?.UpdateAttributes(StatUpdateFlags.Secondary);
        }

        /// <summary>
        /// Toggle whether or not the user is near death (in a coma).
        /// </summary>
        public void ToggleNearDeath()
        {
            if (User == null) return;
            if (Condition.HasFlag(CreatureCondition.Coma))
            {
                Condition &= ~CreatureCondition.Coma;
                User?.Group?.SendMessage($"{User.Name} has recovered!");
            }
            else
                Condition |= CreatureCondition.Coma;
        }

        /// <summary>
        /// Toggle whether or not a user is alive.
        /// </summary>
        public void ToggleAlive()
        {
            if (User == null) return;
            Flags ^= PlayerFlags.Alive;
            User?.UpdateAttributes(StatUpdateFlags.Secondary);
        }

        public void SetDead() => Flags &= ~PlayerFlags.Alive;
    }
}
