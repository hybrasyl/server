using System;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
public class HybrasylMonster : HybrasylWorldObject
{
    public HybrasylMonster(Monster monster) : base(monster)
    {
        World = new HybrasylWorld(monster.World);
        Map = new HybrasylMap(monster.Map);
    }
    
    internal Monster Monster => WorldObject as Monster;

    public Direction Direction => Monster.Direction;
    internal HybrasylWorld World { get; set; }
    internal HybrasylMap Map { get; set; }

    public ThreatInfo ThreatInfo => Monster.ThreatInfo;
    public WorldObject Target => Monster.Target;
    public bool AbsoluteImmortal => Monster.AbsoluteImmortal;
    public bool PhysicalImmortal => Monster.PhysicalImmortal;
    public bool MagicalImmortal => Monster.MagicalImmortal;

    public WorldObject FirstHitter => Monster.FirstHitter;
    public WorldObject LastHitter => Monster.LastHitter;
    public string LastHitTime => Monster.LastHitTime.ToString();

    /// <summary>
    ///     Access the StatInfo of the specified user directly (all stats).
    /// </summary>
    public StatInfo Stats => Monster.Stats;

    public void ForceThreatChange(HybrasylUser invoker)
    {
        Monster.ThreatInfo.ForceThreatChange(invoker.User);
    }

    /// <summary>
    ///     Deal damage to the current player.
    /// </summary>
    /// <param name="damage">Integer amount of damage to deal.</param>
    /// <param name="element">Element of the damage (e.g. fire, air)</param>
    /// <param name="damageType">Type of damage (direct, magical, etc)</param>
    public void Damage(int damage, ElementType element = ElementType.None,
        DamageType damageType = DamageType.Direct)
    {
        Monster.Damage(damage, element, damageType);
    }

    public void SetCreatureDisplaySprite(int displaySprite)
    {
        Monster.Sprite = (ushort)displaySprite;
    }

    public int GetCreatureDisplaySprite() => Monster.Sprite;


    public string GetGMMonsterInfo()
    {
        var s = "Monster Debug Info\n----------------------------\n\n";

        ////this is for debug only
        s += $"Name: {Monster.Name} | Id: {Monster.Id}\n";
        s +=
            $"Level: {Monster.Stats.Level}  Health: {Monster.Stats.Hp}/{Monster.Stats.MaximumHp}  Mana: {Monster.Stats.Mp} / {Monster.Stats.MaximumMp}\n";
        s +=
            $"Stats: STR {Monster.Stats.Str} CON {Monster.Stats.Con} WIS {Monster.Stats.Wis} INT {Monster.Stats.Int} DEX {Monster.Stats.Dex}\n";
        s += $"Experience: {Monster.LootableXp}\n\n";
        s += "Castables:\n";

        foreach (var rotation in Monster.CastableController)
        {
            s +=
                $"  Set Type: {rotation.CastingSet.Type}, {rotation.Interval} second timer, target priority {rotation.CastingSet.TargetPriority} \n  Rotation:\n";
            foreach (var entry in rotation)
                s +=
                    $"    Castable: {entry.Name}, {entry.Directive} second timer, {entry.Threshold}% health, UseOnce: {entry.UseOnce}, Triggered: {entry.ThresholdTriggered}";
        }

        s += $"AbsoluteImmortal: {Monster.AbsoluteImmortal}\n";
        s += $"PhysicalImmortal: {Monster.PhysicalImmortal}\n";
        s += $"MagicalImmortal: {Monster.MagicalImmortal}\n";

        s += $"ShouldWander: {Monster.ShouldWander}\n";

        if (Monster.Target != null) s += $"Target: {Monster.Target.Name}\n";
        if (Monster.FirstHitter != null) s += $"FirstHitter: {Monster.FirstHitter.Name}\n";
        if (Monster.LastHitter != null) s += $"LastHitter: {Monster.LastHitter.Name}\n";
        if (Monster.LastHitTime != default) s += $"LastHitTime: {Monster.LastHitTime}\n";
        if (Monster.ThreatInfo != null)
        {
            s += "ThreatInfo:\n";
            foreach (var user in Monster.ThreatInfo.ThreatTableByCreature)
                s +=
                    $"Name: {Game.World.WorldState.GetWorldObject<VisibleObject>(user.Key)?.Name ?? "unknown"} | Threat: {user.Value}\n";
        }

        return s;
    }

    /// <summary>
    /// Apply a given status to a player.
    /// </summary>
    /// <param name="statusName">The name of the status</param>
    /// <param name="duration">The duration of the status, if zero, use default </param>
    /// <param name="tick">How often the tick should fire on the status (eg OnTick), if zero, use default</param>
    /// <param name="intensity">The intensity of the status (damage modifier), defaults to 1.0</param>
    /// <returns>boolean indicating whether or not the status was applied</returns>
    public bool ApplyStatus(string statusName, int duration = 0, int tick = 0, double intensity = 1)
    {

        var status  = Game.World.WorldData.Get<Status>(statusName);
        if  (status == null)
        {
            GameLog.ScriptingError("ApplyStatus: status {statusName} not found");
            return false;
        }

        return Monster.ApplyStatus(new CreatureStatus(status, Monster,  null,  null,
            duration == 0  ? status.Duration : duration,
            tick == 0  ? status.Tick : tick,
            intensity));
    }
}