using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Castables;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Statuses;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hybrasyl
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class Prohibited : Attribute
    {
        public List<PlayerFlags> Flags { get; set; }
        public List<CreatureCondition> Conditions { get; set; }

        public Prohibited(params object[] prohibited)
        {
            Flags = new List<PlayerFlags>();
            Conditions = new List<CreatureCondition>();

            foreach (var parameter in prohibited)
            {
                if (parameter.GetType() == typeof(PlayerFlags))
                    Flags.Add((PlayerFlags) parameter);
                if (parameter.GetType() == typeof(CreatureCondition))
                    Conditions.Add((CreatureCondition) parameter);
            }
        }

        public bool Check(ConditionInfo condition)
        {
            foreach (var flag in Flags) 
                if (condition.Flags.HasFlag(flag)) return false;

            foreach (var cond in Conditions)
                if (condition.Conditions.HasFlag(cond)) return false;

            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class Required : Attribute
    {
        public List<PlayerFlags> Flags { get; set; }
        public List<CreatureCondition> Conditions { get; set; }

        public Required(params object[] prohibited)
        {
            Flags = new List<PlayerFlags>();
            Conditions = new List<CreatureCondition>();

            foreach (var parameter in prohibited)
            {
                if (parameter.GetType() == typeof(PlayerFlags))
                    Flags.Add((PlayerFlags)parameter);
                if (parameter.GetType() == typeof(CreatureCondition))
                    Conditions.Add((CreatureCondition)parameter);
            }
        }

        public bool Check(ConditionInfo condition)
        {
            foreach (var flag in Flags)
                if (condition.Flags.HasFlag(flag)) return true;

            foreach (var cond in Conditions)
                if (condition.Conditions.HasFlag(cond)) return true;

            return false;
        }
    }
    
    
    public interface ICreatureStatus
    {
        string Name { get; }
        string ActionProhibitedMessage { get; }
        double Duration { get; }
        double Tick { get; }
        DateTime Start { get; }
        DateTime LastTick { get; }
        ushort Icon { get; }

        StatusInfo Info { get; }
        Creature Target { get; }
        Creature Source { get; }
        bool Expired { get; }
        double Elapsed { get; }
        double Remaining { get; }
        double ElapsedSinceTick { get; }
        string UseCastRestrictions { get; }
        string ReceiveCastRestrictions { get; }
        void OnStart(bool displaySfx = true);
        void OnTick();
        void OnEnd();

        SimpleStatusEffect OnStartEffect { get; }
        SimpleStatusEffect OnTickEffect { get; }
        SimpleStatusEffect OnEndEffect { get; }
    }

    public class StatusInfo
    {
        public string Name { get; set; }
        public SimpleStatusEffect OnStartEffect { get; set; }
        public SimpleStatusEffect OnTickEffect { get; set; }
        public SimpleStatusEffect OnEndEffect { get; set; }
        public double Remaining { get; set; }
        public double Tick { get; set; }
    }

    public class SimpleStatusEffect
    {
        double Heal { get; set; }
        DamageOutput Damage { get; set; }

        public SimpleStatusEffect(double heal, DamageOutput damage)
        {
            Heal = heal;
            Damage = damage;
        }
    }

    public class CreatureStatus : ICreatureStatus
    {
        public static readonly ILog Logger =
           LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly ILog ActivityLogger = LogManager.GetLogger("UserActivityLogger");

        public string Name => XmlStatus.Name;
        public ushort Icon => XmlStatus.Icon;
        public double Tick => _tickOverride == -1 ? XmlStatus.Tick : _tickOverride;
        public double Duration => _durationOverride == -1 ? XmlStatus.Duration : _durationOverride;
        public string UseCastRestrictions => XmlStatus.CastRestriction?.Use ?? string.Empty;
        public string ReceiveCastRestrictions => XmlStatus.CastRestriction?.Receive ?? string.Empty;

        public StatusInfo Info => new StatusInfo() { Name = Name, OnStartEffect = OnStartEffect, OnEndEffect = OnEndEffect, OnTickEffect = OnTickEffect, Remaining = Remaining, Tick = Tick};

        private double _durationOverride;
        private double _tickOverride;

        public Creature Target { get; }
        public Creature Source { get; }
        protected User User => Target as User;

        public Conditions ConditionChanges => XmlStatus.Effects?.OnApply?.Conditions;

        public DateTime Start { get; }

        public DateTime LastTick { get; private set; }

        public Castable Castable { get; set; }
        public Status XmlStatus { get; set; }
        public string ActionProhibitedMessage { get; set; }

        public void OnStart(bool displaySfx = true) => _processStart(displaySfx);
        public void OnEnd() => _processRemove();
        public void OnTick() => _processTick();

        public SimpleStatusEffect OnTickEffect { get; }
        public SimpleStatusEffect OnStartEffect { get; }
        public SimpleStatusEffect OnEndEffect { get; }

        public bool Expired => (DateTime.Now - Start).TotalSeconds >= Duration;
        public double Elapsed => (DateTime.Now - Start).TotalSeconds;
        public double Remaining => Duration - Elapsed;

        public double ElapsedSinceTick => (DateTime.Now - LastTick).TotalSeconds;

        public CreatureStatus(Status xmlstatus, Creature target, Castable castable = null, Creature source=null, int durationOverride = -1, int tickOverride = -1)
        {
            Target = target;
            XmlStatus = xmlstatus;
            Start = DateTime.Now;
            _durationOverride = durationOverride;
            _tickOverride = tickOverride;
            // Calculate damage/heal effects. Note that a castable MUST be passed here for a status 
            // to have damage effects as the castable itself has fields we need to access 
            // (intensity, etc) in order to do damage calculations.

            if (castable != null)
            {
                var start = CalculateNumericEffects(castable, xmlstatus.Effects.OnApply, source);
                var tick = CalculateNumericEffects(castable, xmlstatus.Effects.OnTick, source);
                var end = CalculateNumericEffects(castable, xmlstatus.Effects.OnRemove, source);
                OnStartEffect = new SimpleStatusEffect(start.Heal, start.Damage);
                OnTickEffect = new SimpleStatusEffect(tick.Heal, tick.Damage);
                OnEndEffect = new SimpleStatusEffect(end.Heal, end.Damage);
            }
        }

        public CreatureStatus(StatusInfo serialized, Creature target)
        {
            Target = target;
            if (!string.IsNullOrEmpty(serialized.Name))
            {
                if (Game.World.WorldData.TryGetValueByIndex(serialized.Name, out Status status))
                {
                    XmlStatus = status;
                    Start = DateTime.Now;
                    _durationOverride = serialized.Remaining;
                    _tickOverride = serialized.Tick;
                    OnTickEffect = serialized.OnTickEffect;
                    OnEndEffect = serialized.OnEndEffect;
                    OnStartEffect = serialized.OnStartEffect;
                }
                else
                {
                    throw new ArgumentException($"Serialized status {serialized.Name} does not exist or could not be found");
                }
            }
        }

        private void ProcessSfx(ModifierEffect effect)
        {
            if (effect.Sound?.Id != 0)
                User?.PlaySound(effect.Sound.Id);
            if (effect.Animations != null)
            {
                if (effect.Animations?.Target?.Id != 0)
                {
                    var animation = effect.Animations.Target;
                    if (Target is Monster || !Target.Condition.Comatose || (Target.Condition.Comatose && animation.Id == (Game.Config.Handlers?.Death?.Coma?.Effect ?? 24)))
                        Target.Effect(effect.Animations.Target.Id, effect.Animations.Target.Speed);
                }
                if (effect.Animations?.SpellEffect?.Id != 0)
                {
                    Source?.Effect(effect.Animations.SpellEffect.Id, effect.Animations.SpellEffect.Speed);
                }
            }
            // Message handling
            if (effect.Messages != null && User != null)
            {
                if (effect.Messages?.Target != null)
                    User.SendSystemMessage(string.Format(effect.Messages.Target, User.Name));
                if (effect.Messages?.Group != null)
                    User.Group.SendMessage(string.Format(effect.Messages.Group, User.Name));
                if (effect.Messages?.Source != null)
                    (Source as User)?.SendSystemMessage(string.Format(effect.Messages.Source, User.Name));
                if (effect.Messages?.Say != null)
                    User?.Say(string.Format(effect.Messages.Say, User.Name));
                if (effect.Messages?.Shout != null)
                    User?.Say(string.Format(effect.Messages.Shout, User.Name));
            }
        }


        private void ProcessConditions(ModifierEffect effect)
        {
            if (effect.Conditions?.Set != null)
                Target.Condition.Conditions |= effect.Conditions.Set;
            if (effect.Conditions?.Unset != null)
                Target.Condition.Conditions &= ~effect.Conditions.Unset;
        }

        private void ProcessStatModifiers(Statuses.StatModifiers effect, bool remove = false)
        {
            if (effect == null) return;

            if (remove)
            {
                Target.Stats.BonusStr -= effect.Str;
                Target.Stats.BonusInt -= effect.Int;
                Target.Stats.BonusWis -= effect.Wis;
                Target.Stats.BonusCon -= effect.Con;
                Target.Stats.BonusDex -= effect.Dex;
                Target.Stats.BonusHp -= effect.Hp;
                Target.Stats.BonusMp -= effect.Mp;
                Target.Stats.BonusHit -= effect.Hit;
                Target.Stats.BonusDmg -= effect.Dmg;
                Target.Stats.BonusAc -= effect.Ac;
                Target.Stats.BonusRegen -= effect.Regen;
                Target.Stats.BonusMr -= effect.Mr;
                Target.Stats.BonusDamageModifier = effect.DamageModifier;
                Target.Stats.BonusHealModifier = effect.HealModifier;
                Target.Stats.BonusReflectChance -= effect.ReflectChance;
                Target.Stats.BonusReflectIntensity -= effect.ReflectIntensity;
                if (effect.OffensiveElement == (Statuses.Element)Target.Stats.OffensiveElementOverride)
                    Target.Stats.OffensiveElementOverride = Enums.Element.None;
                if (effect.DefensiveElement == (Statuses.Element)Target.Stats.DefensiveElementOverride)
                    Target.Stats.DefensiveElementOverride = Enums.Element.None;
                Target.Stats.BonusAc -= effect.Str;
            }
            else
            {
                Target.Stats.BonusStr += effect.Str;
                Target.Stats.BonusInt += effect.Int;
                Target.Stats.BonusWis += effect.Wis;
                Target.Stats.BonusCon += effect.Con;
                Target.Stats.BonusDex += effect.Dex;
                Target.Stats.BonusHp += effect.Hp;
                Target.Stats.BonusMp += effect.Mp;
                Target.Stats.BonusHit += effect.Hit;
                Target.Stats.BonusDmg += effect.Dmg;
                Target.Stats.BonusAc += effect.Ac;
                Target.Stats.BonusRegen += effect.Regen;
                Target.Stats.BonusMr += effect.Mr;
                Target.Stats.BonusDamageModifier = effect.DamageModifier;
                Target.Stats.BonusHealModifier = effect.HealModifier;
                Target.Stats.BonusReflectChance += effect.ReflectChance;
                Target.Stats.BonusReflectIntensity += effect.ReflectIntensity;
                Target.Stats.BonusAc += effect.Str;
                Target.Stats.OffensiveElementOverride = (Enums.Element)effect.OffensiveElement;
                Target.Stats.DefensiveElementOverride = (Enums.Element)effect.OffensiveElement;

            }
        }

        private (double Heal, DamageOutput Damage) CalculateNumericEffects(Castable castable, ModifierEffect effect, Creature source)
        {
            double heal = 0;
            DamageOutput dmg = new DamageOutput();
            if (!effect.Heal.IsEmpty)
            {
                heal = NumberCruncher.CalculateHeal(castable, effect, Target, source, Name);
            }
            if (!effect.Damage.IsEmpty)
            {
                dmg = NumberCruncher.CalculateDamage(Castable, effect, Target, Source, Name);
               //      if (dmg.Amount != 0) Target.Damage(dmg.Amount, Enums.Element.None, dmg.Type);
            }
            return (heal, dmg);
        }

        private void ProcessNumericEffects(SimpleStatusEffect effect)
        {

        }
        private void ProcessFullEffects(ModifierEffect effect, bool RemoveStatBonuses = false, bool displaySfx = true)
        {
            // Stat modifiers and condition changes are only processed during start/remove
            ProcessConditions(effect);
            ProcessStatModifiers(XmlStatus.Effects?.OnApply?.StatModifiers, RemoveStatBonuses);
            if (displaySfx)
                ProcessSfx(effect);
        }

        private void ProcessEffects(ModifierEffect effect)
        {
            ProcessSfx(effect);
        }

        private void _processStart(bool displaySfx)
        {
            ProcessFullEffects(XmlStatus.Effects?.OnApply, false, displaySfx);
            ProcessNumericEffects(OnStartEffect);
        }

        private void _processTick()
        {
            ProcessEffects(XmlStatus.Effects.OnTick);
            ProcessNumericEffects(OnTickEffect);
        }

        private void _processRemove()
        {
            ProcessFullEffects(XmlStatus.Effects?.OnRemove, true);
            ProcessNumericEffects(OnEndEffect);
        }

    }
}