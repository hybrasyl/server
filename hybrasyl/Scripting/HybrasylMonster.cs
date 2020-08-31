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

        public Dictionary<string, double> AggroTable => Monster.AggroTable;
        public WorldObject Target => Monster.Target;
        public bool AbsoluteImmortal => Monster.AbsoluteImmortal;
        public bool PhysicalImmortal => Monster.PhysicalImmortal;
        public bool MagicalImmortal => Monster.MagicalImmortal;

        public WorldObject FirstHitter => Monster.FirstHitter;
        public WorldObject LastHitter => Monster.LastHitter;
        public string LastHitTime => Monster.LastHitTime.ToString();

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
            s += $"Name: {Monster.Name}\n";
            s += $"Health: {Monster.Stats.Hp}/{Monster.Stats.MaximumHp}\n";
            s += $"AbsoluteImmortal: {Monster.AbsoluteImmortal}\n";
            s += $"PhysicalImmortal: {Monster.PhysicalImmortal}\n";
            s += $"MagicalImmortal: {Monster.MagicalImmortal}\n";
            s += $"IsHostile: {Monster.IsHostile}\n";
            s += $"ShouldWander: {Monster.ShouldWander}\n";
            //if (Monster.Target != 0) s += $"Target: {Monster.Target.Name}\n";
            if (Monster.FirstHitter != null) s += $"FirstHitter: {Monster.FirstHitter.Name}\n";
            //if (Monster.LastHitter != null) s += $"LastHitter: {Monster.LastHitter.Name}\n";
            if (Monster.LastHitTime != null) s += $"LastHitTime: {Monster.LastHitTime}\n";
            if (Monster.AggroTable != null)
            {
                s += $"AggroTable:\n";
                foreach(var aggro in AggroTable)
                {
                    s += $"Name: {aggro.Key} | Damage: {aggro.Value}\n";
                }
            }

            return s;
        }
    }
}
