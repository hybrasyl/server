using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Castables;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Statuses;
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
        string CastableName { get; }
        string ActionProhibitedMessage { get; }
        int Duration { get; }
        int Tick { get; }
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
        void OnStart();
        void OnTick();
        void OnEnd();

    }
   
    public class StatusInfo
    {
        public string Name { get; set; }
        public string CastableName { get; set; }
        public double Remaining { get; set; }
    }

    public class CreatureStatus : ICreatureStatus
    {
        public string Name => XmlStatus.Name;
        public string CastableName => Castable?.Name ?? string.Empty;
        public ushort Icon => XmlStatus.Icon;
        public int Tick => _durationOverride == -1 ? XmlStatus.Tick : _durationOverride;
        public int Duration => _tickOverride == -1 ? XmlStatus.Duration : _durationOverride;
        public string UseCastRestrictions => XmlStatus.CastRestriction?.Use ?? string.Empty;
        public string ReceiveCastRestrictions => XmlStatus.CastRestriction?.Receive ?? string.Empty;

        public StatusInfo Info => new StatusInfo() { Name = Name, CastableName = CastableName, Remaining = Remaining };

        private int _durationOverride;
        private int _tickOverride;

        public Creature Target { get; }
        public Creature Source { get; }
        protected User User => Target as User;

        public Conditions ConditionChanges => XmlStatus.Effects?.OnApply?.Conditions;

        public DateTime Start { get; }

        public DateTime LastTick { get; private set; }

        public Castable Castable { get; set; }
        public Status XmlStatus  { get; set; }
        public string ActionProhibitedMessage { get; set; }

        private void _processStart() => ProcessFullEffects(XmlStatus.Effects?.OnApply);
        private void _processTick() => ProcessEffects(XmlStatus.Effects?.OnTick);
        private void _processRemove() => ProcessFullEffects(XmlStatus.Effects?.OnRemove, true);

        public void OnStart() => _processStart();
        public void OnEnd() => _processRemove();
        public void OnTick() => _processTick(); 

        public bool Expired => (DateTime.Now - Start).TotalSeconds >= Duration;
        public double Elapsed => (DateTime.Now - Start).TotalSeconds;
        public double Remaining => Duration - Elapsed;

        public double ElapsedSinceTick => (DateTime.Now - LastTick).TotalSeconds;

        public CreatureStatus(Status xmlstatus, Creature target, Creature source=null, Castable castable = null,
            int durationOverride = -1, int tickOverride = -1)
        {
            Target = target;
            Source = source;
            XmlStatus = xmlstatus;
            Castable = castable;
            Start = DateTime.Now;
            _durationOverride = durationOverride;
            _tickOverride = tickOverride;
        }

        public CreatureStatus(Status xmlstatus, Creature target, Creature source=null, Castable castable = null)
        {
            Target = target;
            Source = source;
            XmlStatus = xmlstatus;
            Castable = castable;
            Start = DateTime.Now;

            var addList = castable?.Effects.Statuses.Add.Where(e => e.Value == xmlstatus.Name);
            if (addList?.Count() > 0)
            {
                var addObj = addList.First();
                _durationOverride = addObj.Duration != 0 ? addObj.Duration : xmlstatus.Duration;
                _tickOverride = (int) Math.Floor(addObj.Speed * xmlstatus.Tick);
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
                Target.Condition.Conditions|= effect.Conditions.Set;
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
                if (effect.OffensiveElement == (Statuses.Element) Target.Stats.OffensiveElementOverride)
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

        private void ProcessDamageEffects(ModifierEffect effect)
        {
            if (!effect.Heal.IsEmpty)
            {
                var heal = NumberCruncher.CalculateHeal(Castable, effect, Target, Source, Name);
                if (heal != 0) Target.Heal(heal);
            }
            if (!effect.Damage.IsEmpty)
            {
                var dmg = NumberCruncher.CalculateDamage(Castable, effect, Target, Source, Name);
                if (dmg.Amount != 0) Target.Damage(dmg.Amount, Enums.Element.None, dmg.Type);
            }
        }

        private void ProcessFullEffects(ModifierEffect effect, bool RemoveStatBonuses=false)
        {
            // Stat modifiers and condition changes are only processed during start/remove
            ProcessConditions(effect);
            ProcessStatModifiers(XmlStatus.Effects?.OnApply?.StatModifiers, RemoveStatBonuses);
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