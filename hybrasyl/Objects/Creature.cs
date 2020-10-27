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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Hybrasyl.Enums;
using Newtonsoft.Json;
using Hybrasyl.Scripting;

namespace Hybrasyl.Objects
{

    public class Creature : VisibleObject
    {
        private readonly object _lock = new object();

        [JsonProperty(Order = 2)]
        public StatInfo Stats { get; set; }
        [JsonProperty(Order = 3)]
        public ConditionInfo Condition { get; set; }

        protected ConcurrentDictionary<ushort, ICreatureStatus> _currentStatuses;

        [JsonProperty]
        public List<StatusInfo> Statuses { get; set; }

        public List<StatusInfo> CurrentStatusInfo => _currentStatuses.Values.Select(e => e.Info).ToList();

        [JsonProperty]
        public uint Gold { get; set; }

        [JsonProperty]
        public Inventory Inventory { get; protected set; }

        [JsonProperty("Equipment")]
        public Inventory Equipment { get; protected set; }

        public Creature()
        {
            Gold = 0;
            Inventory = new Inventory(59);
            Equipment = new Inventory(18);
            Stats = new StatInfo();
            Condition = new ConditionInfo(this);
            _currentStatuses = new ConcurrentDictionary<ushort, ICreatureStatus>();
            LastHitTime = DateTime.MinValue;
            Statuses = new List<StatusInfo>();
        }

        public override void OnClick(User invoker)
        {
            var prepend = "";
            
            invoker.SendSystemMessage(prepend + Name);
        }

