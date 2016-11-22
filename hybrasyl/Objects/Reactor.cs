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

namespace Hybrasyl.Objects
{
    public class Reactor : VisibleObject
    {
        //private reactor _reactor;
        private HybrasylWorldObject _world;

        public bool Ready;

        public Reactor(/* reactor reactor*/)
        {
            /*
            _reactor = reactor;
            _world = new HybrasylWorldObject(this);
            X = (byte)_reactor.map_x;
            Y = (byte)_reactor.map_y;
            Ready = false;
            Script = null;
             */
        }

        public void OnSpawn()
        {
            // Do we have a script?
            /*
                        Script thescript;
                        if (_reactor.script_name == String.Empty)
                            Game.World.ScriptProcessor.TryGetScript(_reactor.name, out thescript);
                        else
                            Game.World.ScriptProcessor.TryGetScript(_reactor.script_name, out thescript);

                        if (thescript == null)
                        {
                            Logger.WarnFormat("reactor {0}: script not found", _reactor.name);
                            return;
                        }

                        Script = thescript;

                        Script.AssociateScriptWithObject(this);

                        if (!Script.InstantiateScriptable())
                        {
                            Logger.WarnFormat("reactor {0}: script instantiation failed", _reactor.name);
                            return;
                        }

                        Script.ExecuteScriptableFunction("OnSpawn");
                        Ready = true;
             */
        }

        public void OnEntry(WorldObject obj)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnEntry", Script.GetObjectWrapper(obj));
        }

        public void AoiEntry(WorldObject obj)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnAoiEntry", Script.GetObjectWrapper(obj));
        }

        public void OnLeave(WorldObject obj)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnLeave", Script.GetObjectWrapper(obj));
        }

        public void AoiDeparture(WorldObject obj)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnAoiDeparture", Script.GetObjectWrapper(obj));
        }

        public void OnDrop(WorldObject obj, WorldObject dropped)
        {
            if (Ready)
                Script.ExecuteScriptableFunction("OnDrop", Script.GetObjectWrapper(obj),
                    Script.GetObjectWrapper(dropped));
        }
    }

}
