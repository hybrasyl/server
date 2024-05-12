// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Casting;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Scripting;
using Hybrasyl.Utility;
using Hybrasyl.Xml.Objects;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Hybrasyl.Objects;

public class Creature : VisibleObject
{
    private readonly object _lock = new();

    private uint _mLastHitter;

    protected ConcurrentDictionary<ushort, ICreatureStatus> CurrentStatuses;

    public Creature()
    {
        Inventory = new Inventory(59);
        Equipment = new Equipment(18);
        Stats = new StatInfo();
        Condition = new ConditionInfo(this);
        CurrentStatuses = new ConcurrentDictionary<ushort, ICreatureStatus>();
        LastHitTime = DateTime.MinValue;
        Statuses = new List<StatusInfo>();
        Cookies = new Dictionary<string, string>();
        SessionCookies = new Dictionary<string, string>();
    }

    [JsonProperty(Order = 2)] public StatInfo Stats { get; set; }
    [JsonProperty(Order = 3)] public ConditionInfo Condition { get; set; }

    [JsonProperty] public List<StatusInfo> Statuses { get; set; }

    public List<StatusInfo> CurrentStatusInfo => CurrentStatuses.Count > 0
        ? CurrentStatuses.Values.Select(selector: e => e.Info).ToList()
        : new List<StatusInfo>();

    public uint Gold => Stats.Gold;

    [JsonProperty] private Dictionary<string, string> Cookies { get; set; }
    private Dictionary<string, string> SessionCookies { get; set; }

    [JsonProperty] public Inventory Inventory { get; protected set; }

    [JsonProperty] public Equipment Equipment { get; protected set; }

    public DateTime LastHitTime { get; private set; }
    public Creature FirstHitter { get; internal set; }

    public Creature LastHitter
    {
        get
        {
            if (Game.World.Objects.TryGetValue(_mLastHitter, out var o))
                return o as Creature;
            return null;
        }
        set => _mLastHitter = value?.Id ?? 0;
    }

    public Creature LastTarget { get; set; }
    public Creature CurrentTarget { get; set; }

    public bool AbsoluteImmortal { get; set; }
    public bool PhysicalImmortal { get; set; }
    public bool MagicalImmortal { get; set; }

    [FormulaVariable]
    public ushort WeaponSmallDamage
    {
        get
        {
            if (Equipment.Weapon is null)
                return 0;
            var mindmg = (int)Equipment.Weapon.MinSDamage;
            var maxdmg = (int)Equipment.Weapon.MaxSDamage;
            if (mindmg == 0) mindmg = 1;
            if (maxdmg == 0) maxdmg = 1;
            return (ushort)Random.Shared.Next(mindmg, maxdmg + 1);
        }
    }

    [FormulaVariable]
    public ushort WeaponLargeDamage
    {
        get
        {
            if (Equipment.Weapon is null)
                return 0;
            var mindmg = (int)Equipment.Weapon.MinLDamage;
            var maxdmg = (int)Equipment.Weapon.MaxLDamage;
            if (mindmg == 0) mindmg = 1;
            if (maxdmg == 0) maxdmg = 1;
            return (ushort)Random.Shared.Next(mindmg, maxdmg + 1);
        }
    }

    public CreatureSnapshot GetSnapshot()
    {
        var stats = JsonConvert.SerializeObject(Stats);
        var statInfo = JsonConvert.DeserializeObject<StatInfo>(stats);
        return new CreatureSnapshot { Name = Name, Parent = Guid, Stats = statInfo };
    }

    public virtual string Status() => string.Empty;

    public override void OnClick(User invoker)
    {
        // TODO: abstract to xml

        string ret;
        var diff = Stats.Level - invoker.Stats.Level;
        ret = diff switch
        {
            >= -3 and <= 3 => "",
            >= -7 and <= -4 => "Trifling",
            <= -7 => "Paltry",
            >= 4 and <= 7 => "Difficult",
            > 7 => "Deadly"
        };

        ret += $" {Name}";
        if (invoker.AuthInfo.IsPrivileged)
            ret += $" ({Id})";
        invoker.SendSystemMessage(ret);
    }

    public List<Direction> GetClearSides()
    {
        var sides = new List<Direction>();
        if (Direction is Direction.North or Direction.South)
        {
            // Consider West, East
            if (GetDirectionalTarget(Direction.West) == null && !Map.IsWall(GetCoordinatesInDirection(Direction.West)))
                sides.Add(Direction.West);
            if (GetDirectionalTarget(Direction.East) == null && !Map.IsWall(GetCoordinatesInDirection(Direction.East)))
                sides.Add(Direction.East);
        }

        if (Direction is Direction.East or Direction.West)
        {
            // consider North, South
            if (GetDirectionalTarget(Direction.North) == null &&
                !Map.IsWall(GetCoordinatesInDirection(Direction.North)))
                sides.Add(Direction.North);
            if (GetDirectionalTarget(Direction.South) == null &&
                !Map.IsWall(GetCoordinatesInDirection(Direction.South)))
                sides.Add(Direction.South);
        }

        return sides;
    }

    public (byte x, byte y) GetCoordinatesInDirection(Direction direction)
    {
        var a = (int)X;
        var b = (int)Y;

        switch (direction)
        {
            case Direction.East:
                a = X + 1;
                b = Y;
                break;
            case Direction.West:
                a = X - 1;
                b = Y;
                break;
            case Direction.North:
                a = X;
                b = Y - 1;
                break;
            case Direction.South:
                a = X;
                b = Y + 1;
                break;
        }

        return ((byte)Math.Clamp(a, byte.MinValue, byte.MaxValue), (byte)Math.Clamp(b, byte.MinValue, byte.MaxValue));
    }

