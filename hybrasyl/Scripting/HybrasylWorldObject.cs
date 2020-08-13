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

        public string LocationDescription
        {
            get
            {
                if (Obj is VisibleObject vo)
                    return $"{vo.Map.Name} @ ({vo.X},{vo.Y}";
                return "Not on a map";
            }
        }

        /// <summary>
        /// The name of the object.
        /// </summary>
        public string Name
        {
            get
            {
                if (Obj is ItemObject item)
                    return item.Name;
                else
                    return Obj.Name;
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
            if (invoker is null)
            {
                GameLog.ScriptingError("DisplayPursuits: invoker was null, ignoring");
                return;
            }
            if (Obj is Merchant || Obj is Reactor)
            {
                var merchant = Obj as VisibleObject;
                if (invoker is HybrasylUser hybUser)
                {
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
            if (hybrasylSequence is null || hybrasylSequence.Sequence.Dialogs.Count == 0)
            {
                GameLog.ScriptingError("AddPursuit: Dialog sequence (first argument) was null or sequence was empty (no dialogs)");
            }
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
            if (string.IsNullOrEmpty(key) || value is null)
            {
                GameLog.ScriptingError("SetEphemeral: key (first argument) or value (second argument) was null or empty, ignoring");
                return;
            }
            Obj.SetEphemeral(key, value);
            GameLog.ScriptingInfo("{Function}: {Name}, stored key {Key} with value {Value}",
                    MethodInfo.GetCurrentMethod().Name, Obj.Name, key, value);
        }

        /// <summary>
        /// Remove the specified key from the object's ephemeral store.
        /// </summary>
        /// <param name="key"></param>
        public void ClearEphemeral(string key) 
        {
            if (string.IsNullOrEmpty(key))
            {
                GameLog.ScriptingError("ClearEphemeral: key (first argument) was null or empty, ignoring");
                return;
            }
            Obj.ClearEphemeral(key);

        }

        /// <summary>
        /// Get the value of a specified key from the object's ephemeral store.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public dynamic GetEphemeral(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                GameLog.ScriptingError("GetEphemeral: key (first argument) was null or empty, returning nil");
                return DynValue.Nil;
            }
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
            if (hybrasylSequence is null || hybrasylSequence.Sequence.Dialogs.Count == 0)
            {
                GameLog.Error("RegisterSequence: sequence (first argument) was null or contained no dialogs, ignoring");
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
        public int Distance(HybrasylWorldObject target)
        {
            if (target is null)
            {
                GameLog.ScriptingError("Distance: target (first argument) was null, returning -1");
                return -1;
            }
            if (target.Obj is VisibleObject v1 && Obj is VisibleObject v2)
            {
                if (v1.Map.Id == v2.Map.Id)
                    return Obj.Distance(target.Obj);
                else
                {
                    GameLog.ScriptingError("Distance: target (first argument, {targetname}) not on same map as {thisname}, returning -1",
                        v1.Name, v2.Name);
                }
            }
            else
                GameLog.ScriptingError("Distance: either target (first argument) or this object was not a VisibleObject (not on a map), returning -1");
            return -1;
        }
        /// <summary>
        /// Set the default sprite for this world object to a specified creature sprite.
        /// </summary>
        /// <param name="displaySprite">Integer referencing a creature sprite in the client datfiles.</param>
        public void SetNpcDisplaySprite(int displaySprite)
        {
            if (Obj is VisibleObject vobj)
                vobj.DialogSprite = (ushort)(0x4000 + displaySprite);
            else
                GameLog.ScriptingError("SetNpcDisplaySprite: underlying object is not a visible object, ignoring");
        }

        /// <summary>
        /// Set the default sprite for this world object to a specified item sprite.
        /// </summary>
        /// <param name="displaySprite">Integer referencing a creature sprite in the client datfiles.</param>
        public void SetItemDisplaySprite(int displaySprite)
        {
            if (Obj is VisibleObject vobj)
                vobj.DialogSprite = (ushort)(0x4000 + displaySprite);
            else
                GameLog.ScriptingError("SetItemDisplaySprite: underlying object is not a visible object, ignoring");
        }

        public void SetCreatureDisplaySprite(int displaySprite)
        {
            if (Obj is Monster monster)
                monster.Sprite = (ushort)displaySprite;
            else
                GameLog.ScriptingError("SetCreatureDisplaySprite: underlying object is not a monster, ignoring");
        }

        public int GetCreatureDisplaySprite()
        {
            if (Obj is Monster monster)
                return monster.Sprite;
            return 0;
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
            if (string.IsNullOrEmpty(player) || string.IsNullOrEmpty(sequence))
            {
                GameLog.ScriptingError("RequestDialog: player (first argument) or sequence (second argument) was null or empty, returning false");
                return false;
            }

            DialogSequence seq;
            if (Game.World.TryGetActiveUser(player, out User user))
            {
                if (Obj.SequenceCatalog.TryGetValue(sequence, out seq) ||
                    Game.World.GlobalSequences.TryGetValue(sequence, out seq))
                    // TODO: fix this awful object hierarchy nonsense
                    return Game.World.TryAsyncDialog(Obj as VisibleObject, user, seq);
                else
                    GameLog.ScriptingError("RequestDialog: {player} - sequence {sequence} was not found", user.Name, sequence);
            }
            else
                GameLog.ScriptingWarning("RequestDialog: {player} is not online", user.Name);
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
            else
                GameLog.ScriptingError("Say: only visible objects can speak, ignoring");
        }

        /// <summary>
        /// Refresh this object to a player. Can be used for sprite / other display changes.
        /// </summary>
        /// <param name="user">User object that will receive the update.</param>
        public void ShowTo(HybrasylUser user)
        {
            if (Obj is Monster)
            {
                var monster = Obj as Monster;
                monster.ShowTo(user.User);
            }
            else
                GameLog.ScriptingError("ShowTo: only monsters can use this currently, ignoring");
        }
    }
}
