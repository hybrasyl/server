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
 using Hybrasyl.Creatures;
using Hybrasyl.Enums;
using Castable = Hybrasyl.Castables.Castable;
 using Class = Hybrasyl.Castables.Class;

namespace Hybrasyl.Objects
{
    public class Monster : Creature, ICloneable
    {
        private bool _idle = true;

        private uint _mTarget;

        private Spawn _spawn;

        private Creatures.Damage _simpleDamage;

        public bool IsHostile { get; set; }
        public bool ShouldWander { get; set; }

        public Monster()
        {

        }

        public override void OnDeath()
        {
            Shout("AAAAAAAAAAaaaaa!!!");
            // Now that we're dead, award loot.
            // FIXME: Implement loot tables / full looting.
            var hitter = LastHitter as User;
            if (hitter == null) return; // Don't handle cases of MOB ON MOB COMBAT just yet
            hitter.ShareExperience(_spawn.Loot.Xp);

            if (_spawn.Loot.Gold <= 0) return;
            var golds = new Gold(_spawn.Loot.Gold);
            World.Insert(golds);
            Map.Insert(golds, X,Y);
            Map.Remove(this);
            World.Remove(this);
        }

        public Monster(Hybrasyl.Creatures.Creature creature, Spawn spawn, int map)
        {
            Name = creature.Name;
            Sprite = creature.Sprite;
            World = Game.World;
            Map = Game.World.WorldData.Get<Map>(map);
            Level = spawn.Stats.Level;
            BaseHp = spawn.Stats.Hp;
            Hp = spawn.Stats.Hp;
            BaseMp = spawn.Stats.Mp;
            Mp = spawn.Stats.Mp;
            DisplayText = creature.Description;
            BaseStr = spawn.Stats.Str;
            BaseInt = spawn.Stats.Int;
            BaseWis = spawn.Stats.Wis;
            BaseCon = spawn.Stats.Con;
            BaseDex = spawn.Stats.Dex;
            _spawn = spawn;
            _simpleDamage = spawn.Damage;

            var rand = new Random();
            IsHostile = rand.Next(0, 2) == 1;
            ShouldWander = IsHostile == false;
        }

        public Creature Target
        {
            get
            {
                return World.Objects.ContainsKey(_mTarget) ? (Creature)World.Objects[_mTarget] : null;
            }
            set
            {
                _mTarget = value?.Id ?? 0;
            }
        }

        public override int GetHashCode()
        {
            return (Name.GetHashCode() * Id.GetHashCode()) - 1;
        }

        public virtual bool Pathfind(byte x, byte y)
        {
            var xDelta = Math.Abs(x - X);
            var yDelta = Math.Abs(y - Y);

            if (xDelta > yDelta)
            {
                Walk(x > X ? Direction.East : Direction.West);
            }

            return false;
        }

        public void Attack(Direction direction, Creatures.Damage damage, Creature target)
        {
            //do monster attack.
            //monsters are using simple damage for now.
            if (target != null)
            {
                var dmgRand = new Random();
                var dmg = dmgRand.Next(damage.Small.Min, damage.Small.Max);

                var damageType = Enums.DamageType.Physical;
                //these need to be set to integers as attributes. note to fix.
                target.Damage(dmg, OffensiveElement, damageType, this);
            }
            else
            {
                //var formula = damage.Formula;
            }
        }

        public override void Attack(Direction direction, Castable castObject, Creature target)
        {
            if (target != null)
            {
                var damage = castObject.Effects.Damage;

                Random rand = new Random();

                if (damage.Formula == null) //will need to be expanded. also will need to account for damage scripts
                {
                    var simple = damage.Simple;
                    var damageType = EnumUtil.ParseEnum<Enums.DamageType>(damage.Type.ToString(),
                        Enums.DamageType.Magical);
                    var dmg = rand.Next(Convert.ToInt32(simple.Min), Convert.ToInt32(simple.Max));
                    //these need to be set to integers as attributes. note to fix.
                    target.Damage(dmg, OffensiveElement, damageType, this);
                }
                else
                {
                    var formula = damage.Formula;
                    var damageType = EnumUtil.ParseEnum<Enums.DamageType>(damage.Type.ToString(),
                        Enums.DamageType.Magical);
                    FormulaParser parser = new FormulaParser(this, castObject, target);
                    var dmg = parser.Eval(formula);
                    if (dmg == 0) dmg = 1;
                    target.Damage(dmg, OffensiveElement, damageType, this);
                }
                //var dmg = rand.Next(Convert.ToInt32(simple.Min), Convert.ToInt32(simple.Max));
                //these need to be set to integers as attributes. note to fix.
                //target.Damage(dmg, OffensiveElement, damage.Type, this);
            }
            else
            {
                //var formula = damage.Formula;
            }
        }

