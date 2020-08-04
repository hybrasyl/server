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
 
using System;
using System.Collections.Generic;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using System.Reflection;

namespace Hybrasyl
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class Prohibited : Attribute
    {
        public List<PlayerFlags> Flags { get; set; }
        public List<Xml.CreatureCondition> Conditions { get; set; }

        public Prohibited(params object[] prohibited)
        {
            Flags = new List<PlayerFlags>();
            Conditions = new List<Xml.CreatureCondition>();

            foreach (var parameter in prohibited)
            {
                if (parameter.GetType() == typeof(PlayerFlags))
                    Flags.Add((PlayerFlags) parameter);
                if (parameter.GetType() == typeof(Xml.CreatureCondition))
                    Conditions.Add((Xml.CreatureCondition) parameter);
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
        public List<Xml.CreatureCondition> Conditions { get; set; }

        public Required(params object[] prohibited)
        {
            Flags = new List<PlayerFlags>();
            Conditions = new List<Xml.CreatureCondition>();

            foreach (var parameter in prohibited)
            {
                if (parameter.GetType() == typeof(PlayerFlags))
                    Flags.Add((PlayerFlags)parameter);
                if (parameter.GetType() == typeof(Xml.CreatureCondition))
                    Conditions.Add((Xml.CreatureCondition)parameter);
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
        void OnExpire();
        SimpleStatusEffect OnStartEffect { get; }
        SimpleStatusEffect OnTickEffect { get; }
        SimpleStatusEffect OnRemoveEffect { get; }
        SimpleStatusEffect OnExpireEffect { get; }
    }

    public class StatusInfo
    {
        public string Name { get; set; }
        public SimpleStatusEffect OnStartEffect { get; set; }
        public SimpleStatusEffect OnTickEffect { get; set; }
        public SimpleStatusEffect OnRemoveEffect { get; set; }
        public SimpleStatusEffect OnExpireEffect { get; set; }
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
        public string Name => XmlStatus.Name;
        public ushort Icon => XmlStatus.Icon;
        public double Tick { get; }
        public double Duration { get; }
        public string UseCastRestrictions => XmlStatus.CastRestriction?.Use ?? string.Empty;
        public string ReceiveCastRestrictions => XmlStatus.CastRestriction?.Receive ?? string.Empty;

        public StatusInfo Info => new StatusInfo() { Name = Name, OnStartEffect = OnStartEffect, OnRemoveEffect = OnRemoveEffect, OnTickEffect = OnTickEffect, OnExpireEffect = OnExpireEffect, Remaining = Remaining, Tick = Tick};

        public Creature Target { get; }
        public Creature Source { get; }
        protected User User => Target as User;

        public Xml.Conditions ConditionChanges => XmlStatus.Effects?.OnApply?.Conditions;

        public DateTime Start { get; }

        public DateTime LastTick { get; private set; }

        public Xml.Castable Castable { get; set; }
        public Xml.Status XmlStatus { get; set; }
        public string ActionProhibitedMessage { get; set; }

        public void OnStart(bool displaySfx = true) => _processStart(displaySfx);
        public void OnEnd() => _processRemove();
        public void OnTick() => _processTick();
        public void OnExpire() => _processExpire();

        public SimpleStatusEffect OnTickEffect { get; }
        public SimpleStatusEffect OnStartEffect { get; }
        public SimpleStatusEffect OnRemoveEffect { get; }
        public SimpleStatusEffect OnExpireEffect { get; }

        public bool Expired => (DateTime.Now - Start).TotalSeconds >= Duration;
        public double Elapsed => (DateTime.Now - Start).TotalSeconds;
        public double Remaining => Duration - Elapsed;

        public double ElapsedSinceTick => (DateTime.Now - LastTick).TotalSeconds;

        public CreatureStatus(Xml.Status xmlstatus, Creature target, Xml.Castable castable = null, Creature source = null, int duration = -1, int tickFrequency = -1)
        {
            Target = target;
            XmlStatus = xmlstatus;
            Start = DateTime.Now;
            Duration = duration == -1 ? xmlstatus.Duration : duration;
            Tick = tickFrequency == -1 ? xmlstatus.Tick : duration;

            // Calculate damage/heal effects. Note that a castable MUST be passed here for a status 
            // to have damage effects as the castable itself has fields we need to access 
            // (intensity, etc) in order to do damage calculations.

            if (castable != null)
            {
                var start = CalculateNumericEffects(castable, xmlstatus.Effects.OnApply, source);
                var tick = CalculateNumericEffects(castable, xmlstatus.Effects.OnTick, source);
                var end = CalculateNumericEffects(castable, xmlstatus.Effects.OnRemove, source);
                var expire = CalculateNumericEffects(castable, xmlstatus.Effects.OnExpire, source);
                OnStartEffect = new SimpleStatusEffect(start.Heal, start.Damage);
                OnTickEffect = new SimpleStatusEffect(tick.Heal, tick.Damage);
                OnRemoveEffect = new SimpleStatusEffect(end.Heal, end.Damage);
                OnExpireEffect = new SimpleStatusEffect(expire.Heal, expire.Damage);
            }
        }

        public CreatureStatus(StatusInfo serialized, Creature target)
        {
            Target = target;
            if (!string.IsNullOrEmpty(serialized.Name))
            {
                if (Game.World.WorldData.TryGetValueByIndex(serialized.Name, out Xml.Status status))
                {
                    XmlStatus = status;
                    Start = DateTime.Now;               
                    Duration = serialized.Remaining;
                    Tick = serialized.Tick;
                    OnTickEffect = serialized.OnTickEffect;
                    OnRemoveEffect = serialized.OnRemoveEffect;
                    OnStartEffect = serialized.OnStartEffect;
                    OnRemoveEffect = serialized.OnRemoveEffect;
                }
                else
                {
                    throw new ArgumentException($"Serialized status {serialized.Name} does not exist or could not be found");
                }
            }
        }

        private void ProcessSfx(Xml.ModifierEffect effect)
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
            if (effect.Messages != null)
            {
                if (User != null)
                {
                    if (effect.Messages?.Target != null)
                        User.SendSystemMessage(string.Format(effect.Messages.Target, User.Name));
                    if (effect.Messages?.Group != null)
                        User.Group?.SendMessage(string.Format(effect.Messages.Group, User.Name));
                }
                if (effect.Messages?.Source != null)
                    (Source as User)?.SendSystemMessage(string.Format(effect.Messages.Source, User?.Name ?? string.Empty));
                if (effect.Messages?.Say != null)
                    Target.Say(string.Format(effect.Messages.Say, User?.Name ?? string.Empty));
                if (effect.Messages?.Shout != null)
                    Target.Shout(string.Format(effect.Messages.Shout, User?.Name ?? string.Empty));
            }
        }


        private void ProcessConditions(Xml.ModifierEffect effect)
        {
            if (effect.Conditions?.Set != null)
                Target.Condition.Conditions |= effect.Conditions.Set;
            if (effect.Conditions?.Unset != null)
                Target.Condition.Conditions &= ~effect.Conditions.Unset;
        }

        private void ProcessStatModifiers(Xml.StatModifiers effect, bool remove = false)
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
                if (effect.OffensiveElement == Target.Stats.OffensiveElementOverride)
                    Target.Stats.OffensiveElementOverride = Xml.Element.None;
                if (effect.DefensiveElement == Target.Stats.DefensiveElementOverride)
                    Target.Stats.DefensiveElementOverride = Xml.Element.None;
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
                Target.Stats.OffensiveElementOverride = effect.OffensiveElement;
                Target.Stats.DefensiveElementOverride = effect.OffensiveElement;
            }
        }

        private (double Heal, DamageOutput Damage) CalculateNumericEffects(Xml.Castable castable, Xml.ModifierEffect effect, Creature source)
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
               //      if (dmg.Amount != 0) Target.Damage(dmg.Amount, Element.None, dmg.Type);
            }
            return (heal, dmg);
        }

        private void ProcessNumericEffects(SimpleStatusEffect effect)
        {

        }
        private void ProcessFullEffects(Xml.ModifierEffect effect, bool RemoveStatBonuses = false, bool displaySfx = true)
        {
            // Stat modifiers and condition changes are only processed during start/remove
            ProcessConditions(effect);
            ProcessStatModifiers(XmlStatus.Effects?.OnApply?.StatModifiers, RemoveStatBonuses);
            if (displaySfx)
                ProcessSfx(effect);
        }

        private void ProcessHandler(Xml.Handler handler)
        {
            if ((handler?.Function ?? String.Empty ) == String.Empty)
                return;

            // If a handler is specified, check the script for it first. Note that we don't run both;
            // if you override something like OnDeath, that's your problem.
            VisibleObject invoker;
            VisibleObject invokee;

            if (handler.ScriptSource == Xml.ScriptSource.Target)
            {
                invokee = Target;
                invoker = Source;
            }
            else // Caster
            {
                invokee = Source;
                invoker = Target;
            }

            if (invokee.Script != null)
            {
                invokee.Script.ExecuteFunction(handler.Function, invoker);
                return;
            }

            Type type = invokee.GetType();
            try
            {
                MethodInfo methodInfo = type.GetMethod(handler.Function);
                methodInfo.Invoke(invokee,null);
            }
            catch (Exception e)
            { 
                GameLog.Error("Exception processing status handler: {exception}", e);

            }
        }

        private void ProcessEffects(Xml.ModifierEffect effect)
        {
            ProcessSfx(effect);
        }

        private void _processStart(bool displaySfx)
        {
            ProcessFullEffects(XmlStatus.Effects?.OnApply, false, displaySfx);
            ProcessNumericEffects(OnStartEffect);
            ProcessHandler(XmlStatus.Effects?.OnApply?.Handler);
        }

        private void _processTick()
        {
            LastTick = DateTime.Now;
            ProcessEffects(XmlStatus.Effects.OnTick);
            ProcessNumericEffects(OnTickEffect);
            ProcessHandler(XmlStatus.Effects?.OnTick?.Handler);
        }

        private void _processRemove()
        {
            ProcessFullEffects(XmlStatus.Effects?.OnRemove, true);
            ProcessNumericEffects(OnRemoveEffect);
            ProcessHandler(XmlStatus.Effects?.OnRemove?.Handler);
        }

        private void _processExpire()
        {
            ProcessFullEffects(XmlStatus.Effects?.OnExpire, true);
            ProcessNumericEffects(OnExpireEffect);
            ProcessHandler(XmlStatus.Effects?.OnExpire?.Handler);
        }

    }
}