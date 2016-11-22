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
using Hybrasyl.Creatures;
using Hybrasyl.Enums;
using Castable = Hybrasyl.Castables.Castable;

namespace Hybrasyl.Objects
{
    public class Monster : Creature, ICloneable
    {
        private bool _idle = true;

        private uint _mTarget;

        private Spawn _spawn;

        public Monster()
        {

        }

        public override void OnDeath()
        {
            Shout("AAAAAAAAAAaaaaa!!!");
            Map.Remove(this);
            // Now that we're dead, award loot.
            // FIXME: Implement loot tables / full looting.
            var hitter = LastHitter as User;
            if (hitter == null) return; // Don't handle cases of MOB ON MOB COMBAT just yet
            hitter.ShareExperience(_spawn.Loot.Xp);

            if (_spawn.Loot.Gold <= 0) return;
            var golds = new Gold(_spawn.Loot.Gold);
            World.Insert(golds);
            Map.Insert(golds, X,Y);
            World.Remove(this);
        }

        public Monster(Hybrasyl.Creatures.Creature creature, Spawn spawn, int map)
        {
            Sprite = creature.Sprite;
            World = Game.World;
            Map = Game.World.Maps[(ushort)map];
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

        public override void Attack(Direction direction, Castables.Castable castObject, Creature target)
        {
            //do monster attack.
        }

        public override void Attack(Castables.Castable castObject, Creature target)
        {
            //do monster spell
        }

        public override void Attack(Castable castObject)
        {
            //do monster aoe
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                user.SendVisibleCreature(this);
            }
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

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

}