        public bool CheckFacing(Direction direction, Creature target)
        {
            if (Math.Abs(this.X - Target.X) <= 1 && Math.Abs(this.Y - Target.Y) <= 1)
            {
                if (((this.X - Target.X) == 1 && (this.Y - Target.Y) == 0))
                {
                    //check if facing west
                    if (this.Direction == Direction.West) return true;
                    else
                    {
                        this.Turn(Direction.West);
                    }
                }
                if (((this.X - Target.X) == -1 && (this.Y - Target.Y) == 0))
                {
                    //check if facing east
                    if (this.Direction == Direction.East) return true;
                    else
                    {
                        this.Turn(Direction.East);
                    }
                }
                if (((this.X - Target.X) == 0 && (this.Y - Target.Y) == 1))
                {
                    //check if facing south
                    if (this.Direction == Direction.South) return true;
                    else
                    {
                        this.Turn(Direction.South);
                    }
                }
                if (((this.X - Target.X) == 0 && (this.Y - Target.Y) == -1))
                {
                    if (this.Direction == Direction.North) return true;
                    else
                    {
                        this.Turn(Direction.North);
                    }
                }
            }
            return false;
        }

        public override void Attack(Castables.Castable castObject, Creature target)
        {
            //do monster spell
        }

        public override void Attack(Castable castObject)
        {
            var damage = castObject.Effects.Damage;
            if (damage != null)
            {
                var intents = castObject.Intents;
                foreach (var intent in intents)
                {
                    var possibleTargets = new List<VisibleObject>();
                    Rectangle rect = new Rectangle(0, 0, 0, 0);

                    switch (intent.Direction)
                    {
                        case IntentDirection.Front:
                            {
                                switch (Direction)
                                {
                                    case Direction.North:
                                        {
                                            //facing north, attack north
                                            rect = new Rectangle(this.X, this.Y - intent.Radius, 1, intent.Radius);
                                        }
                                        break;
                                    case Direction.South:
                                        {
                                            //facing south, attack south
                                            rect = new Rectangle(this.X, this.Y, 1, 1 + intent.Radius);
                                        }
                                        break;
                                    case Direction.East:
                                        {
                                            //facing east, attack east
                                            rect = new Rectangle(this.X, this.Y, 1 + intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.West:
                                        {
                                            //facing west, attack west
                                            rect = new Rectangle(this.X - intent.Radius, this.Y, intent.Radius, 1);
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
                                            rect = new Rectangle(this.X, this.Y, 1, 1 + intent.Radius);
                                        }
                                        break;
                                    case Direction.South:
                                        {
                                            //facing south, attack north
                                            rect = new Rectangle(this.X, this.Y - intent.Radius, 1, intent.Radius);
                                        }
                                        break;
                                    case Direction.East:
                                        {
                                            //facing east, attack west
                                            rect = new Rectangle(this.X - intent.Radius, this.Y, intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.West:
                                        {
                                            //facing west, attack east
                                            rect = new Rectangle(this.X, this.Y, 1 + intent.Radius, 1);
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
                                            rect = new Rectangle(this.X - intent.Radius, this.Y, intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.South:
                                        {
                                            //facing south, attack east
                                            rect = new Rectangle(this.X, this.Y, 1 + intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.East:
                                        {
                                            //facing east, attack north
                                            rect = new Rectangle(this.X, this.Y, 1, 1 + intent.Radius);
                                        }
                                        break;
                                    case Direction.West:
                                        {
                                            //facing west, attack south
                                            rect = new Rectangle(this.X, this.Y - intent.Radius, 1, intent.Radius);
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
                                            rect = new Rectangle(this.X, this.Y, 1 + intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.South:
                                        {
                                            //facing south, attack west
                                            rect = new Rectangle(this.X - intent.Radius, this.Y, intent.Radius, 1);
                                        }
                                        break;
                                    case Direction.East:
                                        {
                                            //facing east, attack south
                                            rect = new Rectangle(this.X, this.Y - intent.Radius, 1, intent.Radius);
                                        }
                                        break;
                                    case Direction.West:
                                        {
                                            //facing west, attack north
                                            rect = new Rectangle(this.X, this.Y, 1, 1 + intent.Radius);
                                        }
                                        break;
                                }
                            }
                            break;
                        case IntentDirection.Nearby:
                            {
                                //attack radius
                                rect = new Rectangle(this.X - intent.Radius, this.Y - intent.Radius, intent.Radius * 2, intent.Radius * 2);
                            }
                            break;
                    }

                    if (!rect.IsEmpty) possibleTargets.AddRange(Map.EntityTree.GetObjects(rect).Where(obj => obj is Creature && obj != this && obj.GetType() != typeof(Merchant))); ;

                    var actualTargets = intent.MaxTargets > 0 ? possibleTargets.Take(intent.MaxTargets).OfType<Creature>().ToList() : possibleTargets.OfType<Creature>().ToList();

                    foreach (var target in actualTargets)
                    {
                        if (target is Monster || (target is User && ((User)target).Status.HasFlag(PlayerCondition.Pvp)))
                        {

                            var rand = new Random();

                            if (damage.Formula == null) //will need to be expanded. also will need to account for damage scripts
                            {
                                var simple = damage.Simple;
                                var damageType = EnumUtil.ParseEnum(damage.Type.ToString(), Enums.DamageType.Magical);
                                var dmg = rand.Next(Convert.ToInt32(simple.Min), Convert.ToInt32(simple.Max));
                                //these need to be set to integers as attributes. note to fix.
                                target.Damage(dmg, OffensiveElement, damageType, this);
                            }
                            else
                            {
                                var formula = damage.Formula;
                                var damageType = EnumUtil.ParseEnum(damage.Type.ToString(), Enums.DamageType.Magical);
                                var parser = new FormulaParser(this, castObject, target);
                                var dmg = parser.Eval(formula);
                                if (dmg == 0) dmg = 1;
                                target.Damage(dmg, OffensiveElement, damageType, this);

                                if (castObject.Effects.Animations.OnCast.Target == null) continue;
                                var effectAnimation = new ServerPacketStructures.EffectAnimation()
                                {
                                    SourceId = this.Id,
                                    Speed = (short)castObject.Effects.Animations.OnCast.Target.Speed,
                                    TargetId = target.Id,
                                    TargetAnimation = castObject.Effects.Animations.OnCast.Target.Id
                                };
                                //Enqueue(effectAnimation.Packet());
                                SendAnimation(effectAnimation.Packet());
                            }

                        }
                        else
                        {
                            //var formula = damage.Formula;
                        }
                    }
                }

                //TODO: DRY
                
                var sound = new ServerPacketStructures.PlaySound { Sound = (byte)castObject.Effects.Sound.Id };


                var playerAnimation = new ServerPacketStructures.PlayerAnimation()
                {
                    Animation = (byte) 255,
                    Speed = (ushort) (100 / 5), //handles the speed offset in this specific packet.
                    UserId = Id
                };
                    //Enqueue(playerAnimation.Packet());
                SendAnimation(playerAnimation.Packet());
                
                //Enqueue(sound.Packet());
                PlaySound(sound.Packet());
                //this is an attack skill
            }
            else
            {
                //need to handle scripting
            }
        }

        public void AssailAttack(Direction direction, Creature target = null)
        {
            if (target == null)
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

                var monster = obj as Monster;
                if (monster != null) target = monster;
                var user = obj as User;
                if (user != null)
                {
                    target = user;
                }
                var npc = obj as Merchant;
                if (npc != null)
                {
                    target = npc;
                }
                //try to get the creature we're facing and set it as the target.
            }
            
             Attack(direction, _simpleDamage, target);
                
            
            //animation handled here as to not repeatedly send assails.
            var assail = new ServerPacketStructures.PlayerAnimation() { Animation = 1, Speed = 20, UserId = this.Id };
            var sound = new ServerPacketStructures.PlaySound() { Sound = (byte)1 };
            //Enqueue(assail.Packet());
            //Enqueue(sound.Packet());
            SendAnimation(assail.Packet());
            PlaySound(sound.Packet());
        }



        public override void ShowTo(VisibleObject obj)
        {
            if (!(obj is User)) return;
            var user = obj as User;
            user.SendVisibleCreature(this);
        }

        public bool IsIdle()
        {
            return _idle;
        }

        public void Awaken()
        {
            _idle = false;
            //add to alive monsters?
        }

        public void Sleep()
        {
            _idle = true;
            //return to idle state
        }


        public override bool Walk(Direction direction)
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

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

}
