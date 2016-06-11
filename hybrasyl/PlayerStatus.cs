
using System;
using System.Data.SqlTypes;
using Community.CsharpSqlite;
using Hybrasyl.Enums;
using Hybrasyl.Objects;

namespace Hybrasyl
{
    public interface IPlayerStatus
    {

        string OnTickMessage { get; set; }
        string OnStartMessage { get; set; }
        string OnEndMessage { get; set; }
        string Name { get; }
        int Duration { get; set; }
        int Tick { get; set; }
        DateTime Start { get; }
        DateTime LastTick { get; }
        Enums.PlayerCondition Conditions { get; set; }
        ushort Icon { get; }

        bool Expired { get; }

        double ElapsedSinceTick { get; }

        void OnStart();
        void OnTick();
        void OnEnd();

        int GetHashCode();
    }

    public abstract class PlayerStatus : IPlayerStatus
    {
        public string Name { get; }
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

        public double ElapsedSinceTick => (DateTime.Now - LastTick).TotalSeconds;

        public string OnTickMessage { get; set; }
        public string OnStartMessage { get; set; }
        public string OnEndMessage { get; set; }

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

    internal class BlindEffect : PlayerStatus
    {

        private new const ushort Icon = 3;
        private new const string Name = "Blind";
        private new const string OnStartMessage = "You cannot see anything.";
        private new const string OnEndMessage = "You can see again.";

        public BlindEffect(User user, int duration, int tick) : base(user, duration, tick, Icon, Name)
        {
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

    internal class PoisonEffect : PlayerStatus
    {
        private new const ushort Icon = 36;
        private new const string Name = "Poison";
        private const ushort OnTickEffect = 25;

        private new const string OnStartMessage = "Poison";
        private new const string OnTickMessage = "Poison is coursing through your veins.";
        private new const string OnEndMessage = "You feel better.";

        private readonly double _damagePerTick;

        public PoisonEffect(User user, int duration, int tick, double damagePerTick) : base(user, duration, tick, Icon, Name)
        {
            _damagePerTick = damagePerTick;
        }

        public override void OnStart()
        {
            base.OnStart();
            User.SendEffect(User.Id, OnTickEffect, 120);
        }

        public override void OnTick()
        {
            base.OnTick();
            User.SendEffect(User.Id, OnTickEffect, 120);
            User.Damage(_damagePerTick);
        }
    }

    internal class ParalyzeEffect : PlayerStatus
    {
        private new const ushort Icon = 36;
        private new const string Name = "Paralyze";
        private const ushort OnTickEffect = 25;

        private new const string OnStartMessage = "You are in hibernation.";
        private new const string OnEndMessage = "Your body thaws.";

        public ParalyzeEffect(User user, int duration, int tick) : base(user, duration, tick, Icon, Name)
        {
        }

        public override void OnStart()
        {
            base.OnStart();
            User.SendEffect(User.Id, OnTickEffect, 1);
            User.ToggleParalyzed();
        }
        public override void OnEnd()
        {
            base.OnEnd();
            User.ToggleParalyzed();
        }

    }

    internal class SleepEffect : PlayerStatus
    {
        private new const ushort Icon = 36;
        private new const string Name = "Sleep";
        private const ushort OnTickEffect = 25;

        private new const string OnStartMessage = "You are asleep.";
        private new const string OnEndMessage = "You awaken.";

        public SleepEffect(User user, int duration, int tick) : base(user, duration, tick, Icon, Name)
        {
        }

        public override void OnStart()
        {
            base.OnStart();
            User.SendEffect(User.Id, OnTickEffect, 1);
            User.ToggleParalyzed();
        }

        public override void OnTick()
        {
            base.OnTick();
            User.SendEffect(User.Id, OnTickEffect, 1);
        }

    }

    /*
        internal class CastableEffect : PlayerEffect
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
