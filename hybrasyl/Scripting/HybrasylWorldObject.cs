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


using System.Reflection;
using Hybrasyl.Objects;
using Serilog;
using MoonSharp.Interpreter;
using Hybrasyl.Dialogs;

namespace Hybrasyl.Scripting
{
    /// <summary>
    /// A Lua wrapper object representing a Hybrasyl non-player world object (merchant, item, reactor, etc)
    /// </summary>
    [MoonSharpUserData]
    public class HybrasylWorldObject
    {
        internal WorldObject Obj { get; set; }

        // TODO: determine a better way to do this in lua via moonsharp
        /// <summary>
        /// A string representing the underlying object type (merchant, reactor, item, monster, etc)
        /// </summary>
        public string Type
        {
            get
            {
                if (Obj is Merchant) return "merchant";
                else if (Obj is Reactor) return "reactor";
                else if (Obj is ItemObject) return "item";
                else if (Obj is Monster) return "monster";
                return "idk";
            }
        }

        /// <summary>
        /// The name of the object.
        /// </summary>
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
        /// <summary>
        /// The current X coordinate location of the object.
        /// </summary>
        public byte X => Obj.X;
        /// <summary>
        /// The current Y coordinate location of the object.
        /// </summary>
        public byte Y => Obj.Y;

        public HybrasylWorldObject(WorldObject obj)
        {
            Obj = obj;
        }

        /// <summary>
        /// Display a main menu (pursuit list) to a player.
        /// </summary>
        /// <param name="invoker">The object invoking the pursuit list (e.g. a player that clicked on the NPC, item, etc)</param>
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

        /// <summary>
        /// Permanently destroy this object, if the underlying type is an item, or gold.
        /// </summary>
        public void Destroy()
        {
            if (Obj is ItemObject || Obj is Gold)
            {
                Game.World.Remove(Obj);
            }
        }

        /// <summary>
        /// Add a main menu item (pursuit) to this object's menu list.
        /// </summary>
        /// <param name="hybrasylSequence"></param>
        public void AddPursuit(HybrasylDialogSequence hybrasylSequence)
        {
            if (Obj is VisibleObject && !(Obj is User))
            {
                var vobj = Obj as VisibleObject;
                vobj.AddPursuit(hybrasylSequence.Sequence);
            }
        }

        /// <summary>
        /// Set a value in an *object's* ephemeral store. The store lasts for the
        /// lifetime of the object (for mobs, until they're killed; for NPCs, most likely
        /// until server restart, for players, while they're logged in). This is effectively
        /// NPC state memory that is player independent.
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

        /// <summary>
        /// Register a constructed dialog sequence with the current world object, which makes it available for use by that object.
        /// Dialogs must be registered before they can be used.
        /// </summary>
        /// <param name="hybrasylSequence"></param>
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
        /// Calculate the Manhattan distance between the current world object and a target object. Assumes objects are on the same map, otherwise the calculation is meaningless.
        /// </summary>
        /// <param name="target">The target object</param>
        /// <returns></returns>
        public int Distance(HybrasylWorldObject target) => Obj.Distance(target.Obj);

        /// <summary>
        /// Set the default sprite for this world object to a specified creature sprite.
        /// </summary>
        /// <param name="displaySprite">Integer referencing a creature sprite in the client datfiles.</param>
        public void SetNpcDisplaySprite(int displaySprite)
        {
            if (Obj is VisibleObject)
                ((VisibleObject) Obj).DialogSprite = (ushort)(0x4000 + displaySprite);
        }

        /// <summary>
        /// Set the default sprite for this world object to a specified item sprite.
        /// </summary>
        /// <param name="displaySprite">Integer referencing a creature sprite in the client datfiles.</param>
        public void SetItemDisplaySprite(int displaySprite)
        {
            if (Obj is VisibleObject)
                ((VisibleObject)Obj).DialogSprite = (ushort)(0x4000 + displaySprite);
        }

        /// <summary>
        /// Request an asynchronous dialog with a player. This can be used to ask a different player a question (such as for mentoring, etc).
        /// </summary>
        /// <param name="player">The logged-in player that will receive the dialog</param>
        /// <param name="sequence">The sequence that will be started for the target player.</param>
        /// <param name="requireLocal">Whether or not the player needs to be on the same map as the player causing the request.</param>
        /// <returns>Boolean indicating success</returns>
        public bool RequestDialog(string player, string sequence, bool requireLocal = true)
        {
            
            DialogSequence seq;
            if (Game.World.TryGetActiveUser(player, out User user))
            {
                if (Obj.SequenceCatalog.TryGetValue(sequence, out seq) ||
                    Game.World.GlobalSequences.TryGetValue(sequence, out seq))
                    // TODO: fix this awful object hierarchy nonsense
                    return Game.World.TryAsyncDialog(Obj as VisibleObject, user, seq);
            }
            return false;
        }

        /// <summary>
        /// Speak as the current world object ("white message").
        /// </summary>
        /// <param name="message">The text to be spoken</param>
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
