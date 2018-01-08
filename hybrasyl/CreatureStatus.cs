using System;
using System.Collections.Generic;
using Hybrasyl.Castables;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Statuses;

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
        int Duration { get; }
        int Tick { get; }
        DateTime Start { get; }
        DateTime LastTick { get; }
        ushort Icon { get; }

        bool Expired { get; }
        double Elapsed { get; }
        double Remaining { get; }
        double ElapsedSinceTick { get; }
        string UseCastRestrictions { get; }
        string ReceiveCastRestrictions { get; }
        void OnStart();
        void OnTick();
        void OnEnd();

    }

    public class CreatureStatus : ICreatureStatus
    {
        public string Name => XMLStatus.Name;
        public string CastableName => Castable?.Name ?? string.Empty;
        public ushort Icon => XMLStatus.Icon;
        public int Tick => _durationOverride != -1 ? XMLStatus.Tick : _durationOverride;
        public int Duration => _tickOverride != -1 ? XMLStatus.Duration : _durationOverride;
        public string UseCastRestrictions => XMLStatus.CastRestriction?.Use ?? string.Empty;
        public string ReceiveCastRestrictions => XMLStatus.CastRestriction?.Receive ?? string.Empty;

        private int _durationOverride;
        private int _tickOverride;

        protected Creature Target { get; set; }
        protected Creature Source { get; set; }
        protected User User => Target as User;

        public Conditions ConditionChanges => XMLStatus.Effects?.OnApply?.Conditions;

        public DateTime Start { get; }

        public DateTime LastTick { get; private set; }

        public Castable Castable { get; set; }
        public Status XMLStatus  { get; set; }
        public string ActionProhibitedMessage { get; set; }

        private void _processStart() => ProcessFullEffects(XMLStatus.Effects?.OnApply);
        private void _processTick() => ProcessEffects(XMLStatus.Effects?.OnTick);
        private void _processRemove() => ProcessFullEffects(XMLStatus.Effects?.OnRemove, true);

        public void OnStart() => _processStart();
        public void OnEnd() => _processRemove();
        public void OnTick() => _processTick(); 

        public bool Expired => (DateTime.Now - Start).TotalSeconds >= Duration;
        public double Elapsed => (DateTime.Now - Start).TotalSeconds;
        public double Remaining => Duration - Elapsed;

        public double ElapsedSinceTick => (DateTime.Now - LastTick).TotalSeconds;

        public CreatureStatus(Status xmlstatus, Creature target, Castable castable=null, 
            int durationOverride = -1, int tickOverride = -1)
        {
            XMLStatus = xmlstatus;
            Target = target;
            Castable = castable;
            Start = DateTime.Now;
            _durationOverride = durationOverride;
            _tickOverride = tickOverride;
        }

        private void ProcessSfx(ModifierEffect effect)
        {
            if (effect.Sound != null)
                User?.PlaySound(effect.Sound.Id);
            if (effect.Animations != null)
            {
                if (effect.Animations.Target != null)
                    if (User == null && (User != null && !User.Condition.Comatose))
                        Target.Effect(effect.Animations.Target.Id, effect.Animations.Target.Speed);
                if (effect.Animations.SpellEffect != null)
                {
                    Source.Effect(effect.Animations.SpellEffect.Id, effect.Animations.SpellEffect.Speed);
                }
            }
            // Message handling
            if (effect.Messages != null && User != null)
            {
                if (effect.Messages.Target != null)
                    User.SendSystemMessage(string.Format(effect.Messages.Target, User.Name));
                if (effect.Messages.Group != null)
                    User.Group.SendMessage(string.Format(effect.Messages.Group, User.Name));
                if (effect.Messages.Source != null)
                    (Source as User)?.SendSystemMessage(string.Format(effect.Messages.Source, User.Name));
                if (effect.Messages.Say != null)
                    User?.Say(string.Format(effect.Messages.Say, User.Name));
                if (effect.Messages.Shout != null)
                    User?.Say(string.Format(effect.Messages.Shout, User.Name));
            }
        }


        private void ProcessConditions(ModifierEffect effect)
        {
            if (effect.Conditions.Set != 0)
                Target.Condition.Conditions|= effect.Conditions.Set;
            if (effect.Conditions.Unset != 0)
                Target.Condition.Conditions &= ~effect.Conditions.Unset;
        }

        private void ProcessStatModifiers(Statuses.StatModifiers effect, bool remove = false)
        {
            if (effect == null) return;

            if (remove)
            {
                Target.BonusStr -= effect.Str;
                Target.BonusInt -= effect.Int;
                Target.BonusWis -= effect.Wis;
                Target.BonusCon -= effect.Con;
                Target.BonusDex -= effect.Dex;
                Target.BonusHp -= effect.Hp;
                Target.BonusMp -= effect.Mp;
                Target.BonusHit -= effect.Hit;
                Target.BonusDmg -= effect.Dmg;
                Target.BonusAc -= effect.Ac;
                Target.BonusRegen -= effect.Regen;
                Target.BonusMr -= effect.Mr;
                Target.BonusDamageModifier = effect.DamageModifier;
                Target.BonusHealModifier = effect.HealModifier;
                Target.BonusReflectChance -= effect.ReflectChance;
                Target.BonusReflectIntensity -= effect.ReflectIntensity;
                if (effect.OffensiveElement == (Statuses.Element) Target.OffensiveElementOverride)
                    Target.OffensiveElementOverride = Enums.Element.None;
                if (effect.DefensiveElement == (Statuses.Element)Target.DefensiveElementOverride)
                    Target.DefensiveElementOverride = Enums.Element.None;
                Target.BonusAc -= effect.Str;
            }
            else
            {
                Target.BonusStr += effect.Str;
                Target.BonusInt += effect.Int;
                Target.BonusWis += effect.Wis;
                Target.BonusCon += effect.Con;
                Target.BonusDex += effect.Dex;
                Target.BonusHp += effect.Hp;
                Target.BonusMp += effect.Mp;
                Target.BonusHit += effect.Hit;
                Target.BonusDmg += effect.Dmg;
                Target.BonusAc += effect.Ac;
                Target.BonusRegen += effect.Regen;
                Target.BonusMr += effect.Mr;
                Target.BonusDamageModifier = effect.DamageModifier;
                Target.BonusHealModifier = effect.HealModifier;
                Target.BonusReflectChance += effect.ReflectChance;
                Target.BonusReflectIntensity += effect.ReflectIntensity;
                Target.BonusAc += effect.Str;
                Target.OffensiveElementOverride = (Enums.Element)effect.OffensiveElement;
                Target.DefensiveElementOverride = (Enums.Element)effect.OffensiveElement;

            }
        }

        private void ProcessDamageEffects(ModifierEffect effect)
        {
            if (Castable != null)
            {
                var heal = NumberCruncher.CalculateHeal(Castable, effect, Name, Target, Source);
                var dmg = NumberCruncher.CalculateDamage(Castable, effect, Name, Target, Source);
                if (heal != 0) Target.Heal(heal);
                if (dmg.Amount != 0) Target.Damage(dmg.Amount, Enums.Element.None, dmg.Type);
            }
        }

        private void ProcessFullEffects(ModifierEffect effect, bool RemoveStatBonuses=false)
        {
            // Stat modifiers and condition changes are only processed during start/remove
            ProcessConditions(effect);
            ProcessStatModifiers(XMLStatus.Effects?.OnApply?.StatModifiers, RemoveStatBonuses);
            ProcessSfx(effect);
            ProcessDamageEffects(effect);
        }

        private void ProcessEffects(ModifierEffect effect)
        {
            ProcessSfx(effect);
            ProcessDamageEffects(effect);
        }

    }
}