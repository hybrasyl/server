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

using Hybrasyl.Enums;
using Hybrasyl.Scripting;

namespace Hybrasyl.Objects
{
    public class Reactor : VisibleObject
    {

        public bool Ready
        {
            get
            {
                if (!_ready)
                    OnSpawn();
                return _ready;

            }
            set
            {
                _ready = value;
            }
        }
        public bool Blocking;
        public string Description;
        public string ScriptName;
        private bool _ready = false;

        public Reactor(byte x, byte y, Map map, string scriptName, string description = null, bool blocking = true) : base()
        {
            X = x;
            Y = y;
            Map = map;
            Description = description;
            ScriptName = scriptName;
            Blocking = blocking;
        }

        public void OnSpawn()
        {
            Script myScript;
            if (Game.World.ScriptProcessor.TryGetScript(ScriptName, out myScript))
            {
                Script = myScript;
                Script.AssociateScriptWithObject(this);
                _ready = Script.Run(false);
            }
            else
            {
                GameLog.Error($"{Map}: reactor at {X},{Y}: reactor script {ScriptName} not found!");
            }
            // Now run our actual OnSpawn function
            if (_ready)
                Script.ExecuteFunction("OnSpawn");
        }

        public virtual void OnEntry(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                user.LastAssociate = this;
                if (!user.Condition.Alive && !AllowDead)
                    return;
            }
            if (Ready)
                Script.ExecuteFunction("OnEntry", Script.GetObjectWrapper(obj), Script.GetObjectWrapper(this));
        }

        public override void AoiEntry(VisibleObject obj)
        {
            base.AoiEntry(obj);
            if (Ready)
                Script.ExecuteFunction("AoiEntry", Script.GetObjectWrapper(obj), Script.GetObjectWrapper(this));
        }

        public virtual void OnLeave(VisibleObject obj)
        {
            if (Ready && Script.HasFunction("OnLeave"))
                Script.ExecuteFunction("OnLeave", Script.GetObjectWrapper(obj), Script.GetObjectWrapper(this));
            if (obj is User)
                ((User)obj).LastAssociate = null;
        }

        public override void AoiDeparture(VisibleObject obj)
        {
            base.AoiDeparture(obj);
            if (Ready)
                Script.ExecuteFunction("AoiDeparture", Script.GetObjectWrapper(obj), Script.GetObjectWrapper(this));
        }

        public virtual void OnDrop(VisibleObject obj, VisibleObject dropped)
        {
            if (Ready)
                Script.ExecuteFunction("OnDrop", Script.GetObjectWrapper(obj), Script.GetObjectWrapper(this),
                    Script.GetObjectWrapper(dropped));
        }


        public void OnMove(VisibleObject obj)
        {
            if (Ready)
                Script.ExecuteFunction("OnMove", Script.GetObjectWrapper(obj), Script.GetObjectWrapper(this));
        }

        public void OnTake(VisibleObject obj, VisibleObject taken)
        {
            if (Ready)
                Script.ExecuteFunction("OnTake", Script.GetObjectWrapper(obj), Script.GetObjectWrapper(this),
                    Script.GetObjectWrapper(taken));
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                // TODO: improve, this isn't sufficient to work with Say/Shout currently
                var user = obj as User;
                var p = new ServerPacket(0x07);
                p.WriteUInt16(1);
                p.WriteUInt16(X);
                p.WriteUInt16(Y);
                p.WriteUInt32(Id);
                p.WriteUInt16(0);
                p.WriteByte(0); // random 1                                                                                                                                                                                                
                p.WriteByte(0); // random 2                                                                                                                                                                                                
                p.WriteByte(0); // random 3                                                                                                                                                                                                
                p.WriteByte(0); // unknown a                                                                                                                                                                                               
                p.WriteByte((byte)Direction);
                p.WriteByte(0); // unknown b                                                                                                                                                                                               
                p.WriteByte(0);
                p.WriteByte(0); // unknown d                                                                                                                                                                                               
                p.WriteByte((byte) MonsterType.Reactor);
                p.WriteString8(Name);
                user.Enqueue(p);
            }
        }
    }

}
