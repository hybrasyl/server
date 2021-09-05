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

        public string GetGMMonsterInfo()
        {
            var s = "Monster Debug Info\n----------------------------\n\n";

            ////this is for debug only
           s += $"Name: {Monster.Name} | Id: {Monster.Id}\n";
           s += $"Level: {Monster.Stats.Level}  Health: {Monster.Stats.Hp}/{Monster.Stats.MaximumHp}  Mana: {Monster.Stats.Mp} / {Monster.Stats.MaximumMp}\n";
           s += $"Experience: {Monster.LootableXP}\n";
           s += $"Castables:\n";
            if (Monster.BehaviorSet?.Behavior?.Casting?.Offense != null)
            {
                s += $"  Offense: ({Monster.BehaviorSet.Behavior.Casting.Offense.Interval} second timer) \n";
                s += $"  {String.Join(',', Monster.BehaviorSet.OffensiveCastables)}";
                s += $"  Target: {Monster.BehaviorSet.Behavior.Casting.Offense.Priority}";
            }
            if (Monster.BehaviorSet?.Behavior?.Casting?.Defense != null)
            {
                s += $"  Offense: ({Monster.BehaviorSet.Behavior.Casting.Defense.Interval} second timer) \n";
                s += $"  {String.Join(',', Monster.BehaviorSet.DefensiveCastables)}";
                s += $"  Target: {Monster.BehaviorSet.Behavior.Casting.Defense.Priority}";
            }

            if (Monster.BehaviorSet?.Behavior?.Casting?.NearDeath != null)
            {
                s += $"  Offense: ({Monster.BehaviorSet.Behavior.Casting.NearDeath.Interval} second timer) \n";
                s += $"  {String.Join(',', Monster.BehaviorSet.NearDeathCastables)}";
                s += $"  Target: {Monster.BehaviorSet.Behavior.Casting.NearDeath.Priority}";
            }

            if (Monster.BehaviorSet?.Behavior?.Casting?.OnDeath != null)
            {
                s += $"  Offense: ({Monster.BehaviorSet.Behavior.Casting.OnDeath.Interval} second timer) \n";
                s += $"  {String.Join(',', Monster.BehaviorSet.OnDeathCastables)}";
                s += $"  Target: {Monster.BehaviorSet.Behavior.Casting.OnDeath.Priority}";
            }

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
                foreach(var user in Monster.ThreatInfo.ThreatTableByCreature)
                {
                    s += $"Name: {user.Key.Name} | Threat: {user.Value}\n";
                }
            }

            return s;
        }
    }
}
