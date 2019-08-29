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


using System.Reflection;
using Hybrasyl.Objects;
using Serilog;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]
    public class HybrasylWorldObject
    {
        internal WorldObject Obj { get; set; }

        public string Name
        {
            get
            {
                if (Obj is ItemObject)
                    return (Obj as ItemObject).Name;
                else
                    return Name;
            }
        }
        public byte X => Obj.X;
        public byte Y => Obj.Y;

        public HybrasylWorldObject(WorldObject obj)
        {
            Obj = obj;
        }

        public void DisplayPursuits(dynamic invoker)
        {
            if (Obj is Merchant || Obj is Reactor)
            {
                var merchant = Obj as VisibleObject;
                if (invoker is HybrasylUser)
                {
                    var hybUser = (HybrasylUser)invoker;
                    merchant.DisplayPursuits(hybUser.User);
                }
            }

        }

        public void Destroy()
        {
            if (Obj is ItemObject || Obj is Gold)
            {
                Game.World.Remove(Obj);
            }
        }

        public void AddPursuit(HybrasylDialogSequence hybrasylSequence)
        {
            if (Obj is VisibleObject && !(Obj is User))
            {
                var vobj = Obj as VisibleObject;
                vobj.AddPursuit(hybrasylSequence.Sequence);
            }
        }

        /// <summary>
        /// Set a value in an object's ephemeral store. The store lasts for the
        /// lifetime of the object (for mobs, until they're killed; for NPCs, most likely
        /// until server restart, for players, while they're logged in).
        /// </summary>
        /// <param name="key">The key we will store</param>
        /// <param name="value">The value (dynamic) we want to store</param>
        public void SetEphemeral(string key, dynamic value)
        {
            Obj.SetEphemeral(key, value);
            GameLog.ScriptingInfo("{Function}: {Name}, stored key {Key} with value {Value}",
                    MethodInfo.GetCurrentMethod().Name, Obj.Name, key, value);
        }

        /// <summary>
        /// Remove the specified key from the object's ephemeral store.
        /// </summary>
        /// <param name="key"></param>
        public void ClearEphemeral(string key) => Obj.ClearEphemeral(key);
        
        /// <summary>
        /// Get the value of a specified key from the object's ephemeral store.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public dynamic GetEphemeral(string key)
        {
            if (Obj.TryGetEphemeral(key, out dynamic value))
                return value;
            else return DynValue.Nil;
        }

        public void RegisterSequence(HybrasylDialogSequence hybrasylSequence)
        {
            if (hybrasylSequence is null)
            {
                GameLog.Error("RegisterSequence called with null/nil value. Check Lua");
                return;       
            }
            if (Obj is VisibleObject && !(Obj is User))
            {
                var vobj = Obj as VisibleObject;
                vobj.RegisterDialogSequence(hybrasylSequence.Sequence);
            }
        }

        /// <summary>
        /// Calculate the Manhattan distance between ourselves and a target object.
        /// </summary>
        /// <param name="target">The target object</param>
        /// <returns></returns>
        public int Distance(HybrasylWorldObject target) => Obj.Distance(target.Obj);

        public void SetNpcDisplaySprite(int displaySprite)
        {
            if (Obj is VisibleObject)
                ((VisibleObject) Obj).DialogSprite = (ushort)(0x4000 + displaySprite);
        }

        public void SetItemDisplaySprite(int displaySprite)
        {
            if (Obj is VisibleObject)
                ((VisibleObject)Obj).DialogSprite = (ushort)(0x4000 + displaySprite);
        }

        public void Say(string message)
        {
            if (Obj is VisibleObject)
            {
                var creature = Obj as VisibleObject;
                creature.Say(message);
            }
        }

    }
}