        public Creature GetDirectionalTarget(Xml.Direction direction)
        {
            VisibleObject obj;

            switch (direction)
            {
                case Xml.Direction.East:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X + 1 && x.Y == Y && x is Creature);
                    }
                    break;
                case Xml.Direction.West:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X - 1 && x.Y == Y && x is Creature);
                    }
                    break;
                case Xml.Direction.North:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X && x.Y == Y - 1 && x is Creature);
                    }
                    break;
                case Xml.Direction.South:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X && x.Y == Y + 1 && x is Creature);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }

            if (obj is Creature) return obj as Creature;
            return null;
        }

        /// <summary>
        /// Get targets from this creature, in the specified direction, up to the specified radius of tiles.
        /// </summary>
        /// <param name="direction">The direction to find targets</param>
        /// <param name="radius">The radius to search</param>
        /// <returns></returns>
        public List<Creature> GetDirectionalTargets(Xml.Direction direction, int radius = 1)
        {
            var ret = new List<Creature>();
            Rectangle rect = new Rectangle(0, 0, 0, 0);

            switch (direction)
            {
                case Xml.Direction.East:
                    {
                        rect = new Rectangle(X + 1, Y, radius, 1);
                    }
                    break;
                case Xml.Direction.West:
                    {
                        rect = new Rectangle(X - radius, Y, radius, 1);
                    }
                    break;
                case Xml.Direction.South:
                    {
                        rect = new Rectangle(X, Y + 1, 1, radius);
                    }
                    break;
                case Xml.Direction.North:
                    {
                        rect = new Rectangle(X, Y - radius, 1, radius);
                    }
                    break;
            }
            GameLog.UserActivityInfo($"GetDirectionalTargets: {rect.X}, {rect.Y} {rect.Height}, {rect.Width}");
            ret.AddRange(Map.EntityTree.GetObjects(rect).Where(obj => obj is Creature).Select(e => e as Creature));
            return ret;
        }

        public Dictionary<Xml.Direction, List<Creature>> GetFacingTargets(int radius = 1)
        {
            var ret = new Dictionary<Xml.Direction, List<Creature>>();
            foreach (Xml.Direction direction in Enum.GetValues(typeof(Xml.Direction)))
            {
                ret[direction] = new List<Creature>();
                // TODO: null check, remove 
                ret[direction].AddRange(GetDirectionalTargets(direction, radius));
            }
            return ret;
        }

        public virtual List<Creature> GetTargets(Xml.Castable castable, Creature target = null)
        {
            IEnumerable<Creature> actualTargets = new List<Creature>();
            var intents = castable.Intents;
            List<VisibleObject> possibleTargets = new List<VisibleObject>();
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
                    if (intent.UseType == Xml.SpellUseType.Target)
                    {
                        // Exact clicked target
                        possibleTargets.Add(target);                        
                        //GameLog.UserActivityInfo("GetTarget: exact clicked target");
                    }
                    else if (intent.UseType == Xml.SpellUseType.NoTarget)
                    {
                        possibleTargets.Add(this);
                        //GameLog.UserActivityInfo("GetTarget: notarget, self");
                        if (intent.Flags.Contains(Xml.IntentFlags.Group))
                        {
                            // Add group members
                            if (this is User uo)
                                if (uo.Group != null)
                                    possibleTargets.AddRange(uo.Group.Members.Where(x => x.Connected));
                        }
                    }
                    else if (intent.UseType != Xml.SpellUseType.NoTarget)
                        GameLog.UserActivityWarning($"Unhandled intent type {intent.UseType}, ignoring");
                }

                if (intent.Map != null)
                {
                    // add entire map
                    //GameLog.UserActivityInfo("GetTarget: adding map targets");
                    possibleTargets.AddRange(Map.EntityTree.GetAllObjects().Where(e => e is Creature));
                }

                if (intent.UseType == Xml.SpellUseType.NoTarget)
                {
                    origin = this;
                    //GameLog.UserActivityInfo($"GetTarget: origin is {this.Name} at {this.X}, {this.Y}");
                }
                else
                {
                    //GameLog.UserActivityInfo($"GetTarget: origin is {target.Name} at {target.X}, {target.Y}");
                    origin = target;
                }

                // Handle shapes
                foreach (var cross in intent.Cross)
                {
                    // Process cross targets
                    foreach (Xml.Direction direction in Enum.GetValues(typeof(Xml.Direction)))
                    {
                        //GameLog.UserActivityInfo($"GetTarget: cross, {direction}, origin {origin.Name}, radius {cross.Radius}");
                        possibleTargets.AddRange(origin.GetDirectionalTargets(direction, cross.Radius));                       
                    }
                    // Add origin and let flags sort it out
                    possibleTargets.Add(origin);
                }
                foreach (var line in intent.Line)
                {
                    // Process line targets
                    //GameLog.UserActivityInfo($"GetTarget: line, {line.Direction}, origin {origin.Name}, length {line.Length}");
                    possibleTargets.AddRange(origin.GetDirectionalTargets(origin.GetIntentDirection(line.Direction), line.Length));
                    // Similar to above, add origin
                    possibleTargets.Add(origin);
                }
                foreach (var square in intent.Square)
                {
                    // Process square targets
                    var r = (square.Side - 1) / 2;
                    var rect = new Rectangle(origin.X - r, origin.Y - r, square.Side, square.Side);
                    //GameLog.UserActivityInfo($"GetTarget: square, {origin.X - r}, {origin.Y - r} - origin {origin.Name}, side length {square.Side}");
                    possibleTargets.AddRange(origin.Map.EntityTree.GetObjects(rect).Where(e => e is Creature));
                }
                foreach (var tile in intent.Tile)
                {
                    // Process tile targets, which can have either direction OR relative x/y
                    if (tile.Direction == Xml.IntentDirection.None)
                    {
                        if (tile.RelativeX == 0 && tile.RelativeY == 0)
                        {
                            //GameLog.UserActivityInfo($"GetTarget: tile, origin {origin.Name}, RelativeX && RelativeY == 0, skipping");
                            continue;
                        }
                        else
                        {
                            //GameLog.UserActivityInfo($"GetTarget: tile, ({origin.X + tile.RelativeX}, {origin.Y + tile.RelativeY}, origin {origin.Name}");
                            possibleTargets.AddRange(origin.Map.GetTileContents(origin.X + tile.RelativeX, origin.Y + tile.RelativeY).Where(e => e is Creature));
                        }
                    }
                    else
                    {
                        //GameLog.UserActivityInfo($"GetTarget: tile, intent {tile.Direction}, direction {origin.GetIntentDirection(tile.Direction)}, origin {origin.Name}");
                        possibleTargets.Add(origin.GetDirectionalTarget(origin.GetIntentDirection(tile.Direction)));
                    }

                }
                List<Creature> possible = intent.MaxTargets > 0 ? possibleTargets.Take(intent.MaxTargets).OfType<Creature>().ToList() : possibleTargets.OfType<Creature>().ToList();
                if (possible != null && possible.Count > 0) 
                    actualTargets = actualTargets.Concat(possible);
                else GameLog.UserActivityInfo("GetTarget: No targets found");

                // Remove all merchants
                // TODO: perhaps improve with a flag or extend in the future
                actualTargets = actualTargets.Where(e => e is User || e is Monster);


                
                // Process intent flags

                var this_id = this.Id;
                
                if (this is Monster)
                {
                    // No hostile flag: remove players
                    if (!intent.Flags.Contains(Xml.IntentFlags.Hostile))
                    { 
                        finalTargets.AddRange(actualTargets.OfType<User>());
                    }
                    // No friendly flag: remove monsters
                    if (!intent.Flags.Contains(Xml.IntentFlags.Friendly))
                    {
                        finalTargets.AddRange(actualTargets.OfType<Monster>());
                    }
                    // Group / pvp: n/a
                }
                else if (this is User userobj)
                {
                    // No PVP flag: remove PVP flagged players
                    // No hostile flag: remove monsters
                    // No friendly flag: remove non-PVP flagged players
                    // No group flag: remove group members
                    if (intent.Flags.Contains(Xml.IntentFlags.Hostile))
                    {
                        finalTargets.AddRange(actualTargets.OfType<Monster>());
                    }
                    if (intent.Flags.Contains(Xml.IntentFlags.Friendly))
                    {
                        finalTargets.AddRange(actualTargets.OfType<User>().Where(e => e.Condition.PvpEnabled == false && e.Id != Id));
                    }
                    if (intent.Flags.Contains(Xml.IntentFlags.Pvp))
                    {

                        finalTargets.AddRange(actualTargets.OfType<User>().Where(e => e.Condition.PvpEnabled && e.Id != Id));
                    }
                    if (intent.Flags.Contains(Xml.IntentFlags.Group))
                    {
                        // Remove group members
                        if (userobj.Group != null)
                            finalTargets.AddRange(actualTargets.OfType<User>().Where(e => userobj.Group.Contains(e)));
                    }
                }
                // No Self flag: remove self 
                if (intent.Flags.Contains(Xml.IntentFlags.Self))
                {
                    GameLog.UserActivityInfo($"Trying to remove self: my id is {this.Id} and actualtargets contains {String.Join(',',actualTargets.Select(e => e.Id).ToList())}");
                    finalTargets.AddRange(actualTargets.Where(e => e.Id == Id));
                    GameLog.UserActivityInfo($"did it happen :o -  my id is {this.Id} and actualtargets contains {String.Join(',', actualTargets.Select(e => e.Id).ToList())}");
                }

            }

            return finalTargets;
        }


        // Restrict to (inclusive) range between [min, max]. Max is optional, and if its
        // not present then no upper limit will be enforced.
        private static long BindToRange(long start, long? min, long? max)
        {
            if (min != null && start < min)
                return min.GetValueOrDefault();
            else if (max != null && start > max)
                return max.GetValueOrDefault();
            else
                return start;
        }

        public DateTime LastHitTime { get; private set; }
        public Creature FirstHitter { get; internal set; }

        private uint _mLastHitter;
        public Creature LastHitter
        {
            get
            {
                return Game.World.Objects.ContainsKey(_mLastHitter) ? (Creature)Game.World.Objects[_mLastHitter] : null;
            }
            set
            {
                _mLastHitter = value?.Id ?? 0;
            }
        }

        public bool AbsoluteImmortal { get; set; }
        public bool PhysicalImmortal { get; set; }
        public bool MagicalImmortal { get; set; }


        #region Status handling

        /// <summary>
        /// Apply a given status to a player.
        /// </summary>
        /// <param name="status">The status to apply to the player.</param>
        public bool ApplyStatus(ICreatureStatus status, bool sendUpdates = true)
        {
            if (!_currentStatuses.TryAdd(status.Icon, status)) return false;
            if (this is User && sendUpdates)
            {
                (this as User).SendStatusUpdate(status);
            }
            status.OnStart(sendUpdates);
            if (sendUpdates)
                UpdateAttributes(StatUpdateFlags.Full);
            Game.World.EnqueueStatusCheck(this);
            return true;
        }

        /// <summary>
        /// Remove a status from a client, firing the appropriate OnEnd events and removing the icon from the status bar.
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
        /// Remove a status from a client.
        /// </summary>
        /// <param name="icon">The icon of the status we are removing.</param>
        /// <param name="onEnd">Whether or not to run the onEnd effect for the status.</param>
        /// <returns></returns>
        public bool RemoveStatus(ushort icon, bool onEnd = true)
        {
            ICreatureStatus status;
            if (!_currentStatuses.TryRemove(icon, out status)) return false;
            _removeStatus(status, onEnd);
            UpdateAttributes(StatUpdateFlags.Full);
            return true;
        }

        public bool TryGetStatus(string name, out ICreatureStatus status)
        {
            status = _currentStatuses.Values.FirstOrDefault(s => s.Name == name);
            return status != null;
        }

        /// <summary>
        /// Remove all statuses from a user.
        /// </summary>
        public void RemoveAllStatuses()
        {
            lock (_currentStatuses)
            {
                foreach (var status in _currentStatuses.Values)
                {
                    _removeStatus(status, false);
                }

                _currentStatuses.Clear();
                GameLog.Debug($"Current status count is {_currentStatuses.Count}");
            }
        }

        /// <summary>
        /// Process all the given status ticks for a creature's active statuses.
        /// </summary>
        public void ProcessStatusTicks()
        {
            foreach (var kvp in _currentStatuses)
            {
                GameLog.DebugFormat("OnTick: {0}, {1}", Name, kvp.Value.Name);

                if (kvp.Value.Expired)
                {
                    var removed = RemoveStatus(kvp.Key);
                    if (removed && kvp.Value.Name.ToLower() == "coma")
                    {
                        // Coma removal from expiration means: dead
                        (this as User).OnDeath();
                    }
                    GameLog.DebugFormat($"Status {kvp.Value.Name} has expired: removal was {removed}");
                }

                if (kvp.Value.ElapsedSinceTick >= kvp.Value.Tick)
                {
                    kvp.Value.OnTick();
                    if (this is User) (this as User).SendStatusUpdate(kvp.Value);
                }
            }
        }

        public int ActiveStatusCount => _currentStatuses.Count;

        #endregion

        public virtual bool UseCastable(Xml.Castable castObject, Creature target = null, Xml.SpawnCastable spawnCastable = null, bool assailAttack = false)
        {
            if (!Condition.CastingAllowed) return false;
            
            if (this is User) GameLog.UserActivityInfo($"UseCastable: {Name} begin casting {castObject.Name} on target: {target?.Name ?? "no target"} CastingAllowed: {Condition.CastingAllowed}");

            var damage = castObject.Effects.Damage;
            List<Creature> targets = new List<Creature>();

            if(this is Monster)
            {
                if(spawnCastable != null)
                {
                    damage = new Xml.CastableDamage
                    {
                        Simple = new Xml.SimpleQuantity
                        {
                            Min = (uint)spawnCastable.MinDmg,
                            Max = (uint)spawnCastable.MaxDmg
                        }
                    };

                    castObject.Effects.Damage = damage; //set damage based on spawncastable settings.
                    castObject.Element = spawnCastable.Element; //handle defined element without redoing a ton of code.
                }                
            }

            targets = GetTargets(castObject, target);

            // Quick checks
            // If no targets and is not an assail, do nothing
            if (targets.Count() == 0 && castObject.IsAssail == false && string.IsNullOrEmpty(castObject.Script))
            {
                GameLog.UserActivityInfo($"UseCastable: {Name}: no targets and not assail");
                return false;
            }
            // Is this a pvpable spell? If so, is pvp enabled?

            // We do these next steps to ensure effects are displayed uniformly and as fast as possible
            var deadMobs = new List<Creature>();
            if (castObject.Effects?.Animations?.OnCast != null)
            {
                foreach (var tar in targets)
                {
                    foreach (var user in tar.viewportUsers.ToList())
                    {
                        GameLog.UserActivityInfo($"UseCastable: Sending {user.Name} effect for {Name}: {castObject.Effects.Animations.OnCast.Target.Id}");
                        user.SendEffect(tar.Id, castObject.Effects.Animations.OnCast.Target.Id, castObject.Effects.Animations.OnCast.Target.Speed);
                    }
                }
                if (castObject.Effects?.Animations?.OnCast?.SpellEffect != null)
                {
                    GameLog.UserActivityInfo($"UseCastable: Sending spelleffect for {Name}: {castObject.Effects.Animations.OnCast.SpellEffect.Id}");
                    Effect(castObject.Effects.Animations.OnCast.SpellEffect.Id, castObject.Effects.Animations.OnCast.SpellEffect.Speed);
                }
            }

            if (castObject.Effects?.Sound != null)
                PlaySound(castObject.Effects.Sound.Id);

            GameLog.UserActivityInfo($"UseCastable: {Name} casting {castObject.Name}, {targets.Count()} targets");

            if (!string.IsNullOrEmpty(castObject.Script))
            {
                // If a script is defined we fire it immediately, and let it handle targeting / etc
                if (Game.World.ScriptProcessor.TryGetScript(castObject.Script, out Script script))
                    return script.ExecuteFunction("OnUse", this);
                else
                {
                    GameLog.UserActivityError($"UseCastable: {Name} casting {castObject.Name}: castable script {castObject.Script} missing");
                    return false;
                }

            }
            if (targets.Count == 0)
                GameLog.UserActivityError("{Name}: {castObject.Name}: hey fam no targets");

            foreach (var tar in targets)
            {
                if (castObject.Effects?.ScriptOverride == true)
                {
                    // TODO: handle castables with scripting
                    // DoStuff();
                    continue;
                }
                if (!castObject.Effects.Damage.IsEmpty)
                {
                    Xml.ElementType attackElement;
                    var damageOutput = NumberCruncher.CalculateDamage(castObject, tar, this);
                    if (castObject.Element == Xml.ElementType.Random)
                    {
                        Random rnd = new Random();
                        var Elements = Enum.GetValues(typeof(Xml.ElementType));
                        attackElement = (Xml.ElementType)Elements.GetValue(rnd.Next(Elements.Length));
                    }
                    else if (castObject.Element != Xml.ElementType.None)
                        attackElement = castObject.Element;
                    else
                        attackElement = (Stats.OffensiveElementOverride != Xml.ElementType.None ? Stats.OffensiveElementOverride : Stats.BaseOffensiveElement);
                    GameLog.UserActivityInfo($"UseCastable: {Name} casting {castObject.Name} - target: {tar.Name} damage: {damageOutput}, element {attackElement}");

                    tar.Damage(damageOutput.Amount, attackElement, damageOutput.Type, damageOutput.Flags, this, false);

                    if (this is Monster)
                    {
                        if(tar is User)
                        {
                            (tar as User).SendSystemMessage($"{this.Name} attacks you with {castObject.Name}.");
                        }
                    }

                    if (this is User)
                    {
                        if(Equipment.Weapon != null && !Equipment.Weapon.Undamageable) Equipment.Weapon.Durability -= 1 / (Equipment.Weapon.MaximumDurability * ((100 - Stats.Ac) == 0 ? 1 : (100 - Stats.Ac)));
                    }

                    if (tar.Stats.Hp <= 0) { deadMobs.Add(tar); }
                }
                // Note that we ignore castables with both damage and healing effects present - one or the other.
                // A future improvement might be to allow more complex effects.
                else if (!castObject.Effects.Heal.IsEmpty)
                {
                    var healOutput = NumberCruncher.CalculateHeal(castObject, tar, this);
                    tar.Heal(healOutput, this);
                    if (this is User)
                    {
                        GameLog.UserActivityInfo($"UseCastable: {Name} casting {castObject.Name} - target: {tar.Name} healing: {healOutput}");
                        if (Equipment.Weapon != null && !Equipment.Weapon.Undamageable)
                           Equipment.Weapon.Durability -= 1 / (Equipment.Weapon.MaximumDurability * ((100 - Stats.Ac) == 0 ? 1 : (100 - Stats.Ac)));
                    }
                }

                // Handle statuses

                foreach (var status in castObject.Effects.Statuses.Add.Where(e => e.Value != null))
                {
                    Xml.Status applyStatus;
                    if (World.WorldData.TryGetValue<Xml.Status>(status.Value.ToLower(), out applyStatus))
                    {
                        var duration = status.Duration == 0 ? applyStatus.Duration : status.Duration;
                        GameLog.UserActivityInfo($"UseCastable: {Name} casting {castObject.Name} - applying status {status.Value} - duration {duration}");
                        if(tar.CurrentStatusInfo.Any(x => x.Category == applyStatus.Category))
                        {
                            if(this is User user)
                            {
                                user.SendSystemMessage($"Another {applyStatus.Category} already affects your target.");
                            }
                        }
                        else
                            tar.ApplyStatus(new CreatureStatus(applyStatus, tar, castObject, this, duration, -1, status.Intensity));
                    }
                    else
                        GameLog.UserActivityError($"UseCastable: {Name} casting {castObject.Name} - failed to add status {status.Value}, does not exist!");
                }

                foreach (var status in castObject.Effects.Statuses.Remove)
                {
                    Xml.Status applyStatus;
                    if (World.WorldData.TryGetValue<Xml.Status>(status.ToLower(), out applyStatus))
                    {
                        GameLog.UserActivityError($"UseCastable: {Name} casting {castObject.Name} - removing status {status}");
                        tar.RemoveStatus(applyStatus.Icon);
                    }
                    else
                        GameLog.UserActivityError($"UseCastable: {Name} casting {castObject.Name} - failed to remove status {status}, does not exist!");

                }
            }

            // Now flood away
            foreach (var dead in deadMobs)
                World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.HandleDeath, dead));
            Condition.Casting = false;
            return true;
        }

        public void SendAnimation(ServerPacket packet)
        {
            GameLog.DebugFormat("SendAnimation");
            GameLog.DebugFormat("SendAnimation byte format is: {0}", BitConverter.ToString(packet.ToArray()));
            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
            {
                var nPacket = (ServerPacket)packet.Clone();
                GameLog.DebugFormat("SendAnimation to {0}", user.Name);
                user.Enqueue(nPacket);

            }
        }

        public void SendCastLine(ServerPacket packet)
        {
            GameLog.DebugFormat("SendCastLine");
            GameLog.DebugFormat($"SendCastLine byte format is: {BitConverter.ToString(packet.ToArray())}");
            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
            {
                var nPacket = (ServerPacket)packet.Clone();
                GameLog.DebugFormat($"SendCastLine to {user.Name}");
                user.Enqueue(nPacket);

            }

        }

        public virtual void UpdateAttributes(StatUpdateFlags flags)
        {
        }

        public virtual bool Walk(Xml.Direction direction)
        {
            lock (_lock)
            {
                int oldX = X, oldY = Y, newX = X, newY = Y;
                Rectangle arrivingViewport = Rectangle.Empty;
                Rectangle departingViewport = Rectangle.Empty;
                Rectangle commonViewport = Rectangle.Empty;
                var halfViewport = Constants.VIEWPORT_SIZE / 2;
                Warp targetWarp;

                switch (direction)
                {
                    // Calculate the differences (which are, in all cases, rectangles of height 12 / width 1 or vice versa)
                    // between the old and new viewpoints. The arrivingViewport represents the objects that need to be notified
                    // of this object's arrival (because it is now within the viewport distance), and departingViewport represents
                    // the reverse. We later use these rectangles to query the quadtree to locate the objects that need to be 
                    // notified of an update to their AOI (area of interest, which is the object's viewport calculated from its
                    // current position).

                    case Xml.Direction.North:
                        --newY;
                        arrivingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, 1);
                        departingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport, Constants.VIEWPORT_SIZE, 1);
                        break;
                    case Xml.Direction.South:
                        ++newY;
                        arrivingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport, Constants.VIEWPORT_SIZE, 1);
                        departingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, 1);
                        break;
                    case Xml.Direction.West:
                        --newX;
                        arrivingViewport = new Rectangle(newX - halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                        departingViewport = new Rectangle(oldX + halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                        break;
                    case Xml.Direction.East:
                        ++newX;
                        arrivingViewport = new Rectangle(oldX + halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                        departingViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
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
                if (Map.IsWall[newX, newY])
                {
                    Refresh();
                    return false;
                }
                else
                {
                    // Is the player trying to walk into an occupied tile?
                    foreach (var obj in Map.GetTileContents((byte)newX, (byte)newY))
                    {
                        GameLog.DebugFormat("Collsion check: found obj {0}", obj.Name);
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
                        else if (targetWarp.MaximumLevel < Stats.Level)
                        {

                            Refresh();
                            return false;
                        }
                    }
                }

                // Calculate the common viewport between the old and new position

                commonViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport, Constants.VIEWPORT_SIZE, Constants.VIEWPORT_SIZE);
                commonViewport.Intersect(new Rectangle(newX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, Constants.VIEWPORT_SIZE));
                GameLog.DebugFormat("Moving from {0},{1} to {2},{3}", oldX, oldY, newX, newY);
                GameLog.DebugFormat("Arriving viewport is a rectangle starting at {0}, {1}", arrivingViewport.X, arrivingViewport.Y);
                GameLog.DebugFormat("Departing viewport is a rectangle starting at {0}, {1}", departingViewport.X, departingViewport.Y);
                GameLog.DebugFormat("Common viewport is a rectangle starting at {0}, {1} of size {2}, {3}", commonViewport.X,
                    commonViewport.Y, commonViewport.Width, commonViewport.Height);

                X = (byte)newX;
                Y = (byte)newY;
                Direction = direction;

                // Objects in the common viewport receive a "walk" (0x0C) packet
                // Objects in the arriving viewport receive a "show to" (0x33) packet
                // Objects in the departing viewport receive a "remove object" (0x0E) packet

                foreach (var obj in Map.EntityTree.GetObjects(commonViewport))
                {
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
            HasMoved = true;
            return true;
        }

        public virtual bool Turn(Xml.Direction direction)
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
                    {
                        user.Enqueue(x11);
                    }
                }
            }

            return true;
        }

        public virtual void Motion(byte motion, short speed)
        {
            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    user.SendMotion(Id, motion, speed);
                }
            }
        }

        public virtual void Heal(double heal, Creature source = null)
        {
            OnHeal(source, (uint) heal);

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

        public virtual void Damage(double damage, Xml.ElementType element = Xml.ElementType.None, Xml.DamageType damageType = Xml.DamageType.Direct, Xml.DamageFlags damageFlags = Xml.DamageFlags.None, Creature attacker = null, bool onDeath=true)
        {
            if (this is Monster ms && !Condition.Alive) return;
            if (attacker is User && this is Monster)
            {
                if (FirstHitter == null || !World.UserConnected(FirstHitter.Name) || ((DateTime.Now - LastHitTime).TotalSeconds > Constants.MONSTER_TAGGING_TIMEOUT)) FirstHitter = attacker;
                if (attacker != FirstHitter && !((FirstHitter as User).Group?.Members.Contains(attacker) ?? false)) return;
            }

            LastHitTime = DateTime.Now;

            if (damageType != Xml.DamageType.Direct)
            {
                double armor = Stats.Ac * -1 + 100;
                var elementTable = Game.World.WorldData.Get<Xml.ElementTable>("ElementTable");
                var multiplier = elementTable.Source.First(x => x.Element == element).Target.FirstOrDefault(x => x.Element == Stats.BaseDefensiveElement).Multiplier;
                var reduction = damage * (armor / (armor + 50));
                damage = (damage - reduction) * multiplier;
            }

            if (attacker != null)
                _mLastHitter = attacker.Id;

            var normalized = (uint)damage;

            if (normalized > Stats.Hp && damageFlags.HasFlag(Xml.DamageFlags.Nonlethal))
                normalized = Stats.Hp - 1;
            else if (normalized > Stats.Hp)
                normalized = Stats.Hp;

            OnDamage(attacker, normalized);

            if (AbsoluteImmortal) return;

            if (damageType == Xml.DamageType.Physical && (AbsoluteImmortal || PhysicalImmortal))
                return;

            if (damageType == Xml.DamageType.Magical && (AbsoluteImmortal || MagicalImmortal))
                return;

            Stats.Hp = (Stats.Hp - normalized) < 0 ? 0 : Stats.Hp - normalized;
            //Stats.Hp -= normalized;

            SendDamageUpdate(this);

            // TODO: Separate this out into a control message
            if (Stats.Hp == 0 && onDeath)
            {
                if (this is Monster) Condition.Alive = false;
                OnDeath();
            }
        }

        private void SendDamageUpdate(Creature creature)
        {
            if (Map == null) return;
            var percent = ((creature.Stats.Hp / (double)creature.Stats.MaximumHp) * 100);
            var healthbar = new ServerPacketStructures.HealthBar() { CurrentPercent = (byte)percent, ObjId = creature.Id };

            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
            {
                var nPacket = (ServerPacket)healthbar.Packet().Clone();
                user.Enqueue(nPacket);
            }
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (!(obj is User)) return;
            var user = (User)obj;
            user.SendVisibleCreature(this);
        }

        public virtual void Refresh()
        {
        }
    }

}
