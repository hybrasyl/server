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
using Hybrasyl.Scripting;
using Hybrasyl.Xml;
using Creature = Hybrasyl.Objects.Creature;

namespace Hybrasyl;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class Prohibited : Attribute
{
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

    public List<PlayerFlags> Flags { get; set; }
    public List<CreatureCondition> Conditions { get; set; }

    public bool Check(ConditionInfo condition)
    {
        foreach (var flag in Flags)
            if (condition.Flags.HasFlag(flag))
                return false;

        foreach (var cond in Conditions)
            if (condition.Conditions.HasFlag(cond))
                return false;

        return true;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class Required : Attribute
{
    public Required(params object[] prohibited)
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

    public List<PlayerFlags> Flags { get; set; }
    public List<CreatureCondition> Conditions { get; set; }

    public bool Check(ConditionInfo condition)
    {
        foreach (var flag in Flags)
            if (condition.Flags.HasFlag(flag))
                return true;

        foreach (var cond in Conditions)
            if (condition.Conditions.HasFlag(cond))
                return true;

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
    double Intensity { get; set; }
    double Elapsed { get; }
    double Remaining { get; }
    double ElapsedSinceTick { get; }
    string UseCastRestrictions { get; }
    string ReceiveCastRestrictions { get; }
    SimpleStatusEffect OnStartEffect { get; }
    SimpleStatusEffect OnTickEffect { get; }
    SimpleStatusEffect OnRemoveEffect { get; }
    SimpleStatusEffect OnExpireEffect { get; }
    void OnStart(bool displaySfx = true);
    void OnTick();
    void OnEnd();
    void OnExpire();
}

public class StatusInfo
{
    public string Name { get; set; }
    public string Category { get; set; }
    public SimpleStatusEffect OnStartEffect { get; set; }
    public SimpleStatusEffect OnTickEffect { get; set; }
    public SimpleStatusEffect OnRemoveEffect { get; set; }
    public SimpleStatusEffect OnExpireEffect { get; set; }
    public double Remaining { get; set; }
    public double Tick { get; set; }
    public double Intensity { get; set; }
}

public class SimpleStatusEffect
{
    public SimpleStatusEffect(double heal, DamageOutput damage)
    {
        Heal = heal;
        Damage = damage;
    }

    public double Heal { get; set; }
    public DamageOutput Damage { get; set; }
}

public class CreatureStatus : ICreatureStatus
{
    public CreatureStatus(Status xmlstatus, Creature target, Castable castable = null, Creature source = null,
        int duration = -1, int tickFrequency = -1, double intensity = 1.0)
    {
        Target = target;
        XmlStatus = xmlstatus;
        Start = DateTime.Now;
        Target = target;
        Source = source;
        Duration = duration == -1 ? xmlstatus.Duration : duration;
        Tick = tickFrequency == -1 ? xmlstatus.Tick : tickFrequency;
        Intensity = intensity;

        // Calculate damage/heal effects. Note that a castable MUST be passed here for a status 
        // to have damage effects as the castable itself has fields we need to access 
        // (intensity, etc) in order to do damage calculations.

        var start = CalculateNumericEffects(castable, xmlstatus.Effects.OnApply, source);
        var tick = CalculateNumericEffects(castable, xmlstatus.Effects.OnTick, source);
        var end = CalculateNumericEffects(castable, xmlstatus.Effects.OnRemove, source);
        var expire = CalculateNumericEffects(castable, xmlstatus.Effects.OnExpire, source);
        OnStartEffect = new SimpleStatusEffect(start.Heal, start.Damage);
        OnTickEffect = new SimpleStatusEffect(tick.Heal, tick.Damage);
        OnRemoveEffect = new SimpleStatusEffect(end.Heal, end.Damage);
        OnExpireEffect = new SimpleStatusEffect(expire.Heal, expire.Damage);
        BonusModifiers = NumberCruncher.CalculateStatusModifiers(castable, intensity,
            xmlstatus.Effects.OnApply.StatModifiers, source, target);
    }

    public CreatureStatus(StatusInfo serialized, Creature target)
    {
        Target = target;
        if (!string.IsNullOrEmpty(serialized.Name))
        {
            if (Game.World.WorldData.TryGetValue(serialized.Name, out Status status))
            {
                XmlStatus = status;
                Start = DateTime.Now;
                Duration = serialized.Remaining;
                Tick = serialized.Tick;
                OnTickEffect = serialized.OnTickEffect;
                OnRemoveEffect = serialized.OnRemoveEffect;
                OnStartEffect = serialized.OnStartEffect;
                OnRemoveEffect = serialized.OnRemoveEffect;
                Intensity = serialized.Intensity;
            }
            else
            {
                throw new ArgumentException(
                    $"Serialized status {serialized.Name} does not exist or could not be found");
            }
        }
    }

    public string Category => XmlStatus.Category;
    protected User TargetUser => Target as User;
    protected User SourceUser => Target as User;

    public Conditions ConditionChanges => XmlStatus.Effects?.OnApply?.Conditions;

    public Castable Castable { get; set; }
    public Status XmlStatus { get; set; }
    public StatInfo BonusModifiers { get; set; } = new();
    public string Name => XmlStatus.Name;
    public ushort Icon => XmlStatus.Icon;
    public double Tick { get; }
    public double Duration { get; }
    public string UseCastRestrictions => XmlStatus.CastRestriction?.Use ?? string.Empty;
    public string ReceiveCastRestrictions => XmlStatus.CastRestriction?.Receive ?? string.Empty;
    public double Intensity { get; set; } = 1;

    public StatusInfo Info => new()
    {
        Name = Name,
        OnStartEffect = OnStartEffect,
        OnRemoveEffect = OnRemoveEffect,
        OnTickEffect = OnTickEffect,
        OnExpireEffect = OnExpireEffect,
        Remaining = Remaining,
        Tick = Tick,
        Category = Category,
        Intensity = Intensity
    };

    public Creature Target { get; }
    public Creature Source { get; }

    public DateTime Start { get; }

    public DateTime LastTick { get; private set; }
    public string ActionProhibitedMessage { get; set; }

    public void OnStart(bool displaySfx = true)
    {
        _processStart(displaySfx);
    }

    public void OnEnd()
    {
        _processRemove();
    }

    public void OnTick()
    {
        _processTick();
    }

    public void OnExpire()
    {
        _processExpire();
    }

    public SimpleStatusEffect OnTickEffect { get; }
    public SimpleStatusEffect OnStartEffect { get; }
    public SimpleStatusEffect OnRemoveEffect { get; }
    public SimpleStatusEffect OnExpireEffect { get; }

    public bool Expired => (DateTime.Now - Start).TotalSeconds >= Duration;
    public double Elapsed => (DateTime.Now - Start).TotalSeconds;
    public double Remaining => Duration - Elapsed;

    public double ElapsedSinceTick => (DateTime.Now - LastTick).TotalSeconds;

    private void ProcessSfx(ModifierEffect effect)
    {
        if (effect == null) return;
        if (effect.Sound != null && effect.Sound.Id != 0)
        {
            (Target as User)?.PlaySound(effect.Sound.Id);
            (Source as User)?.PlaySound(effect.Sound.Id);
        }

        if (effect.Animations != null)
        {
            if (effect.Animations?.Target != null && effect.Animations.Target.Id != 0)
            {
                var animation = effect.Animations.Target;
                if (Target is Monster || !Target.Condition.Comatose || (Target.Condition.Comatose &&
                                                                        animation.Id == (Game.Config.Handlers?.Death
                                                                            ?.Coma?.Effect ?? 24)))
                    Target.Effect(effect.Animations.Target.Id, effect.Animations.Target.Speed);
            }

            if (effect.Animations?.SpellEffect != null && effect.Animations?.SpellEffect.Id != 0)
                Source?.Effect(effect.Animations.SpellEffect.Id, effect.Animations.SpellEffect.Speed);
        }

        // Message handling
        if (effect.Messages != null)
        {
            if (TargetUser != null)
            {
                if (effect.Messages?.Target != null)
                    TargetUser.SendSystemMessage(string.Format(effect.Messages.Target, SourceUser.Name));
                if (effect.Messages?.Group != null)
                    TargetUser.Group?.SendMessage(string.Format(effect.Messages.Group, SourceUser.Name));
            }

            if (effect.Messages?.Source != null && TargetUser != null && TargetUser != SourceUser)
                (Source as User)?.SendSystemMessage(string.Format(effect.Messages.Source,
                    TargetUser?.Name ?? string.Empty));
            if (effect.Messages?.Say != null)
                Target.Say(string.Format(effect.Messages.Say, Target.Name ?? string.Empty));
            if (effect.Messages?.Shout != null)
                Target.Shout(string.Format(effect.Messages.Shout, Target.Name ?? string.Empty));
        }
    }


    private void ProcessConditions(ModifierEffect effect)
    {
        if (effect == null) return;
        if (effect.Conditions?.Set != null)
            Target.Condition.Conditions |= effect.Conditions.Set;
        if (effect.Conditions?.Unset != null)
            Target.Condition.Conditions &= ~effect.Conditions.Unset;
    }

    private void ProcessStatModifiers(bool remove = false)
    {
        if (remove)
            Target.Stats.Remove(BonusModifiers);
        else
            Target.Stats.Apply(BonusModifiers);
    }

    private (double Heal, DamageOutput Damage) CalculateNumericEffects(Castable castable, ModifierEffect effect,
        Creature source)
    {
        double heal = 0;
        var dmg = new DamageOutput();
        if (effect == null) return (heal, dmg);
        if (effect.Heal != null) heal = NumberCruncher.CalculateHeal(castable, effect, Target, source, Name);
        if (effect.Damage != null) dmg = NumberCruncher.CalculateDamage(castable, effect, Target, source, Name);
        return (heal, dmg);
    }

    private void ProcessNumericEffects(SimpleStatusEffect effect)
    {
        if (effect == null) return;
        if (effect.Damage != null && effect.Damage.Amount != 0)
            Target.Damage(effect.Damage.Amount, effect.Damage.Element, effect.Damage.Type, effect.Damage.Flags, Source, Castable);
        if (effect.Heal != 0)
            Target.Heal(effect.Heal, Source, Castable);
    }

    private void ProcessFullEffects(ModifierEffect effect, bool RemoveStatBonuses = false, bool displaySfx = true)
    {
        if (effect == null) return;
        // Stat modifiers and condition changes are only processed during start/remove
        ProcessConditions(effect);
        ProcessStatModifiers(RemoveStatBonuses);
        if (displaySfx)
            ProcessSfx(effect);
    }

    private void ProcessHandler(Handler handler)
    {
        if ((handler?.Function ?? string.Empty) == string.Empty)
            return;

        // If a handler is specified, check the script for it first. Note that we don't run both;
        // if you override something like OnDeath, that's your problem.
        VisibleObject invoker;
        VisibleObject invokee;

        if (handler.ScriptSource == ScriptSource.Target)
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
            invokee.Script.ExecuteFunction(handler.Function,
                ScriptEnvironment.CreateWithTargetAndSource(invoker, invokee));
            return;
        }

        var type = invokee.GetType();

        try
        {
            var methodInfo = type.GetMethod(handler.Function);
            methodInfo.Invoke(invokee, null);
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error("Exception processing status handler: {exception}", e);
        }
    }

    private void ProcessEffects(ModifierEffect effect)
    {
        if (effect == null) return;
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