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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        [JsonProperty]
        public byte Level { get; set; }

        [JsonProperty]
        public uint Experience { get; set; }

        [JsonProperty]
        public byte Ability { get; set; }

        [JsonProperty]
        public uint AbilityExp { get; set; }

        [JsonProperty]
        public uint Hp { get; set; }

        [JsonProperty]
        public uint Mp { get; set; }

        [JsonProperty]
        public long BaseHp { get; set; }

        [JsonProperty]
        public long BaseMp { get; set; }

        [JsonProperty]
        public long BaseStr { get; set; }

        [JsonProperty]
        public long BaseInt { get; set; }

        [JsonProperty]
        public long BaseWis { get; set; }

        [JsonProperty]
        public long BaseCon { get; set; }

        [JsonProperty]
        public ConditionInfo Condition { get; set; }

        [JsonProperty]
        public long BaseDex { get; set; }

        public long BonusHp { get; set; }
        public long BonusMp { get; set; }
        public long BonusStr { get; set; }
        public long BonusInt { get; set; }
        public long BonusWis { get; set; }
        public long BonusCon { get; set; }
        public long BonusDex { get; set; }
        public long BonusDmg { get; set; }
        public long BonusHit { get; set; }
        public long BonusAc { get; set; }
        public long BonusMr { get; set; }
        public long BonusRegen { get; set; }

        protected Enums.Element BaseOffensiveElement { get; set; }
        protected Enums.Element BaseDefensiveElement { get; set; }

        public Enums.Element OffensiveElement
        {
            get
            {
                return (OffensiveElementOverride == Enums.Element.None ? OffensiveElementOverride : BaseOffensiveElement);
            }
        }
        public Enums.Element DefensiveElement
        {
            get
            {
                return (DefensiveElementOverride == Enums.Element.None ? DefensiveElementOverride : BaseDefensiveElement);
            }
        }

        public Enums.Element OffensiveElementOverride { get; set; }
        public Enums.Element DefensiveElementOverride { get; set; }

        public Enums.DamageType? DamageTypeOverride { get; set; }

        public double ReflectChance
        {

            get
            {
                var value = BaseReflectChance + BonusReflectChance;

                if (value > 1.0)
                    return 1.0;

                if (value < 0)
                    return 0;

                return value;
            }
        }

        [JsonProperty]
        public double BaseReflectChance { get; set; }
        [JsonProperty]
        public double BonusReflectChance { get; set; }

        public double ReflectIntensity
        {

            get
            {
                var value = BaseReflectChance + BonusReflectChance;

                if (value < 0)
                    return 0;

                return value;
            }
        }

        [JsonProperty]
        public double BaseReflectIntensity { get; set; }
        [JsonProperty]
        public double BonusReflectIntensity { get; set; }

        public bool IsReflected
        {
            get
            {
                Random rnd1 = new Random();
                return (rnd1.NextDouble() >= ReflectChance);
            }
        }

        public double HealModifier
        {
            get
            {
                var value = BaseHealModifier + BonusHealModifier;

                if (value < 0)
                    return 0;

                return value;
            }
        }

        [JsonProperty]
        public double BaseHealModifier { get; set; }
        [JsonProperty]
        public double BonusHealModifier { get; set; }

        public double DamageModifier
        {
            get
            {
                var value = BaseDamageModifier + BonusDamageModifier;

                if (value < 0)
                    return 0;

                return value;
            }
        }

        [JsonProperty]
        public double BaseDamageModifier { get; set; }
        [JsonProperty]
        public double BonusDamageModifier { get; set; }
   
        public ushort MapId { get; protected set; }
        public byte MapX { get; protected set; }
        public byte MapY { get; protected set; }

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
            BaseDamageModifier = 1;
            BonusDamageModifier = 0;
            BaseHealModifier = 1;
            BonusHealModifier = 0;
            BaseReflectIntensity = 1;
            BaseReflectChance = 0;
            DamageTypeOverride = null;
            Condition = new ConditionInfo(this);            
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
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X + 1 && x.Y == Y);
                    }
                    break;
                case Direction.West:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X - 1 && x.Y == Y);
                    }
                    break;
                case Direction.North:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X && x.Y == Y - 1);
                    }
                    break;
                case Direction.South:
                    {
                        obj = Map.EntityTree.FirstOrDefault(x => x.X == X && x.Y == Y + 1);
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
                        if ((castable.Effects?.Damage != null && target.Condition.IsAttackable) ||
                        (castable.Effects?.Damage == null && target is User))
                            possibleTargets.Add(target);
                }
                else if (intent.UseType == Castables.SpellUseType.NoTarget && intent.Radius == 0 && intent.Direction == IntentDirection.None)
                {
                    // Targeting self - which, currently, is only allowed for non-damaging spells
                    if (castable.Effects?.Damage == null)
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
                                            rect = new Rectangle(X, Y - intent.Radius, 1, intent.Radius);
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
                                rect = new Rectangle(X - intent.Radius, Y - intent.Radius, Math.Max(intent.Radius, (byte)1), Math.Max(intent.Radius, (byte)1));
                            }
                            break;
                    }

                    if (!rect.IsEmpty)
                    {
                        var intersect = Map.EntityTree.GetObjects(Rect).Where(obj => obj is Creature && obj != this);
                        // Disqualify targets based on intent
                        // If we aren't targeting friendlies or pvp, remove all users entirely
                        if (!intent.Target.Contains(IntentTarget.Friendly) && !intent.Target.Contains(IntentTarget.Pvp))
                            intersect = intersect.Where(e => !(e is User));

                        // If we aren't targeting hostiles, remove all monsters
                        if (!intent.Target.Contains(IntentTarget.Hostile))
                            intersect.Where(e => !(e is Monster || (e as Creature).Condition.PvpEnabled));
                    }

                }

                List<Creature> possible = intent.MaxTargets > 0 ? possibleTargets.Take(intent.MaxTargets).OfType<Creature>().ToList() : possibleTargets.OfType<Creature>().ToList();

                if (possible != null && possible.Count > 0) actualTargets.AddRange(possible);

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

        public uint MaximumHp
        {
            get
            {
                var value = BaseHp + BonusHp;

                if (value > uint.MaxValue)
                    return uint.MaxValue;

                if (value < uint.MinValue)
                    return 1;

                return (uint)BindToRange(value, StatLimitConstants.MIN_BASE_HPMP, StatLimitConstants.MAX_BASE_HPMP);
            }
        }

        public uint MaximumMp
        {
            get
            {
                var value = BaseMp + BonusMp;

                if (value > uint.MaxValue)
                    return uint.MaxValue;

                if (value < uint.MinValue)
                    return 1;

                return (uint)BindToRange(value, StatLimitConstants.MIN_BASE_HPMP, StatLimitConstants.MAX_BASE_HPMP);
            }
        }

        public byte Str
        {
            get
            {
                var value = BaseStr + BonusStr;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Int
        {
            get
            {
                var value = BaseInt + BonusInt;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Wis
        {
            get
            {
                var value = BaseWis + BonusWis;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Con
        {
            get
            {
                var value = BaseCon + BonusCon;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Dex
        {
            get
            {
                var value = BaseDex + BonusDex;

                if (value > byte.MaxValue)
                    return byte.MaxValue;

                if (value < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(value, StatLimitConstants.MIN_STAT, StatLimitConstants.MAX_STAT);
            }
        }

        public byte Dmg
        {
            get
            {
                if (BonusDmg > byte.MaxValue)
                    return byte.MaxValue;

                if (BonusDmg < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(BonusDmg, StatLimitConstants.MIN_DMG, StatLimitConstants.MAX_DMG);
            }
        }

        public byte Hit
        {
            get
            {
                if (BonusHit > byte.MaxValue)
                    return byte.MaxValue;

                if (BonusHit < byte.MinValue)
                    return byte.MinValue;

                return (byte)BindToRange(BonusHit, StatLimitConstants.MIN_HIT, StatLimitConstants.MAX_HIT);
            }
        }

        public sbyte Ac
        {
            get
            {
                Logger.DebugFormat("BonusAc is {0}", BonusAc);
                var value = 100 - Level / 3 + BonusAc;

                if (value > sbyte.MaxValue)
                    return sbyte.MaxValue;

                if (value < sbyte.MinValue)
                    return sbyte.MinValue;

                return (sbyte)BindToRange(value, StatLimitConstants.MIN_AC, StatLimitConstants.MAX_AC);
            }
        }

        public sbyte Mr
        {
            get
            {
                if (BonusMr > sbyte.MaxValue)
                    return sbyte.MaxValue;

                if (BonusMr < sbyte.MinValue)
                    return sbyte.MinValue;

                return (sbyte)BindToRange(BonusMr, StatLimitConstants.MIN_MR, StatLimitConstants.MAX_MR);
            }
        }

        public sbyte Regen
        {
            get
            {
                if (BonusRegen > sbyte.MaxValue)
                    return sbyte.MaxValue;

                if (BonusRegen < sbyte.MinValue)
                    return sbyte.MinValue;

                return (sbyte)BonusRegen;
            }
        }

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


        public virtual bool UseCastable(Castable castObject, Creature target = null)
        {
            // TODO: DRY if possible?
            if (!Condition.CastingAllowed) return false;
            var damage = castObject.Effects.Damage;
            List<Creature> targets;
            List<Creature> actualtargets;

            if (target != null)
                targets = new List<Creature> { target };
            else
                targets = GetTargets(castObject);

            if (castObject.Effects?.Damage != null)
                actualtargets = targets.Where(e => e is Monster || e.Condition.PvpEnabled).ToList();
            else if (castObject.Effects?.Heal != null)
                actualtargets = targets.Where(e => (e is Monster) == false).ToList();
            else
                actualtargets = targets; // yech

            if (actualtargets.Count() == 0) return false;

            foreach (var tar in actualtargets)
            {
                if (tar is Monster || (tar is User && ((User)tar).Condition.PvpEnabled))
                {
                    if (castObject.Effects?.Damage != null)
                    {
                        var damageOutput = NumberCruncher.CalculateDamage(castObject, tar, this);

                        tar.Damage(damageOutput.Amount, OffensiveElement, damageOutput.Type, this);
                    }
                    else if (castObject.Effects?.Heal != null)
                    {
                        var healOutput = NumberCruncher.CalculateHeal(castObject, tar, this);
                        tar.Heal(healOutput, this);
                    }
                    else if (castObject.Statuses?.Add != null)
                    {

                    }
                    else if (castObject.Statuses?.Remove != null)
                    {

                    }
                    if (castObject.Effects.Animations.OnCast.Target == null) continue;

                    var effectAnimation = new ServerPacketStructures.EffectAnimation()
                    {
                        SourceId = this.Id,
                        Speed = (short)castObject.Effects.Animations.OnCast.Target.Speed,
                        TargetId = tar.Id,
                        TargetAnimation = castObject.Effects.Animations.OnCast.Target.Id
                    };
                    SendAnimation(effectAnimation.Packet());
                }

                Motion motion;

                if (castObject.Effects.Sound != null)
                    PlaySound(castObject.Effects.Sound.Id);
            }
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
                    if (targetWarp.MinimumLevel > Level)
                    {

                        Refresh();
                        return false;
                    }
                    else if (targetWarp.MaximumLevel < Level)
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
            if (Hp == MaximumHp) return;

            Hp = heal > uint.MaxValue ? MaximumHp : Math.Min(MaximumHp, (uint)(Hp + heal));
            SendDamageUpdate(this);
            if (this is User) { UpdateAttributes(StatUpdateFlags.Current); }
        }

        public virtual void RegenerateMp(double mp, Creature regenerator = null)
        {
            if (AbsoluteImmortal)
                return;

            if (Mp == MaximumMp || mp > MaximumMp)
                return;

            Mp = mp > uint.MaxValue ? MaximumMp : Math.Min(MaximumMp, (uint)(Mp + mp));
        }

        public virtual void Damage(double damage, Enums.Element element = Enums.Element.None, Enums.DamageType damageType = Enums.DamageType.Direct, Creature attacker = null)
        {
            if (damageType == Enums.DamageType.Physical && (AbsoluteImmortal || PhysicalImmortal))
                return;

            if (damageType == Enums.DamageType.Magical && (AbsoluteImmortal || MagicalImmortal))
                return;

            if (damageType != Enums.DamageType.Direct)
            {
                double armor = Ac * -1 + 100;
                var resist = Game.ElementTable[(int)element, 0];
                var reduction = damage * (armor / (armor + 50));
                damage = (damage - reduction) * resist;
            }

            if (attacker != null)
                _mLastHitter = attacker.Id;

            var normalized = (uint)damage;

            if (normalized > Hp)
                normalized = Hp;

            Hp -= normalized;

            SendDamageUpdate(this);

            OnReceiveDamage();
            
            if (Hp == 0) OnDeath();
        }

        private void SendDamageUpdate(Creature creature)
        {
            var percent = ((creature.Hp / (double)creature.MaximumHp) * 100);
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
