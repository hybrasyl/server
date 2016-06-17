
using System;
using System.Data.SqlTypes;
using System.Windows.Forms;
using Community.CsharpSqlite;
using Hybrasyl.Enums;
using Hybrasyl.Objects;

namespace Hybrasyl
{

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ProhibitedCondition : System.Attribute
    {
        public PlayerCondition Condition { get; set; }

        public ProhibitedCondition(PlayerCondition requirement)
        {
            Condition = requirement;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RequiredCondition : System.Attribute
    {
        public PlayerCondition Condition { get; set; }
        public string ErrorMessage { get; set; }

        public RequiredCondition(PlayerCondition requirement)
        {
            Condition = requirement;
        }
    }


    public interface IPlayerStatus
    {

        string Name { get; }
        string OnTickMessage { get; set; }
        string OnStartMessage { get; set; }
        string OnEndMessage { get; set; }
        string ActionProhibitedMessage { get; set; }
        int Duration { get; set; }
        int Tick { get; set; }
        DateTime Start { get; }
        DateTime LastTick { get; }
        Enums.PlayerCondition Conditions { get; set; }
        ushort Icon { get; }

        bool Expired { get; }
        double Elapsed { get; }
        double Remaining { get; }
        double ElapsedSinceTick { get; }

        void OnStart();
        void OnTick();
        void OnEnd();

        int GetHashCode();
    }

    public abstract class PlayerStatus : IPlayerStatus
    {
        public string Name { get; set; }
        public ushort Icon { get; }
        public int Tick { get; set; }
        public int Duration { get; set; }
        protected User User { get; set; }
        public Enums.PlayerCondition Conditions { get; set; }

        public DateTime Start { get; }

        public DateTime LastTick { get; private set; }

        public virtual void OnStart()
        {
            if (OnStartMessage != string.Empty) User.SendSystemMessage(OnStartMessage);
        }

        public virtual void OnTick()
        {
            LastTick = DateTime.Now;
            if (OnTickMessage != string.Empty) User.SendSystemMessage(OnTickMessage);
        }

        public virtual void OnEnd()
        {
            if (OnEndMessage != string.Empty) User.SendSystemMessage(OnEndMessage);
        }

        public bool Expired => (DateTime.Now - Start).TotalSeconds >= Duration;

        public double Elapsed => (DateTime.Now - Start).TotalSeconds;
        public double Remaining => Duration - Elapsed;

        public double ElapsedSinceTick => (DateTime.Now - LastTick).TotalSeconds;

        public string OnTickMessage { get; set; }
        public string OnStartMessage { get; set; }
        public string OnEndMessage { get; set; }
        public string ActionProhibitedMessage { get; set; }

        protected PlayerStatus(User user, int duration, int tick, ushort icon, string name)
        {
            User = user;
            Duration = duration;
            Tick = tick;
            Icon = icon;
            Name = name;
            Start = DateTime.Now;
            OnTickMessage = String.Empty;
            OnStartMessage = String.Empty;
            OnEndMessage = String.Empty;

        }
    }

    internal class BlindStatus : PlayerStatus
    {

        public new static ushort Icon = 3;
        public new static string Name = "blind";
        public new static string ActionProhibitedMessage = "You can't see well enough to do that.";

        public BlindStatus(User user, int duration, int tick) : base(user, duration, tick, Icon, Name)
        {
            OnStartMessage = "The world goes dark!";
            OnEndMessage = "You can see again.";
        }

        public override void OnStart()
        {
            base.OnStart();
            User.ToggleBlind();
        }

        public override void OnTick()
        {
        }

        public override void OnEnd()
        {
            base.OnEnd();
            User.ToggleBlind();
        }

    }

    internal class PoisonStatus : PlayerStatus
    {
        private new static ushort Icon = 36;
        public new static string Name = "poison";
        public static ushort OnTickEffect = 25;

        public new const string ActionProhibitedMessage = "You double over in pain.";

        private readonly double _damagePerTick;

        public PoisonStatus(User user, int duration, int tick, double damagePerTick) : base(user, duration, tick, Icon, Name)
        {
            OnStartMessage = "Poison";
            OnTickMessage = "Poison is coursing through your veins.";
            OnEndMessage = "You feel better.";
            _damagePerTick = damagePerTick;
        }

        public override void OnStart()
        {
            base.OnStart();
            if (!User.Status.HasFlag(PlayerCondition.InComa))
                User.Effect(OnTickEffect, 120);
        }

        public override void OnTick()
        {
            base.OnTick();
            if (!User.Status.HasFlag(PlayerCondition.InComa))
                User.Effect(OnTickEffect, 120);
            User.Damage(_damagePerTick);
        }
    }

    internal class ParalyzeStatus : PlayerStatus
    {
        public new static ushort Icon = 36;
        public new static string Name = "paralyze";
        public static ushort OnTickEffect = 41;

        public new const string ActionProhibitedMessage = "You cannot move!";

        public ParalyzeStatus(User user, int duration, int tick) : base(user, duration, tick, Icon, Name)
        {
            OnStartMessage = "You are in hibernation.";
            OnEndMessage = "Your body thaws.";
        }

        public override void OnStart()
        {
            base.OnStart();
            if (!User.Status.HasFlag(PlayerCondition.InComa))
                User.Effect(OnTickEffect, 120);
            User.ToggleParalyzed();
        }
        public override void OnEnd()
        {
            base.OnEnd();
            User.ToggleParalyzed();
        }

    }

    internal class FreezeStatus : PlayerStatus
    {
        public new static ushort Icon = 36;
        public new static string Name = "freeze";
        public static ushort OnTickEffect = 40;

        public new const string ActionProhibitedMessage = "You cannot move!";

        public FreezeStatus(User user, int duration, int tick) : base(user, duration, tick, Icon, Name)
        {
            OnStartMessage = "You are in hibernation.";
            OnEndMessage = "Your body thaws.";
        }

        public override void OnStart()
        {
            base.OnStart();
            if (!User.Status.HasFlag(PlayerCondition.InComa))
                User.Effect(OnTickEffect, 120);
            User.ToggleFreeze();
        }
        public override void OnEnd()
        {
            base.OnEnd();
            User.ToggleFreeze();
        }
    }

    internal class SleepStatus : PlayerStatus
    {
        public new static ushort Icon = 36;
        public new static string Name = "sleep";
        public static ushort OnTickEffect = 28;

        public new const string ActionProhibitedMessage = "You are too sleepy to even raise your hands!";

        public SleepStatus(User user, int duration, int tick) : base(user, duration, tick, Icon, Name)
        {
            OnStartMessage = "You are asleep.";
            OnEndMessage = "You awaken.";
        }

        public override void OnStart()
        {
            base.OnStart();
            if (!User.Status.HasFlag(PlayerCondition.InComa))
                User.Effect(OnTickEffect, 120);
            User.ToggleAsleep();
        }

        public override void OnTick()
        {
            base.OnTick();
            User.Effect(OnTickEffect, 120);
        }
        public override void OnEnd()
        {
            base.OnEnd();
            User.ToggleAsleep();
        }
    }

    internal class NearDeathStatus : PlayerStatus
    {
        public new static ushort Icon = 24;
        public new static string Name = "neardeath";
        public static ushort OnTickEffect = 24;

        public new const string ActionProhibitedMessage = "The life is draining from your body.";


        public NearDeathStatus(User user, int duration, int tick) : base(user, duration, tick, Icon, Name)
        {
            OnStartMessage = "You are near death.";
            OnEndMessage = "You have died!";
        }

        public override void OnStart()
        {
            base.OnStart();
            User.Effect(OnTickEffect, 120);
            User.ToggleNearDeath();
            User.Group?.SendMessage($"{User.Name} is dying!");
        }

        public override void OnTick()
        {
            base.OnTick();
            if (User.Status.HasFlag(PlayerCondition.InComa))
                User.Effect(OnTickEffect, 120);
            if (Remaining < 5)
                User.Group?.SendMessage($"{User.Name}'s soul hangs by a thread!");
        }

        public override void OnEnd()
        {
            base.OnEnd();
            User.OnDeath();
        }
    }

    /*
        internal class CastableStatus : PlayerEffect
        {
            private Script _script;

            public CastableEffect(User user, Script script, int duration = 30000, int ticks = 1000)
                : base(user, duration, ticks)
            {
                _script = script;
                Icon = icon;
            }
        }
        */
}