    public Creature GetDirectionalTarget(Direction direction)
    {
        VisibleObject obj;

        switch (direction)
        {
            case Direction.East:
                {
                    obj = Map.EntityTree.FirstOrDefault(predicate: x => x.X == X + 1 && x.Y == Y && x is Creature);
                }
                break;
            case Direction.West:
                {
                    obj = Map.EntityTree.FirstOrDefault(predicate: x => x.X == X - 1 && x.Y == Y && x is Creature);
                }
                break;
            case Direction.North:
                {
                    obj = Map.EntityTree.FirstOrDefault(predicate: x => x.X == X && x.Y == Y - 1 && x is Creature);
                }
                break;
            case Direction.South:
                {
                    obj = Map.EntityTree.FirstOrDefault(predicate: x => x.X == X && x.Y == Y + 1 && x is Creature);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }

        if (obj is Creature) return obj as Creature;
        return null;
    }

    /// <summary>
    ///     Get targets from this creature, in the specified direction, up to the specified radius of tiles.
    /// </summary>
    /// <param name="direction">The direction to find targets</param>
    /// <param name="radius">The radius to search</param>
    /// <returns></returns>
    public List<Creature> GetDirectionalTargets(Direction direction, int radius = 1)
    {
        var ret = new List<Creature>();
        var rect = new Rectangle(0, 0, 0, 0);

        switch (direction)
        {
            case Direction.East:
                {
                    rect = new Rectangle(X + 1, Y, radius, 1);
                }
                break;
            case Direction.West:
                {
                    rect = new Rectangle(X - radius, Y, radius, 1);
                }
                break;
            case Direction.South:
                {
                    rect = new Rectangle(X, Y + 1, 1, radius);
                }
                break;
            case Direction.North:
                {
                    rect = new Rectangle(X, Y - radius, 1, radius);
                }
                break;
        }

        GameLog.UserActivityInfo($"GetDirectionalTargets: {rect.X}, {rect.Y} {rect.Height}, {rect.Width}");
        ret.AddRange(Map.EntityTree.GetObjects(rect)
            .OfType<Creature>().OrderBy(keySelector: x => x.Distance(this)));
        return ret;
    }

    public Dictionary<Direction, List<Creature>> GetFacingTargets(int radius = 1)
    {
        var ret = new Dictionary<Direction, List<Creature>>();
        foreach (Direction direction in Enum.GetValues(typeof(Direction)))
        {
            ret[direction] = new List<Creature>();
            // TODO: null check, remove 
            ret[direction].AddRange(GetDirectionalTargets(direction, radius));
        }

        return ret;
    }

    public void ProcessProcs(ProcEventType type, Castable castable, Creature target)
    {
        if (castable.Effects?.Procs != null)
            foreach (var proc in castable.Effects.Procs.Where(predicate: proc =>
                         Random.Shared.NextDouble() <= proc.Chance))
                // Proc fires
                Game.World.EnqueueProc(proc, castable, Guid, target?.Guid ?? Guid.Empty);

        if (!castable.IsAssail || Equipment?.Weapon?.Procs == null)
            return;

        foreach (var proc in Equipment.Weapon.Procs.Where(predicate: proc => Random.Shared.NextDouble() <= proc.Chance))
            Game.World.EnqueueProc(proc, castable, Guid, target?.Guid ?? Guid.Empty);
    }


    public virtual List<Creature> GetTargets(Castable castable, Creature target = null)
    {
        IEnumerable<Creature> actualTargets = new List<Creature>();
        var intents = castable.Intents;
        var possibleTargets = new List<VisibleObject>();
        var finalTargets = new List<Creature>();
        Creature origin;

        foreach (var intent in intents)
        {
            if (intent.IsShapeless)
            {
                //GameLog.UserActivityInfo("GetTarget: Shapeless");
                // No shapes specified. 
                // If UseType=Target, exact clicked target.
                // If UseType=NoTarget Target=Self, caster.
                // If UseType=NoTarget Target=Group, *entire* group regardless of location on map.
                // Otherwise, no target.
                if (intent.UseType == SpellUseType.Target)
                {
                    // Exact clicked target
                    possibleTargets.Add(target);
                    //GameLog.UserActivityInfo("GetTarget: exact clicked target");
                }
                else if (intent.UseType == SpellUseType.NoTarget)
                {
                    possibleTargets.Add(this);
                    //GameLog.UserActivityInfo("GetTarget: notarget, self");
                    if (intent.Flags.Contains(IntentFlags.Group))
                        // Add group members
                        if (this is User uo)
                            if (uo.Group != null)
                                possibleTargets.AddRange(uo.Group.Members.Where(predicate: x => x.Connected));
                }
                else if (intent.UseType != SpellUseType.NoTarget)
                {
                    GameLog.UserActivityWarning($"Unhandled intent type {intent.UseType}, ignoring");
                }
            }

            if (intent.Map != null)
                // add entire map
                //GameLog.UserActivityInfo("GetTarget: adding map targets");
                possibleTargets.AddRange(Map.EntityTree.GetAllObjects().Where(predicate: e => e is Creature));

            if (intent.UseType == SpellUseType.NoTarget)
                origin = this;
            //GameLog.UserActivityInfo($"GetTarget: origin is {this.Name} at {this.X}, {this.Y}");
            else
                //GameLog.UserActivityInfo($"GetTarget: origin is {target.Name} at {target.X}, {target.Y}");
                origin = target;

            // Handle shapes
            foreach (var cross in intent.Cross)
            {
                // Process cross targets
                foreach (Direction direction in Enum.GetValues(typeof(Direction)))
                    //GameLog.UserActivityInfo($"GetTarget: cross, {direction}, origin {origin.Name}, radius {cross.Radius}");
                    possibleTargets.AddRange(origin.GetDirectionalTargets(direction, cross.Radius));

                // Add origin and let flags sort it out
                possibleTargets.Add(origin);
            }

            foreach (var line in intent.Line)
            {
                // Process line targets
                //GameLog.UserActivityInfo($"GetTarget: line, {line.Direction}, origin {origin.Name}, length {line.Length}");
                possibleTargets.AddRange(origin.GetDirectionalTargets(origin.GetIntentDirection(line.Direction),
                    line.Length));
                // Similar to above, add origin
                possibleTargets.Add(origin);
            }

            foreach (var square in intent.Square)
            {
                // Process square targets
                var r = (square.Side - 1) / 2;
                var rect = new Rectangle(origin.X - r, origin.Y - r, square.Side, square.Side);
                //GameLog.UserActivityInfo($"GetTarget: square, {origin.X - r}, {origin.Y - r} - origin {origin.Name}, side length {square.Side}");
                possibleTargets.AddRange(origin.Map.EntityTree.GetObjects(rect).Where(predicate: e => e is Creature));
            }

            foreach (var tile in intent.Tile)
                // Process tile targets, which can have either direction OR relative x/y
                if (tile.Direction == IntentDirection.None)
                {
                    if (tile.RelativeX == 0 && tile.RelativeY == 0)
                        //GameLog.UserActivityInfo($"GetTarget: tile, origin {origin.Name}, RelativeX && RelativeY == 0, skipping");
                        continue;
                    possibleTargets.AddRange(origin.Map
                        .GetTileContents(origin.X + tile.RelativeX, origin.Y + tile.RelativeY)
                        .Where(predicate: e => e is Creature));
                }
                else
                {
                    //GameLog.UserActivityInfo($"GetTarget: tile, intent {tile.Direction}, direction {origin.GetIntentDirection(tile.Direction)}, origin {origin.Name}");
                    possibleTargets.Add(origin.GetDirectionalTarget(origin.GetIntentDirection(tile.Direction)));
                }

            foreach (var tile in intent.Cone)
            {
                var radius = Math.Min(tile.Radius, Game.ActiveConfiguration.Constants.ViewportSize / 2);
                if (radius == 0)
                    continue;
                var coneDirection = tile.Direction.Resolve(Direction);
                for (var i = 1; i <= radius; i++)
                {
                    var rect = coneDirection switch
                    {
                        Direction.North => new Rectangle(X - i + 1, Y - i, 2 * i - 1, 1),
                        Direction.South => new Rectangle(X - i + 1, Y + i, 2 * i - 1, 1),
                        Direction.East => new Rectangle(X + i, Y - i + 1, 1, 2 * i - 1),
                        Direction.West => new Rectangle(X - i, Y - i + 1, 1, 2 * i - 1),
                        _ => throw new ArgumentOutOfRangeException(nameof(coneDirection))
                    };
                    possibleTargets.AddRange(Map.EntityTree.GetObjects(rect).Where(predicate: e => e is Creature));
                }
            }

            var possible = intent.MaxTargets > 0
                ? possibleTargets.Take(intent.MaxTargets).OfType<Creature>().ToList()
                : possibleTargets.OfType<Creature>().ToList();
            if (possible != null && possible.Count > 0)
                actualTargets = actualTargets.Concat(possible);
            else GameLog.UserActivityInfo("GetTarget: No targets found");

            // Remove all merchants
            // TODO: perhaps improve with a flag or extend in the future
            actualTargets = actualTargets.Where(predicate: e => e is User || e is Monster);

            // Process intent flags

            switch (this)
            {
                // No hostile flag: remove players
                case Monster when Condition.Charmed:
                    {
                        if (intent.Flags.Contains(IntentFlags.Hostile))
                            finalTargets.AddRange(actualTargets.OfType<Monster>());

                        // No friendly flag, or not charmed - remove monsters
                        if (intent.Flags.Contains(IntentFlags.Friendly))
                            finalTargets.AddRange(actualTargets.OfType<User>());
                        break;
                    }
                // Group / pvp: n/a
                case Monster:
                    {
                        if (intent.Flags.Contains(IntentFlags.Hostile))
                            finalTargets.AddRange(actualTargets.OfType<User>());

                        // No friendly flag, or not charmed - remove monsters
                        if (intent.Flags.Contains(IntentFlags.Friendly))
                            finalTargets.AddRange(actualTargets.OfType<Monster>());
                        break;
                    }
                case User userobj:
                    {
                        // No PVP flag: remove PVP flagged players
                        // No hostile flag: remove monsters
                        // No friendly flag: remove non-PVP flagged players
                        // No group flag: remove group members
                        if (intent.Flags.Contains(IntentFlags.Hostile))
                            finalTargets.AddRange(actualTargets.OfType<Monster>());

                        if (intent.Flags.Contains(IntentFlags.Friendly))
                            finalTargets.AddRange(actualTargets.OfType<User>()
                                .Where(predicate: e => e.Condition.PvpEnabled == false && e.Id != Id));

                        if (intent.Flags.Contains(IntentFlags.Pvp))
                            finalTargets.AddRange(actualTargets.OfType<User>()
                                .Where(predicate: e => e.Condition.PvpEnabled && e.Id != Id));

                        if (intent.Flags.Contains(IntentFlags.Group))
                            // Remove group members
                            if (userobj.Group != null)
                                finalTargets.AddRange(actualTargets.OfType<User>()
                                    .Where(predicate: e => userobj.Group.Contains(e)));
                        break;
                    }
            }

            // No Self flag: remove self 
            if (intent.Flags.Contains(IntentFlags.Self))
            {
                GameLog.UserActivityInfo(
                    $"Trying to remove self: my id is {Id} and actualtargets contains {string.Join(',', actualTargets.Select(selector: e => e.Id).ToList())}");
                finalTargets.AddRange(actualTargets.Where(predicate: e => e.Id == Id));
                GameLog.UserActivityInfo(
                    $"did it happen :o -  my id is {Id} and actualtargets contains {string.Join(',', actualTargets.Select(selector: e => e.Id).ToList())}");
            }
        }

        // Lastly, remove any duplicates
        return finalTargets.DistinctBy(keySelector: x => x.Guid).ToList();
    }

    public virtual bool UseCastable(Castable castableXml, Creature target = null)
    {
        if (this is User)
            GameLog.UserActivityInfo(
                $"UseCastable: {Name} begin casting {castableXml.Name} on target: {target?.Name ?? "no target"} CastingAllowed: {Condition.CastingAllowed}");

        var damage = castableXml.Effects.Damage;
        var targets = new List<Creature>();
        targets = GetTargets(castableXml, target);

        // Quick checks
        // If no targets and is not an assail, do nothing
        if (!targets.Any() && castableXml.IsAssail == false && string.IsNullOrEmpty(castableXml.Script))
        {
            GameLog.UserActivityInfo($"UseCastable: {Name}: no targets and not assail");
            return false;
        }

        ProcessProcs(ProcEventType.OnCast, castableXml, null);
        // Is this a pvpable spell? If so, is pvp enabled?

        // We do these next steps to ensure effects are displayed uniformly and as fast as possible
        var deadMobs = new List<Creature>();

        if (castableXml.Effects?.Animations?.OnCast != null)
        {
            if (castableXml.Effects?.Animations?.OnCast.Target != null)
                foreach (var tar in targets)
                    foreach (var user in tar.viewportUsers.ToList())
                    {
                        GameLog.UserActivityInfo(
                            $"UseCastable: Sending {user.Name} effect for {Name}: {castableXml.Effects.Animations.OnCast.Target.Id}");
                        user.SendEffect(tar.Id, castableXml.Effects.Animations.OnCast.Target.Id,
                            castableXml.Effects.Animations.OnCast.Target.Speed);
                    }

            if (castableXml.Effects?.Animations?.OnCast?.SpellEffect != null)
            {
                GameLog.UserActivityInfo(
                    $"UseCastable: Sending spelleffect for {Name}: {castableXml.Effects.Animations.OnCast.SpellEffect.Id}");
                Effect(castableXml.Effects.Animations.OnCast.SpellEffect.Id,
                    castableXml.Effects.Animations.OnCast.SpellEffect.Speed);
            }
        }

        if (castableXml.IsAssail)
        {
            if (Equipment?.Weapon?.AssailSound != null)
                PlaySound(Equipment.Weapon.AssailSound);
            else if (this is Monster m && m.AssailSound != 0)
                PlaySound(m.AssailSound);
            else if (castableXml.Effects?.Sound != null)
                PlaySound(castableXml.Effects.Sound.Id);
        }
        else if (castableXml.Effects?.Sound != null)
        {
            PlaySound(castableXml.Effects.Sound.Id);
        }

        GameLog.UserActivityInfo($"UseCastable: {Name} casting {castableXml.Name}, {targets.Count} targets");

        if (!string.IsNullOrEmpty(castableXml.Script))
        {
            // If a script is defined we fire it immediately, and let it handle targeting / etc
            if (Game.World.ScriptProcessor.TryGetScript(castableXml.Script, out var script))
            {
                Game.World.WorldState.TryGetValueByIndex(castableXml.Guid, out CastableObject castableObj);
                var ret = script.ExecuteFunction("OnUse",
                    ScriptEnvironment.Create(("target", target), ("origin", castableObj), ("source", this),
                        ("castable", castableObj)));
                return ret.Result == ScriptResult.Success;
            }

            GameLog.UserActivityError(
                $"UseCastable: {Name} casting {castableXml.Name}: castable script {castableXml.Script} missing");
            return false;
        }

        if (targets.Count == 0)
            GameLog.UserActivityError("{Name}: {castObject.Name}: hey fam no targets");

        foreach (var tar in targets)
        {
            if (castableXml.Effects?.ScriptOverride == true)
                // TODO: handle castables with scripting
                // DoStuff();
                continue;

            foreach (var reactor in castableXml.Effects?.Reactors)
            {
                if (X + reactor.RelativeX < byte.MinValue || X + reactor.RelativeX > byte.MaxValue ||
                    Y + reactor.RelativeY < byte.MinValue || Y + reactor.RelativeY > byte.MaxValue)
                    continue;
                var actualX = (byte)(X + reactor.RelativeX);
                var actualY = (byte)(Y + reactor.RelativeY);
                var reactorObj =
                    new Reactor(actualX, actualY, tar.Map, reactor, this, $"{Name}'s {castableXml.Name}")
                    {
                        Sprite = reactor.Sprite,
                        CreatedBy = Guid,
                        Uses = Convert.ToInt32(FormulaParser.Eval(reactor.Uses,
                            new FormulaEvaluation { Castable = castableXml, Source = this }))
                    };
                // Don't insert a reactor with no uses into the world
                if (reactorObj.Uses == 0) continue;
                World.Insert(reactorObj);
                tar.Map.InsertReactor(reactorObj);
                reactorObj.OnSpawn();
            }

            if (castableXml.Effects?.Damage != null && !castableXml.Effects.Damage.IsEmpty)
            {
                ElementType attackElement;
                var damageOutput = NumberCruncher.CalculateDamage(castableXml, tar, this);
                attackElement = castableXml.Element switch
                {
                    ElementType.RandomTemuair => (ElementType)Random.Shared.Next(1, 7),
                    ElementType.RandomExpanded => (ElementType)Random.Shared.Next(1, 10),
                    ElementType.Belt => Equipment?.Belt?.Element ?? ElementType.None,
                    ElementType.Necklace => Equipment?.Necklace?.Element ?? ElementType.None,
                    ElementType.None => ElementType.None,
                    ElementType.Current => Stats.OffensiveElementOverride != ElementType.None
                        ? Stats.OffensiveElementOverride
                        : Stats.BaseOffensiveElement,
                    _ => castableXml.Element
                };

                GameLog.UserActivityInfo(
                    $"UseCastable: {Name} casting {castableXml.Name} - target: {tar.Name} damage: {damageOutput}, element {attackElement}");

                tar.Damage(damageOutput.Amount, attackElement, damageOutput.Type, damageOutput.Flags, this, castableXml,
                    false);
                if (tar is Monster m)
                    m.ThreatInfo.LastCaster = this;
                ProcessProcs(ProcEventType.OnHit, castableXml, tar);

                if (tar is User u && !castableXml.IsAssail)
                    u.SendSystemMessage($"{Name} attacks you with {castableXml.Name}.");

                if (this is User)
                    if (Equipment.Weapon is { Undamageable: false })
                        Equipment.Weapon.Durability -= 1 / (Equipment.Weapon.MaximumDurability *
                                                            (100 - Stats.Ac == 0 ? 1 : 100 - Stats.Ac));

                if (tar.Stats.Hp <= 0) deadMobs.Add(tar);
            }
            // Note that we ignore castables with both damage and healing effects present - one or the other.
            // A future improvement might be to allow more complex effects.
            else if (castableXml.Effects?.Heal != null && !castableXml.Effects.Heal.IsEmpty)
            {
                var healOutput = NumberCruncher.CalculateHeal(castableXml, tar, this);
                tar.Heal(healOutput, this, castableXml);
                ProcessProcs(ProcEventType.OnHit, castableXml, tar);
                if (this is User)
                {
                    GameLog.UserActivityInfo(
                        $"UseCastable: {Name} casting {castableXml.Name} - target: {tar.Name} healing: {healOutput}");
                    if (Equipment.Weapon is { Undamageable: false })
                        Equipment.Weapon.Durability -= 1 / (Equipment.Weapon.MaximumDurability *
                                                            (100 - Stats.Ac == 0 ? 1 : 100 - Stats.Ac));
                }
            }

            // Handle statuses

            foreach (var status in castableXml.AddStatuses)
                if (World.WorldData.TryGetValue<Status>(status.Value, out var applyStatus))
                {
                    var duration = status.Duration == 0 ? applyStatus.Duration : status.Duration;
                    var tick = status.Tick == 0 ? applyStatus.Tick : status.Tick;
                    GameLog.UserActivityInfo(
                        $"UseCastable: {Name} casting {castableXml.Name} - applying status {status.Value} - duration {duration}");
                    if (tar.CurrentStatusInfo.Count > 0)
                    {
                        var overlap = tar.CurrentStatusInfo.Where(predicate: x => applyStatus.IsCategory(x.Category))
                            .ToList();
                        if (overlap.Any())
                        {
                            if (this is User user)
                                user.SendSystemMessage(
                                    $"Another {overlap.First().Category} already affects your target.");

                            continue;
                        }
                    }

                    // Check immunities
                    var apply = true;
                    CreatureImmunity immunity = null;
                    if (tar is Monster m && (castableXml.CategoryList.Any(predicate: x =>
                                                 m.BehaviorSet.ImmuneToCastableCategory(x, out immunity)) ||
                                             m.BehaviorSet.ImmuneToStatus(applyStatus, out immunity) ||
                                             applyStatus.CategoryList.Any(predicate: x =>
                                                 m.BehaviorSet.ImmuneToStatusCategory(x, out immunity))))
                    {
                        m.SendImmunityMessage(immunity, this);
                        apply = false;
                    }

                    if (apply)
                        tar.ApplyStatus(new CreatureStatus(applyStatus, tar, castableXml, this, duration, tick,
                            status.Intensity));
                }
                else
                {
                    GameLog.UserActivityError(
                        $"UseCastable: {Name} casting {castableXml.Name} - failed to add status {status.Value}, does not exist!");
                }

            foreach (var status in castableXml.RemoveStatuses)
                if (status.IsCategory)
                {
                    for (var x = 0; x <= status.Quantity; x++)
                    {
                        var toRemove = tar.Statuses.FirstOrDefault(predicate: x => string.Equals(x.Category,
                            status.Value,
                            StringComparison.CurrentCultureIgnoreCase));
                        if (toRemove == null) break;
                        tar.RemoveStatus(toRemove.Icon);
                        GameLog.UserActivityInfo(
                            $"UseCastable: {Name} casting {castableXml.Name} - removing status category {status.Value}");
                    }
                }
                else if (World.WorldData.TryGetValue<Status>(status.Value.ToLower(), out var applyStatus))
                {
                    GameLog.UserActivityError(
                        $"UseCastable: {Name} casting {castableXml.Name} - removing status {status}");
                    tar.RemoveStatus(applyStatus.Icon);
                }
                else
                {
                    GameLog.UserActivityError(
                        $"UseCastable: {Name} casting {castableXml.Name} - failed to remove status {status}, does not exist!");
                }

            LastTarget = tar;
        }

        // Now flood away
        foreach (var dead in deadMobs)
            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.HandleDeath, dead));
        Condition.Casting = false;
        return true;
    }

    public void SendCastLine(ServerPacket packet)
    {
        GameLog.DebugFormat("SendCastLine");
        GameLog.DebugFormat($"SendCastLine byte format is: {BitConverter.ToString(packet.ToArray())}");
        foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
        {
            var nPacket = packet.Clone();
            GameLog.DebugFormat($"SendCastLine to {user.Name}");
            user.Enqueue(nPacket);
        }
    }

    public virtual void UpdateAttributes(StatUpdateFlags flags) { }

    public virtual bool Walk(Direction direction)
    {
        lock (_lock)
        {
            int oldX = X, oldY = Y, newX = X, newY = Y;
            var arrivingViewport = Rectangle.Empty;
            var departingViewport = Rectangle.Empty;
            var commonViewport = Rectangle.Empty;
            var halfViewport = Game.ActiveConfiguration.Constants.ViewportSize / 2;
            Warp targetWarp;

            switch (direction)
            {
                // Calculate the differences (which are, in all cases, rectangles of height 12 / width 1 or vice versa)
                // between the old and new viewpoints. The arrivingViewport represents the objects that need to be notified
                // of this object's arrival (because it is now within the viewport distance), and departingViewport represents
                // the reverse. We later use these rectangles to query the quadtree to locate the objects that need to be 
                // notified of an update to their AOI (area of interest, which is the object's viewport calculated from its
                // current position).

                case Direction.North:
                    --newY;
                    arrivingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport,
                        Game.ActiveConfiguration.Constants.ViewportSize,
                        1);
                    departingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport,
                        Game.ActiveConfiguration.Constants.ViewportSize,
                        1);
                    break;
                case Direction.South:
                    ++newY;
                    arrivingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport,
                        Game.ActiveConfiguration.Constants.ViewportSize,
                        1);
                    departingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport,
                        Game.ActiveConfiguration.Constants.ViewportSize,
                        1);
                    break;
                case Direction.West:
                    --newX;
                    arrivingViewport = new Rectangle(newX - halfViewport, oldY - halfViewport, 1,
                        Game.ActiveConfiguration.Constants.ViewportSize);
                    departingViewport = new Rectangle(oldX + halfViewport, oldY - halfViewport, 1,
                        Game.ActiveConfiguration.Constants.ViewportSize);
                    break;
                case Direction.East:
                    ++newX;
                    arrivingViewport = new Rectangle(oldX + halfViewport, oldY - halfViewport, 1,
                        Game.ActiveConfiguration.Constants.ViewportSize);
                    departingViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport, 1,
                        Game.ActiveConfiguration.Constants.ViewportSize);
                    break;
            }

            var isWarp = Map.Warps.TryGetValue(new Tuple<byte, byte>((byte)newX, (byte)newY), out targetWarp);

            // Now that we know where we are going, perform some sanity checks.
            // Is the player trying to walk into a wall, or off the map?

            if (newX >= Map.X || newY >= Map.Y || newX < 0 || newY < 0)
            {
                Refresh();
                return false;
            }

            if (Map.IsWall(newX, newY))
            {
                Refresh();
                return false;
            }

            // Is the player trying to walk into an occupied tile?
            foreach (var obj in Map.GetTileContents((byte)newX, (byte)newY))
            {
                GameLog.DebugFormat("Collision check: found obj {0}", obj.Name);
                if (obj is Creature)
                {
                    GameLog.DebugFormat("Walking prohibited: found {0}", obj.Name);
                    Refresh();
                    return false;
                }
            }

            // Is this user entering a forbidden (by level or otherwise) warp?
            if (isWarp)
            {
                if (targetWarp.MinimumLevel > Stats.Level)
                {
                    Refresh();
                    return false;
                }

                if (targetWarp.MaximumLevel < Stats.Level)
                {
                    Refresh();
                    return false;
                }
            }

            // Calculate the common viewport between the old and new position

            commonViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport,
                Game.ActiveConfiguration.Constants.ViewportSize,
                Game.ActiveConfiguration.Constants.ViewportSize);
            commonViewport.Intersect(new Rectangle(newX - halfViewport, newY - halfViewport,
                Game.ActiveConfiguration.Constants.ViewportSize,
                Game.ActiveConfiguration.Constants.ViewportSize));
            GameLog.DebugFormat("Moving from {0},{1} to {2},{3}", oldX, oldY, newX, newY);
            GameLog.DebugFormat("Arriving viewport is a rectangle starting at {0}, {1}", arrivingViewport.X,
                arrivingViewport.Y);
            GameLog.DebugFormat("Departing viewport is a rectangle starting at {0}, {1}", departingViewport.X,
                departingViewport.Y);
            GameLog.DebugFormat("Common viewport is a rectangle starting at {0}, {1} of size {2}, {3}",
                commonViewport.X,
                commonViewport.Y, commonViewport.Width, commonViewport.Height);

            X = (byte)newX;
            Y = (byte)newY;
            Direction = direction;
            // Objects in the common viewport receive a "walk" (0x0C) packet
            // Objects in the arriving viewport receive a "show to" (0x33) packet
            // Objects in the departing viewport receive a "remove object" (0x0E) packet

            foreach (var obj in Map.EntityTree.GetObjects(commonViewport))
                if (obj != this && obj is User)
                {
                    var user = obj as User;
                    GameLog.DebugFormat("Sending walk packet for {0} to {1}", Name, user.Name);
                    var x0C = new ServerPacket(0x0C);
                    x0C.WriteUInt32(Id);
                    x0C.WriteUInt16((byte)oldX);
                    x0C.WriteUInt16((byte)oldY);
                    x0C.WriteByte((byte)direction);
                    x0C.WriteByte(0x00);
                    user.Enqueue(x0C);
                }

            Map.EntityTree.Move(this);

            foreach (var obj in Map.EntityTree.GetObjects(arrivingViewport).Distinct())
            {
                obj.AoiEntry(this);
                AoiEntry(obj);
            }

            foreach (var obj in Map.EntityTree.GetObjects(departingViewport).Distinct())
            {
                obj.AoiDeparture(this);
                AoiDeparture(obj);
            }
        }

        // Have we entered a reactor?
        if (Map.Reactors.TryGetValue((X, Y), out var reactors))
            foreach (var r in reactors.Values)
                r.OnEntry(this);

        HasMoved = true;
        return true;
    }

    public virtual bool Turn(Direction direction)
    {
        Direction = direction;

        foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
        {
            if (obj is User)
            {
                var user = obj as User;
                var x11 = new ServerPacket(0x11);
                x11.WriteUInt32(Id);
                x11.WriteByte((byte)direction);
                user.Enqueue(x11);
            }

            if (obj is Monster)
            {
                var mob = obj as Monster;
                var x11 = new ServerPacket(0x11);
                x11.WriteUInt32(Id);
                x11.WriteByte((byte)direction);
                foreach (var user in Map.EntityTree.GetObjects(Map.GetViewport(mob.X, mob.Y)).OfType<User>().ToList())
                    user.Enqueue(x11);
            }
        }

        return true;
    }

    public virtual void Motion(byte motion, short speed)
    {
        foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
        {
            if (obj is not User user) continue;
            user.SendAnimation(Id, motion, speed);
        }
    }

    public virtual void Heal(double heal, Creature source = null, Castable castable = null)
    {
        var bonusHeal = heal * Stats.BaseInboundHealModifier + (heal * source?.Stats.BaseOutboundHealModifier ?? 0.0);
        heal += bonusHeal;

        OnHeal(new HealEvent
        {
            Amount = Convert.ToUInt32(heal),
            BonusHeal = Convert.ToInt32(bonusHeal),
            Source = source,
            SourceCastable = castable,
            Target = this
        });

        if (AbsoluteImmortal || PhysicalImmortal) return;
        if (Stats.Hp == Stats.MaximumHp) return;
        Stats.Hp = heal > uint.MaxValue ? Stats.MaximumHp : Math.Min(Stats.MaximumHp, (uint)(Stats.Hp + heal));
        SendDamageUpdate(this);
    }

    public virtual void RegenerateMp(double mp, Creature regenerator = null)
    {
        if (AbsoluteImmortal)
            return;

        if (Stats.Mp == Stats.MaximumMp || mp > Stats.MaximumMp)
            return;

        Stats.Mp = mp > uint.MaxValue ? Stats.MaximumMp : Math.Min(Stats.MaximumMp, (uint)(Stats.Mp + mp));
    }

    public virtual void Damage(double damage, ElementType element = ElementType.None,
        DamageType damageType = DamageType.Direct, DamageFlags damageFlags = DamageFlags.None,
        Creature attacker = null, Castable castable = null, bool onDeath = true)
    {
        if (this is Monster ms && !Condition.Alive) return;
        var damageEvent = new DamageEvent();
        damageEvent.Source = attacker;
        damageEvent.SourceCastable = castable;
        damageEvent.Target = this;
        damageEvent.Element = element;
        damageEvent.Type = damageType;

        // Handle dodging first
        if (damageType == DamageType.Physical && Stats.Dodge > 0 && !damageFlags.HasFlag(DamageFlags.NoDodge))
        {
            var dodgeReduction = attacker == null ? 0 : attacker.Stats.Hit;
            if (Random.Shared.NextDouble() <= Stats.Dodge * dodgeReduction)
            {
                Effect(115, 100);
                return;
            }
        }

        if (damageType == DamageType.Magical && Stats.MagicDodge > 0 && !damageFlags.HasFlag(DamageFlags.NoDodge))
        {
            var dodgeReduction = attacker == null ? 0 : attacker.Stats.Hit;
            if (Random.Shared.NextDouble() <= Stats.MagicDodge * dodgeReduction)
            {
                Effect(33, 100);
                return;
            }
        }

        if (attacker is User && this is Monster)
        {
            if (FirstHitter == null || !World.UserConnected(FirstHitter.Name) ||
                (DateTime.Now - LastHitTime).TotalSeconds > Game.ActiveConfiguration.Constants.MonsterTaggingTimeout)
                FirstHitter = attacker;
            if (attacker != FirstHitter && !((FirstHitter as User).Group?.Members.Contains(attacker) ?? false)) return;
        }

        LastHitTime = DateTime.Now;

        // handle ac
        if (damageType != DamageType.Direct && !damageFlags.HasFlag(DamageFlags.NoResistance))
        {
            double armor = Stats.Ac * -1 + 100;
            var reduction = damage * (armor / (armor + 50));
            damage -= reduction;
            damageEvent.ArmorReduction = Convert.ToInt32(reduction);
        }

        // handle elements

        if (damageType != DamageType.Direct && !damageFlags.HasFlag(DamageFlags.NoElement))
        {
            var elementTable = Game.World.WorldData.Get<ElementTable>("ElementTable");
            var multiplierList = elementTable?.Source.First(predicate: x => x.Element == element).Target;
            if (multiplierList != null)
            {
                var multiplier = multiplierList
                    .FirstOrDefault(predicate: x => x.Element == Stats.BaseDefensiveElement)?.Multiplier ?? 1.0;
                damageEvent.ElementalInteraction = Convert.ToInt32(damage * multiplier);
                damage *= multiplier;
            }
        }

        // Handle dmg/mr/crit/magiccrit
        if (attacker != null && damageType != DamageType.Direct)
        {
            damage += damage * attacker.Stats.Dmg;
            damageEvent.BonusDmg = Convert.ToInt32(damage * attacker.Stats.Dmg);

            if (damageType == DamageType.Magical && !damageFlags.HasFlag(DamageFlags.NoResistance))
            {
                damage += damage * Stats.Mr;
                damageEvent.MagicResisted = Convert.ToInt32(damage * Stats.Mr);
            }

            if (attacker.Stats.Crit > 0 && damageType == DamageType.Physical &&
                !damageFlags.HasFlag(DamageFlags.NoCrit))
                if (Random.Shared.NextDouble() <= attacker.Stats.Crit)
                {
                    damage += damage * 0.5;
                    damageEvent.Crit = true;
                    Effect(24, 100);
                }

            if (attacker.Stats.MagicCrit > 0 && damageType == DamageType.Magical &&
                !damageFlags.HasFlag(DamageFlags.NoCrit))
                if (Random.Shared.NextDouble() <= attacker.Stats.Crit)
                {
                    damage += damage * 2;
                    damageEvent.MagicCrit = true;
                    Effect(24, 100);
                }

            // negative dodge, aka "i rolled a 1 and hit myself in the face"
            if (damageType != DamageType.Magical && Stats.Dodge < 0)
                if (Random.Shared.Next() <= Stats.Dodge * -1)
                {
                    Effect(68, 100);
                    var selfDamage = damage * -1 * 0.25;
                    (attacker as User)?.SendSystemMessage("You fumble, and strike yourself hard!");
                    attacker.World.EnqueueGuidStatUpdate(attacker.Guid,
                        new StatInfo { DeltaHp = (long)selfDamage },
                        new StatChangeEvent
                        {
                            Amount = Convert.ToUInt32(selfDamage),
                            EventType = CombatLogEventType.CriticalFailure,
                            Source = attacker,
                            Target = this
                        });
                    return;
                }

            // negative magic dodge, aka "i rolled a 1 and my robes exploded"
            if (damageType == DamageType.Magical && Stats.MagicDodge < 0)
                if (Random.Shared.NextDouble() <= Stats.MagicDodge * -1)
                {
                    Effect(68, 100);
                    var selfDamage = damage * -1 * 0.25;
                    (attacker as User)?.SendSystemMessage("You stammer, and flames envelop you!");
                    attacker.World.EnqueueGuidStatUpdate(attacker.Guid,
                        new StatInfo { DeltaHp = (long)selfDamage },
                        new StatChangeEvent
                        {
                            Amount = Convert.ToUInt32(selfDamage),
                            EventType = CombatLogEventType.CriticalMagicFailure,
                            Source = attacker,
                            Target = this
                        });
                    return;
                }
        }


        // Apply elemental resistances, if they exist
        var resisted = Stats.ElementalModifiers?.GetResistance(element) ?? 0.0;
        damage -= damage * resisted;
        damageEvent.ElementalResisted = Convert.ToInt32(damage * resisted);

        // Apply augmentation, if exists
        var augment = attacker?.Stats?.ElementalModifiers?.GetAugment(element) ?? 0.0;
        damage += damage * augment;
        damageEvent.ElementalAugmented = Convert.ToInt32(damage * augment);

        // Handle straight damage buff / debuff from inbound modifiers
        var modified = damage * Stats.InboundDamageModifier +
                       (damage * attacker?.Stats?.OutboundDamageModifier ?? 0.0);
        damage += modified;
        damageEvent.ModifierDmg = Convert.ToInt32(modified);


        // Negative damage (possible with augments and resistances) 
        if (damage < 0) damage = 1;

        // lastly, handle shield

        if (Stats.Shield > 0)
        {
            if (Stats.Shield >= damage)
            {
                damage = 0;
                Stats.Shield -= damage;
            }
            else
            {
                damage -= Stats.Shield;
                Stats.Shield = 0;
            }
        }

        // Now, normalize damage for uint (max hp)
        var normalized = (uint)damage;

        if (normalized > Stats.Hp && damageFlags.HasFlag(DamageFlags.Nonlethal))
            normalized = Stats.Hp - 1;
        else if (normalized > Stats.Hp)
            normalized = Stats.Hp;

        switch (damageType)
        {
            case DamageType.Physical when AbsoluteImmortal || PhysicalImmortal || Condition.IsInvulnerable:
            case DamageType.Magical when AbsoluteImmortal || MagicalImmortal || Condition.IsInvulnerable:
            case DamageType.Direct when AbsoluteImmortal || Condition.IsInvulnerable:
                damageEvent.Applied = false;
                return;
        }

        damageEvent.Amount = normalized;
        _mLastHitter = attacker?.Id ?? 0;
        OnDamage(damageEvent);

        // Handle reflection and steals. For now these are handled as straight hp/mp effects
        // without mitigation.
        if (Stats.ReflectMagical > 0 && damageType == DamageType.Magical && attacker != null)
        {
            var reflected = Stats.ReflectMagical * normalized;
            if (reflected > 0)
                attacker.World.EnqueueGuidStatUpdate(attacker.Guid, new StatInfo { DeltaHp = (long)(reflected * -1) },
                    new StatChangeEvent
                    {
                        Amount = Convert.ToUInt32(reflected * -1),
                        EventType = CombatLogEventType.ReflectMagical,
                        Source = attacker,
                        Target = this
                    });
        }

        if (Stats.ReflectPhysical > 0 && damageType == DamageType.Physical && attacker != null)
        {
            var reflected = Stats.ReflectPhysical * normalized;
            if (reflected > 0)
                attacker.World.EnqueueGuidStatUpdate(attacker.Guid, new StatInfo { DeltaHp = (long)reflected * -1 },
                    new StatChangeEvent
                    {
                        Amount = Convert.ToUInt32(reflected * -1),
                        EventType = CombatLogEventType.ReflectPhysical,
                        Source = attacker,
                        Target = this
                    });
        }

        if (attacker != null && attacker.Stats.LifeSteal > 0)
        {
            var stolen = normalized * Stats.LifeSteal;
            if (stolen > 0)
                attacker.World.EnqueueGuidStatUpdate(attacker.Guid, new StatInfo { DeltaHp = (long)stolen },
                    new StatChangeEvent
                    {
                        Amount = Convert.ToUInt32(stolen),
                        EventType = CombatLogEventType.LifeSteal,
                        Source = attacker,
                        Target = this
                    });
        }

        if (attacker != null && attacker.Stats.ManaSteal > 0)
        {
            var stolen = normalized * Stats.ManaSteal;
            if (stolen > 0)
                attacker.World.EnqueueGuidStatUpdate(attacker.Guid, new StatInfo { DeltaMp = (long)stolen },
                    new StatChangeEvent
                    {
                        Amount = Convert.ToUInt32(stolen),
                        EventType = CombatLogEventType.LifeSteal,
                        Source = attacker,
                        Target = this
                    });
        }

        // Lastly, handle damage to MP redirection

        if (attacker != null && Stats.InboundDamageToMp > 0)
        {
            var redirected = Stats.InboundDamageToMp * normalized;
            if (redirected > 0)
                attacker.World.EnqueueGuidStatUpdate(attacker.Guid, new StatInfo { DeltaMp = (long)redirected },
                    new StatChangeEvent
                    {
                        Amount = Convert.ToUInt32(redirected),
                        EventType = CombatLogEventType.LifeSteal,
                        Source = attacker,
                        Target = this,
                        SourceCastable = castable
                    });
        }

        if ((MagicalImmortal && damageType != DamageType.Magical) ||
            (PhysicalImmortal && damageType != DamageType.Physical) ||
            !AbsoluteImmortal)
            Stats.Hp = (int)Stats.Hp - normalized < 0 ? 0 : Stats.Hp - normalized;

        SendDamageUpdate(this);

        // TODO: Separate this out into a control message
        if (Stats.Hp != 0 || !onDeath) return;
        if (this is Monster) Condition.Alive = false;
        OnDeath();
    }

    private void SendDamageUpdate(Creature creature)
    {
        if (Map == null) return;
        var percent = creature.Stats.Hp / (double)creature.Stats.MaximumHp * 100;
        var healthbar = new ServerPacketStructures.HealthBar { CurrentPercent = (byte)percent, ObjId = creature.Id };

        foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
        {
            var nPacket = healthbar.Packet().Clone();
            user.Enqueue(nPacket);
        }
    }

    public override void ShowTo(IVisible obj)
    {
        if (obj is not User user) return;
        user.SendVisibleCreature(this);
    }

    public virtual void Refresh() { }

    public void SetCookie(string cookieName, string value)
    {
        Cookies[cookieName] = value;
    }

    public void SetSessionCookie(string cookieName, string value)
    {
        SessionCookies[cookieName] = value;
    }

    public IReadOnlyDictionary<string, string> GetCookies() => Cookies;

    public IReadOnlyDictionary<string, string> GetSessionCookies() => SessionCookies;

    public string GetCookie(string cookieName) => Cookies.TryGetValue(cookieName, out var value) ? value : null;

    public string GetSessionCookie(string cookieName) =>
        SessionCookies.TryGetValue(cookieName, out var value) ? value : null;

    public bool HasCookie(string cookieName) => Cookies.ContainsKey(cookieName);

    public bool HasSessionCookie(string cookieName) => SessionCookies.ContainsKey(cookieName);

    public bool DeleteCookie(string cookieName) => Cookies.Remove(cookieName);

    public bool DeleteSessionCookie(string cookieName) => SessionCookies.Remove(cookieName);


    #region Status handling

    /// <summary>
    ///     Apply a given status to a player.
    /// </summary>
    /// <param name="status">The status to apply to the player.</param>
    public bool ApplyStatus(ICreatureStatus status, bool sendUpdates = true)
    {
        // Check for immunities to status effects
        if (this is Monster { BehaviorSet: not null } m)
            if (m.BehaviorSet.ImmuneToStatus(status.Name, out var immunity) ||
                m.BehaviorSet.ImmuneToStatusCategory(status.Info.Category, out immunity))
            {
                m.SendImmunityMessage(immunity);
                return false;
            }

        if (!CurrentStatuses.TryAdd(status.Icon, status))
            return false;
        if (this is User u)
        {
            if (sendUpdates)
                u.SendStatusUpdate(status);

            foreach (var reactor in Map.EntityTree.GetObjects(GetViewport()).OfType<Reactor>())
                if (reactor.VisibleToStatuses?.Contains(status.Name) ?? false)
                    reactor.ShowTo(this);
        }

        if (this is Monster m2)
            m2.ThreatInfo.LastCaster = status.Source;

        status.OnStart(sendUpdates);
        if (sendUpdates)
            UpdateAttributes(StatUpdateFlags.Full);
        Game.World.EnqueueStatusCheck(this);
        return true;
    }

    /// <summary>
    ///     Remove a status from a client, firing the appropriate OnEnd events and removing the icon from the status bar.
    /// </summary>
    /// <param name="status">The status to remove.</param>
    /// <param name="onEnd">Whether or not to run the onEnd event for the status removal.</param>
    private void _removeStatus(ICreatureStatus status, bool onEnd = true)
    {
        if (onEnd)
        {
            if (status.Expired)
                status.OnExpire();
            else
                status.OnEnd();
        }

        if (this is User) (this as User).SendStatusUpdate(status, true);
        if (this is Monster)
            Game.World.RemoveStatusCheck(this);
    }

    /// <summary>
    ///     Remove a status from a client.
    /// </summary>
    /// <param name="icon">The icon of the status we are removing.</param>
    /// <param name="onEnd">Whether or not to run the onEnd effect for the status.</param>
    /// <returns></returns>
    public bool RemoveStatus(ushort icon, bool onEnd = true)
    {
        ICreatureStatus status;
        if (!CurrentStatuses.TryRemove(icon, out status)) return false;
        _removeStatus(status, onEnd);
        UpdateAttributes(StatUpdateFlags.Full);
        if (this is User u)
            foreach (var reactor in Map.EntityTree.GetObjects(GetViewport()).OfType<Reactor>())
                if (reactor.VisibleToStatuses?.Contains(status.Name) ?? false)
                    reactor.AoiDeparture(this);
        return true;
    }

    public bool TryGetStatus(string name, out ICreatureStatus status)
    {
        status = CurrentStatuses.Values.FirstOrDefault(predicate: s => s.Name == name);
        return status != null;
    }

    /// <summary>
    ///     Remove all statuses from a user.
    /// </summary>
    public void RemoveAllStatuses()
    {
        lock (CurrentStatuses)
        {
            foreach (var status in CurrentStatuses.Values) _removeStatus(status, false);

            CurrentStatuses.Clear();
            GameLog.Debug($"Current status count is {CurrentStatuses.Count}");
        }
    }

    /// <summary>
    ///     Process all the given status ticks for a creature's active statuses.
    /// </summary>
    public void ProcessStatusTicks()
    {
        foreach (var kvp in CurrentStatuses)
        {
            GameLog.DebugFormat("OnTick: {0}, {1}", Name, kvp.Value.Name);

            if (kvp.Value.Expired)
            {
                var removed = RemoveStatus(kvp.Key);
                if (removed && kvp.Value.Name.ToLower() == "coma")
                    // Coma removal from expiration means: dead
                    (this as User).OnDeath();

                GameLog.DebugFormat($"Status {kvp.Value.Name} has expired: removal was {removed}");
            }

            if (kvp.Value.ElapsedSinceTick >= kvp.Value.Tick)
            {
                kvp.Value.OnTick();
                if (this is User) (this as User).SendStatusUpdate(kvp.Value);
            }
        }
    }

    public int ActiveStatusCount => CurrentStatuses.Count;

    #endregion
}