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

        public ThreatInfo ThreatInfo => Monster.ThreatInfo;
        public WorldObject Target => Monster.Target;
        public bool AbsoluteImmortal => Monster.AbsoluteImmortal;
        public bool PhysicalImmortal => Monster.PhysicalImmortal;
        public bool MagicalImmortal => Monster.MagicalImmortal;

        public WorldObject FirstHitter => Monster.FirstHitter;
        public WorldObject LastHitter => Monster.LastHitter;
        public string LastHitTime => Monster.LastHitTime.ToString();

        public void ForceThreatChange(HybrasylUser invoker) => Monster.ThreatInfo.ForceThreatChange(invoker.User);
        public void OnDamage(HybrasylUser invoker, int amount) => Monster.OnDamage(invoker.User, (uint)amount);

        public HybrasylMonster(Monster monster)
        {
            Monster = monster;
            World = new HybrasylWorld(monster.World);
            Map = new HybrasylMap(monster.Map);
        }

        public string GetGMMonsterInfo()
        {
            var s = "Monster Debug Info\n----------------------------\n\n";

            //this is for debug only
            s += $"Name: {Monster.Name} | Id: {Monster.Id}\n";
            s += $"Level: {Monster.Stats.Level} Health: {Monster.Stats.Hp}/{Monster.Stats.MaximumHp}\n";
            s += $"Damage: {Monster._spawn.Damage.Min}-{Monster._spawn.Damage.Max}\n";
            s += $"Experience: {Monster._spawn.Loot.Xp}\n";
            s += $"Castables:\n";
            s += $"  Offense: ({Monster._spawn.Castables.Offense.Interval} second timer) \n";
            foreach (var castable in Monster._spawn.Castables.Offense.Castables)
            {
                s += $"    {castable.Name}:\n";
                s += $"      Damage: {castable.MinDmg}-{castable.MaxDmg}\n";
                s += $"      Element: {castable.Element}\n";
                s += $"      TargetType: {castable.Target.ToString()}\n";
            }
            s += $"  Defense: ({Monster._spawn.Castables.Defense.Interval} second timer) \n";
            foreach (var castable in Monster._spawn.Castables.Defense.Castables)
            {
                s += $"    {castable.Name}:\n";
                s += $"      Damage: {castable.MinDmg}-{castable.MaxDmg}\n";
                s += $"      Element: {castable.Element}\n";
                s += $"      TargetType: {castable.Target.ToString()}\n";
            }
            s += $"  NearDeath: ({Monster._spawn.Castables.NearDeath.Interval} second timer) \n";
            foreach (var castable in Monster._spawn.Castables.NearDeath.Castables)
            {
                s += $"    {castable.Name}:\n";
                s += $"      Damage: {castable.MinDmg}-{castable.MaxDmg}\n";
                s += $"      Element: {castable.Element}\n";
                s += $"      TargetType: {castable.Target.ToString()}\n";
            }
            s += $"  OnDeath:\n";
            foreach (var castable in Monster._spawn.Castables.OnDeath)
            {
                s += $"    {castable.Name}:\n";
                s += $"      Damage: {castable.MinDmg}-{castable.MaxDmg}\n";
                s += $"      Element: {castable.Element}\n";
                s += $"      TargetType: {castable.Target.ToString()}\n";
            }
            s += $"AbsoluteImmortal: {Monster.AbsoluteImmortal}\n";
            s += $"PhysicalImmortal: {Monster.PhysicalImmortal}\n";
            s += $"MagicalImmortal: {Monster.MagicalImmortal}\n";
            s += $"IsHostile: {Monster.IsHostile}\n";
            s += $"ShouldWander: {Monster.ShouldWander}\n";
            //if (Monster.Target != 0) s += $"Target: {Monster.Target.Name}\n";
            if (Monster.FirstHitter != null) s += $"FirstHitter: {Monster.FirstHitter.Name}\n";
            //if (Monster.LastHitter != null) s += $"LastHitter: {Monster.LastHitter.Name}\n";
            if (Monster.LastHitTime != null) s += $"LastHitTime: {Monster.LastHitTime}\n";
            if (Monster.ThreatInfo != null)
            {
                s += $"ThreatInfo:\n";
                foreach(var user in Monster.ThreatInfo.ThreatTable)
                {
                    s += $"Name: {user.Key.Name} | Threat: {user.Value}\n";
                }
            }

            return s;
        }
    }
}
