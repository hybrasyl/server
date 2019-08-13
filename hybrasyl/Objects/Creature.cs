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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015-2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */
 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Hybrasyl.Castables;
using Hybrasyl.Enums;
using Hybrasyl.Statuses;
using log4net;
using Newtonsoft.Json;

namespace Hybrasyl.Objects
{

    public class Creature : VisibleObject
    {
        public new static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly ILog ActivityLogger = LogManager.GetLogger(Assembly.GetEntryAssembly(),"UserActivityLog");

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
        }

        public Creature GetDirectionalTarget(Direction direction)
        {
            VisibleObject obj;

            switch (direction)
            {
                case Direction.East:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X + 1 && x.Y == Y && x is Creature);
                    }
                    break;
                case Direction.West:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X - 1 && x.Y == Y && x is Creature);
                    }
                    break;
                case Direction.North:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X && x.Y == Y - 1 && x is Creature);
                    }
                    break;
                case Direction.South:
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
    
        public virtual List<Creature> GetTargets(Castable castable, Creature target = null)
        {
            List<Creature> actualTargets = new List<Creature>();

            /* INTENT HANDLING FOR TARGETING
             * 
             * This is particularly confusing so it is documented here.
             * UseType=Target Radius=0 Direction=None -> exact clicked target 
             * UseType=Target Radius=0 Direction=!None -> invalid
             * UseType=Target Radius=>0 Direction=None -> rect centered on target 
             * UseType=Target Radius>0 Direction=(anything but none) -> directional rect target based on click x/y
             * UseType=NoTarget Radius=0 Direction=None -> self (wings of protection, maybe custom spells / mentoring / lore / etc)?
             * UseType=NoTarget Radius>0 Direction=None -> rect from self in all directions
             * UseType=NoTarget Radius>0 Direction=!None -> rect from self in specific direction
             */

            var intents = castable.Intents;

            foreach (var intent in intents)
            {
                var possibleTargets = new List<VisibleObject>();
                if (intent.UseType == Castables.SpellUseType.NoTarget && intent.Target.Contains(IntentTarget.Group))
                {
                    // Targeting group members
                    var user = this as User;
                    if (user != null && user.Group != null)
                        possibleTargets.AddRange(user.Group.Members.Where(m => m.Map.Id == Map.Id && m.Distance(this) < intent.Radius));
                }
                else if (intent.UseType == Castables.SpellUseType.Target && intent.Radius == 0 && intent.Direction == IntentDirection.None)
                {
                    // Targeting the exact clicked target
                    if (target == null)
                        Logger.Error($"GetTargets: {castable.Name} - intent was for exact clicked target but no target was passed?");
                    else
                        // Heal spels can be cast on players, other spells can be cast on attackable creatures
                        if ((!castable.Effects.Damage.IsEmpty && target.Condition.IsAttackable) ||
                        (castable.Effects.Damage.IsEmpty && target is User))
                        possibleTargets.Add(target);
                }
                else if (intent.UseType == Castables.SpellUseType.NoTarget && intent.Radius == 0 && intent.Direction == IntentDirection.None)
                {
                    // Targeting self - which, currently, is only allowed for non-damaging spells
                    if (castable.Effects.Damage.IsEmpty)
                        possibleTargets.Add(this);
                }
                else
                {
                    // Area targeting, directional or otherwise

                    Rectangle rect = new Rectangle(0, 0, 0, 0);
                    byte X = this.X;
                    byte Y = this.Y;

                    // Handle area targeting with click target as the source
                    if (intent.UseType == Castables.SpellUseType.Target)
                    {
                        X = target.X;
                        Y = target.Y;
                    }

                    switch (intent.Direction)
                    {
                        case IntentDirection.Front:
                            {
                                switch (Direction)
                                {
                                    case Direction.North:
                                        {
                                            //facing north, attack north
                                            rect = new Rectangle(X, Y - intent.Radius, 1,  intent.Radius);
                                        }
                                        break;
                                    case Direction.South:
                                        {
                                            //facing south, attack south
                                            rect = new Rectangle(X, Y, 1, 1 + intent.Radius);
                                        }
                                        break;
                                    case Direction.East:
                                        {
                                            //facing east, attack east
                                            rect = new Rectangle(X, Y, 1 + intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.West:
                                        {
                                            //facing west, attack west
                                            rect = new Rectangle(X - intent.Radius, Y, intent.Radius, 1);
                                        }
                                        break;
                                }
                            }
                            break;
                        case IntentDirection.Back:
                            {
                                switch (Direction)
                                {
                                    case Direction.North:
                                        {
                                            //facing north, attack south
                                            rect = new Rectangle(X, Y, 1, 1 + intent.Radius);
                                        }
                                        break;
                                    case Direction.South:
                                        {
                                            //facing south, attack north
                                            rect = new Rectangle(X, Y - intent.Radius, 1, intent.Radius);
                                        }
                                        break;
                                    case Direction.East:
                                        {
                                            //facing east, attack west
                                            rect = new Rectangle(X - intent.Radius, Y, intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.West:
                                        {
                                            //facing west, attack east
                                            rect = new Rectangle(X, Y, 1 + intent.Radius, 1);
                                        }
                                        break;
                                }
                            }
                            break;
                        case IntentDirection.Left:
                            {
                                switch (Direction)
                                {
                                    case Direction.North:
                                        {
                                            //facing north, attack west
                                            rect = new Rectangle(X - intent.Radius, Y, intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.South:
                                        {
                                            //facing south, attack east
                                            rect = new Rectangle(X, Y, 1 + intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.East:
                                        {
                                            //facing east, attack north
                                            rect = new Rectangle(X, Y, 1, 1 + intent.Radius);
                                        }
                                        break;
                                    case Direction.West:
                                        {
                                            //facing west, attack south
                                            rect = new Rectangle(X, Y - intent.Radius, 1, intent.Radius);
                                        }
                                        break;
                                }
                            }
                            break;
                        case IntentDirection.Right:
                            {
                                switch (Direction)
                                {
                                    case Direction.North:
                                        {
                                            //facing north, attack east
                                            rect = new Rectangle(X, Y, 1 + intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.South:
                                        {
                                            //facing south, attack west
                                            rect = new Rectangle(X - intent.Radius, Y, intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.East:
                                        {
                                            //facing east, attack south
                                            rect = new Rectangle(X, Y - intent.Radius, 1, intent.Radius);
                                        }
                                        break;
                                    case Direction.West:
                                        {
                                            //facing west, attack north
                                            rect = new Rectangle(X, Y, 1, 1 + intent.Radius);
                                        }
                                        break;
                                }
                            }
                            break;
                        case IntentDirection.Nearby:
                        case IntentDirection.None:
                            {
                                //attack radius
                                rect = new Rectangle(X - intent.Radius, Y - intent.Radius, Math.Max(intent.Radius, (byte)1)*2, Math.Max(intent.Radius, (byte)1)*2);
                            }
                            break;
                    }
                    Logger.Info($"Rectangle: x: {X - intent.Radius} y: {Y - intent.Radius}, radius: {intent.Radius} - LOCATION: {rect.Location} TOP: {rect.Top}, BOTTOM: {rect.Bottom}, RIGHT: {rect.Right}, LEFT: {rect.Left}");
                    if (rect.IsEmpty) continue;

                    possibleTargets.AddRange(Map.EntityTree.GetObjects(rect).Where(obj => obj is Creature && obj != this));
                }

                // Remove merchants
                possibleTargets = possibleTargets.Where(e => !(e is Merchant)).ToList();

                // Handle intent flags
                if (this is Monster)
                {
                    // No hostile flag: remove users
                    // No friendly flag: remove monsters
                    // Group / pvp: do not apply here
                    if (!intent.Target.Contains(IntentTarget.Friendly))
                        possibleTargets = possibleTargets.Where(e => !(e is Monster)).ToList();
                    if (!intent.Target.Contains(IntentTarget.Hostile))
                        possibleTargets = possibleTargets.Where(e => !(e is User)).ToList();
                }
                else if (this is User)
                {
                    var user = this as User;
                    // No hostile flag: remove monsters
                    // No friendly flag: remove users with pvp disabled
                    // No pvp: remove 
                    // If we aren't targeting friendlies or pvp, remove all users entirely
                    if (!intent.Target.Contains(IntentTarget.Pvp))
                        possibleTargets = possibleTargets.Where(e => !(e is User && (e as Creature).Condition.PvpEnabled == true)).ToList();
                    if (!intent.Target.Contains(IntentTarget.Friendly))
                        possibleTargets = possibleTargets.Where(e => !(e is User && (e as Creature).Condition.PvpEnabled == false)).ToList();
                    // If we aren't targeting hostiles, remove all monsters
                    if (!intent.Target.Contains(IntentTarget.Hostile))
                        possibleTargets = possibleTargets.Where(e => !(e is Monster)).ToList();
                }

                // Finally, add the targets to our list

                List<Creature> possible = intent.MaxTargets > 0 ? possibleTargets.Take(intent.MaxTargets).OfType<Creature>().ToList() : possibleTargets.OfType<Creature>().ToList();
                if (possible != null && possible.Count > 0) actualTargets.AddRange(possible);
                else Logger.Info("No targets found");
            }
            return actualTargets;
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
        public Creature FirstHitter { get; private set; }

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
                status.OnEnd();
            if (this is User) (this as User).SendStatusUpdate(status, true);
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
                Logger.Debug($"Current status count is {_currentStatuses.Count}");
            }
        }

        /// <summary>
        /// Process all the given status ticks for a creature's active statuses.
        /// </summary>
        public void ProcessStatusTicks()
        {
            foreach (var kvp in _currentStatuses)
            {
                Logger.DebugFormat("OnTick: {0}, {1}", Name, kvp.Value.Name);

                if (kvp.Value.Expired)
                {
                    var removed = RemoveStatus(kvp.Key);
                    Logger.DebugFormat($"Status {kvp.Value.Name} has expired: removal was {removed}");
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

        public virtual bool UseCastable(Castable castObject, Creature target = null)
        {
            if (!Condition.CastingAllowed) return false;
            
            if (this is User) ActivityLogger.Info($"UseCastable: {Name} begin casting {castObject.Name} on target: {target?.Name ?? "no target"} CastingAllowed: {Condition.CastingAllowed}");

            var damage = castObject.Effects.Damage;
            List<Creature> targets;

            targets = GetTargets(castObject, target);

            if (targets.Count() == 0 && castObject.IsAssail == false) return false;

            // We do these next steps to ensure effects are displayed uniformly and as fast as possible
            var deadMobs = new List<Creature>();
            foreach (var tar in targets)
            {
                foreach (var user in tar.viewportUsers)
                {
                    user.SendEffect(tar.Id, castObject.Effects.Animations.OnCast.Target.Id, castObject.Effects.Animations.OnCast.Target.Speed);
                }
            }

            if (castObject.Effects?.Animations?.OnCast?.SpellEffect != null)
                Effect(castObject.Effects.Animations.OnCast.SpellEffect.Id, castObject.Effects.Animations.OnCast.SpellEffect.Speed);

            if (castObject.Effects.Sound != null)
                PlaySound(castObject.Effects.Sound.Id);

            ActivityLogger.Info($"UseCastable: {Name} casting {castObject.Name}, {targets.Count()} targets");
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
                    Enums.Element attackElement;
                    var damageOutput = NumberCruncher.CalculateDamage(castObject, tar, this);
                    if (castObject.Element == Castables.Element.Random)
                    {
                        Random rnd = new Random();
                        var Elements = Enum.GetValues(typeof(Enums.Element));
                        attackElement = (Enums.Element)Elements.GetValue(rnd.Next(Elements.Length));
                    }
                    else if (castObject.Element != Castables.Element.None)
                        attackElement = (Enums.Element)castObject.Element;
                    else
                        attackElement = (Stats.OffensiveElementOverride == Enums.Element.None ? Stats.OffensiveElementOverride : Stats.OffensiveElement);
                    if (this is User) ActivityLogger.Info($"UseCastable: {Name} casting {castObject.Name} - target: {tar.Name} damage: {damageOutput}, element {attackElement}");

                    tar.Damage(damageOutput.Amount, attackElement, damageOutput.Type, damageOutput.Flags, this, false);
                    if (tar.Stats.Hp <= 0) { deadMobs.Add(tar); }
                }
                // Note that we ignore castables with both damage and healing effects present - one or the other.
                // A future improvement might be to allow more complex effects.
                else if (!castObject.Effects.Heal.IsEmpty)
                {
                    var healOutput = NumberCruncher.CalculateHeal(castObject, tar, this);
                    tar.Heal(healOutput, this);
                    if (this is User) ActivityLogger.Info($"UseCastable: {Name} casting {castObject.Name} - target: {tar.Name} healing: {healOutput}");
                }

                // Handle statuses

                foreach (var status in castObject.Effects.Statuses.Add.Where(e => e.Value != null))
                {
                    Status applyStatus;
                    if (World.WorldData.TryGetValueByIndex<Status>(status.Value, out applyStatus))
                    {
                        ActivityLogger.Info($"UseCastable: {Name} casting {castObject.Name} - applying status {status.Value}");
                        ApplyStatus(new CreatureStatus(applyStatus, tar, castObject));
                    }
                    else
                        ActivityLogger.Error($"UseCastable: {Name} casting {castObject.Name} - failed to add status {status.Value}, does not exist!");
                }

                foreach (var status in castObject.Effects.Statuses.Remove)
                {
                    Status applyStatus;
                    if (World.WorldData.TryGetValueByIndex<Status>(status, out applyStatus))
                    {
                        ActivityLogger.Error($"UseCastable: {Name} casting {castObject.Name} - removing status {status}");
                        RemoveStatus(applyStatus.Icon);
                    }
                    else
                        ActivityLogger.Error($"UseCastable: {Name} casting {castObject.Name} - failed to remove status {status}, does not exist!");

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
            Logger.DebugFormat("SendAnimation");
            Logger.DebugFormat("SendAnimation byte format is: {0}", BitConverter.ToString(packet.ToArray()));
            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
            {
                var nPacket = (ServerPacket)packet.Clone();
                Logger.DebugFormat("SendAnimation to {0}", user.Name);
                user.Enqueue(nPacket);

            }
        }

        public void SendCastLine(ServerPacket packet)
        {
            Logger.DebugFormat("SendCastLine");
            Logger.DebugFormat($"SendCastLine byte format is: {BitConverter.ToString(packet.ToArray())}");
            foreach (var user in Map.EntityTree.GetObjects(GetViewport()).OfType<User>())
            {
                var nPacket = (ServerPacket)packet.Clone();
                Logger.DebugFormat($"SendCastLine to {user.Name}");
                user.Enqueue(nPacket);

            }

        }

        public virtual void UpdateAttributes(StatUpdateFlags flags)
        {
        }

        public virtual bool Walk(Direction direction)
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

                case Direction.North:
                    --newY;
                    arrivingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, 1);
                    departingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport, Constants.VIEWPORT_SIZE, 1);
                    break;
                case Direction.South:
                    ++newY;
                    arrivingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport, Constants.VIEWPORT_SIZE, 1);
                    departingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, 1);
                    break;
                case Direction.West:
                    --newX;
                    arrivingViewport = new Rectangle(newX - halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    departingViewport = new Rectangle(oldX + halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    break;
                case Direction.East:
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
                    Logger.DebugFormat("Collsion check: found obj {0}", obj.Name);
                    if (obj is Creature)
                    {
                        Logger.DebugFormat("Walking prohibited: found {0}", obj.Name);
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
            Logger.DebugFormat("Moving from {0},{1} to {2},{3}", oldX, oldY, newX, newY);
            Logger.DebugFormat("Arriving viewport is a rectangle starting at {0}, {1}", arrivingViewport.X, arrivingViewport.Y);
            Logger.DebugFormat("Departing viewport is a rectangle starting at {0}, {1}", departingViewport.X, departingViewport.Y);
            Logger.DebugFormat("Common viewport is a rectangle starting at {0}, {1} of size {2}, {3}", commonViewport.X,
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
                    Logger.DebugFormat("Sending walk packet for {0} to {1}", Name, user.Name);
                    var x0C = new ServerPacket(0x0C);
                    x0C.WriteUInt32(Id);
                    x0C.WriteUInt16((byte)oldX);
                    x0C.WriteUInt16((byte)oldY);
                    x0C.WriteByte((byte)direction);
                    x0C.WriteByte(0x00);
                    user.Enqueue(x0C);
                }
            }

            foreach (var obj in Map.EntityTree.GetObjects(arrivingViewport))
            {
                obj.AoiEntry(this);
                AoiEntry(obj);
            }

            foreach (var obj in Map.EntityTree.GetObjects(departingViewport))
            {
                obj.AoiDeparture(this);
                AoiDeparture(obj);
            }

            HasMoved = true;
            Map.EntityTree.Move(this);
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

        public virtual void Damage(double damage, Enums.Element element = Enums.Element.None, Enums.DamageType damageType = Enums.DamageType.Direct, Castables.DamageFlags damageFlags = Castables.DamageFlags.None, Creature attacker = null, bool onDeath=true)
        {
            if (attacker is User && this is Monster)
            {
                if (FirstHitter == null || !Game.World.ActiveUsersByName.ContainsKey(FirstHitter.Name) || ((DateTime.Now - LastHitTime).TotalSeconds > Constants.MONSTER_TAGGING_TIMEOUT)) FirstHitter = attacker;
                if (attacker != FirstHitter && !((FirstHitter as User).Group?.Members.Contains(attacker) ?? false)) return;
            }

            LastHitTime = DateTime.Now;

            if (damageType == Enums.DamageType.Physical && (AbsoluteImmortal || PhysicalImmortal))
                return;

            if (damageType == Enums.DamageType.Magical && (AbsoluteImmortal || MagicalImmortal))
                return;

            if (damageType != Enums.DamageType.Direct)
            {
                double armor = Stats.Ac * -1 + 100;
                var resist = Game.ElementTable[(int)element, 0];
                var reduction = damage * (armor / (armor + 50));
                damage = (damage - reduction) * resist;
            }

            if (attacker != null)
                _mLastHitter = attacker.Id;

            var normalized = (uint)damage;

            if (normalized > Stats.Hp && damageFlags.HasFlag(Castables.DamageFlags.Nonlethal))
                normalized = Stats.Hp - 1;
            else if (normalized > Stats.Hp)
                normalized = Stats.Hp;

            Stats.Hp -= normalized;

            SendDamageUpdate(this);
            
            OnReceiveDamage();
            
            // TODO: Separate this out into a control message
            if (Stats.Hp == 0 && onDeath)
                OnDeath();
        }

        private void SendDamageUpdate(Creature creature)
        {
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
