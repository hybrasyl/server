using Grpc.Core;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Text;

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]
    public class HybrasylMonster
    {
        internal Monster Monster { get; set; }
        internal HybrasylWorld World { get; set; }
        internal HybrasylMap Map { get; set; }

        public string Name => Monster.Name;

        public Xml.Direction Direction => Monster.Direction;

        public ThreatInfo ThreatInfo => Monster.ThreatInfo;
        public WorldObject Target => Monster.Target;
        public bool AbsoluteImmortal => Monster.AbsoluteImmortal;
        public bool PhysicalImmortal => Monster.PhysicalImmortal;
        public bool MagicalImmortal => Monster.MagicalImmortal;

        public WorldObject FirstHitter => Monster.FirstHitter;
        public WorldObject LastHitter => Monster.LastHitter;
        public string LastHitTime => Monster.LastHitTime.ToString();

        public void ForceThreatChange(HybrasylUser invoker) => Monster.ThreatInfo.ForceThreatChange(invoker.User);
        public void MakeHostile() => Monster.MakeHostile();

        public HybrasylMonster(Monster monster)
        {
            Monster = monster;
            World = new HybrasylWorld(monster.World);
            Map = new HybrasylMap(monster.Map);
        }
        public void SystemMessage(string nil) {}

        /// <summary>
        /// Deal damage to the current player.
        /// </summary>
        /// <param name="damage">Integer amount of damage to deal.</param>
        /// <param name="element">Element of the damage (e.g. fire, air)</param>
        /// <param name="damageType">Type of damage (direct, magical, etc)</param>
        public void Damage(int damage, Xml.ElementType element = Xml.ElementType.None,
            Xml.DamageType damageType = Xml.DamageType.Direct)
        {
            Monster.Damage(damage, element, damageType);
        }

        public string GetGMMonsterInfo()
        {
            var s = "Monster Debug Info\n----------------------------\n\n";

            ////this is for debug only
            s += $"Name: {Monster.Name} | Id: {Monster.Id}\n";
            s += $"Level: {Monster.Stats.Level}  Health: {Monster.Stats.Hp}/{Monster.Stats.MaximumHp}  Mana: {Monster.Stats.Mp} / {Monster.Stats.MaximumMp}\n";
            s += $"Stats: STR {Monster.Stats.Str} CON {Monster.Stats.Con} WIS {Monster.Stats.Wis} INT {Monster.Stats.Int} DEX {Monster.Stats.Dex}\n";
            s += $"Experience: {Monster.LootableXP}\n\n";
            s += $"Castables:\n";

            if (Monster.BehaviorSet?.Behavior?.Casting?.Offense != null)
            {
                s += $"  Offense: ({Monster.BehaviorSet.Behavior.Casting.Offense.Interval} second timer) \n";
                foreach (var castable in Monster.BehaviorSet.OffensiveCastables)
                {
                    s += $"  {castable.Value}: {castable.HealthPercentage}% {castable.Priority} {castable.UseOnce}\n";
                }
                s += $"  Target Priority: {Monster.BehaviorSet.Behavior.Casting.Offense.Priority}\n";
            }
            else
                s += $"  Offense: undefined / null";

            if (Monster.BehaviorSet?.Behavior?.Casting?.Defense != null)
            {
                s += $"  Defense: ({Monster.BehaviorSet.Behavior.Casting.Defense.Interval} second timer) \n";
                foreach (var castable in Monster.BehaviorSet.DefensiveCastables)
                {
                    s += $"  {castable.Value}: {castable.HealthPercentage}% {castable.Priority} {castable.UseOnce} \n";
                }
                s += $"  Target: {Monster.BehaviorSet.Behavior.Casting.Defense.Priority} \n";
            }
            else
                s += $"  Defense: undefined / null";

            if (Monster.BehaviorSet?.Behavior?.Casting?.NearDeath != null)
            {
                s += $"  NearDeath: ({Monster.BehaviorSet.Behavior.Casting.NearDeath.Interval} second timer) \n";
                foreach (var castable in Monster.BehaviorSet.NearDeathCastables)
                {
                    s += $"  {castable.Value}: {castable.HealthPercentage}% {castable.Priority} {castable.UseOnce}\n";
                }
                s += $"  Target: {Monster.BehaviorSet.Behavior.Casting.NearDeath.Priority}  \n";
            }
            else
                s += $"  NearDeath: undefined / null";

            if (Monster.BehaviorSet?.Behavior?.Casting?.OnDeath != null)
            {
                s += $"  OnDeath: ({Monster.BehaviorSet.Behavior.Casting.OnDeath.Interval} second timer) \n";
                foreach (var castable in Monster.BehaviorSet.OnDeathCastables)
                {
                    s += $"  {castable.Value}: {castable.HealthPercentage}% {castable.Priority} {castable.UseOnce}\n";
                }
                s += $"  Target: {Monster.BehaviorSet.Behavior.Casting.OnDeath.Priority}  \n";
            }
            else
                s += $"  OnDeath: undefined / null";

            s += $"AbsoluteImmortal: {Monster.AbsoluteImmortal}\n";
            s += $"PhysicalImmortal: {Monster.PhysicalImmortal}\n";
            s += $"MagicalImmortal: {Monster.MagicalImmortal}\n";
            s += $"IsHostile: {Monster.IsHostile}\n";
            s += $"ShouldWander: {Monster.ShouldWander}\n";

            if (Monster.Target != null) s += $"Target: {Monster.Target.Name}\n";
            if (Monster.FirstHitter != null) s += $"FirstHitter: {Monster.FirstHitter.Name}\n";
            if (Monster.LastHitter != null) s += $"LastHitter: {Monster.LastHitter.Name}\n";
            if (Monster.LastHitTime != default) s += $"LastHitTime: {Monster.LastHitTime}\n";
            if (Monster.ThreatInfo != null)
            {
                s += $"ThreatInfo:\n";
                foreach (var user in Monster.ThreatInfo.ThreatTableByCreature)
                {
                    s += $"Name: {user.Key.Name} | Threat: {user.Value}\n";
                }
            }

            return s;
        }
    }
}
