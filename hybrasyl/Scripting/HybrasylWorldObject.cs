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


using Hybrasyl.Casting;
using Hybrasyl.Dialogs;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using System.Linq;
using System.Reflection;
using Creature = Hybrasyl.Objects.Creature;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
public class HybrasylWorldObject : IScriptable
{
    public HybrasylWorldObject(IWorldObject obj)
    {
        WorldObject = obj;
    }

    public WorldObject Obj => WorldObject as WorldObject;

    public virtual bool IsPlayer => false;
    //public Xml.Direction Direction => WorldObject.Direction;
    public string Guid => Obj.Guid.ToString();

    public string LocationDescription
    {
        get
        {
            if (Obj is VisibleObject vo)
                return $"{vo.Map.Name} @ ({vo.X},{vo.Y}";
            return "Not on a map";
        }
    }

    public IWorldObject WorldObject { get; set; }
    public string Name => WorldObject.Name;
    public string Type => Obj.GetType().Name;


    /// <summary>
    ///     The current X coordinate location of the object.
    /// </summary>
    public byte X => Obj.X;

    /// <summary>
    ///     The current Y coordinate location of the object.
    /// </summary>
    public byte Y => Obj.Y;

    public void DebugFunction(string x)
    {
        GameLog.ScriptingWarning(x);
    }

    /// <summary>
    ///     Set the default sprite for this world object to a specified creature sprite.
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
    ///     Set the default sprite for this world object to a specified item sprite.
    /// </summary>
    /// <param name="displaySprite">Integer referencing a creature sprite in the client datfiles.</param>
    public void SetItemDisplaySprite(int displaySprite)
    {
        if (Obj is VisibleObject vobj)
            vobj.DialogSprite = (ushort)(0x4000 + displaySprite);
        else
            GameLog.ScriptingError("SetItemDisplaySprite: underlying object is not a visible object, ignoring");
    }

    /// <summary>
    ///     Return a localized string given a key
    /// </summary>
    /// <param name="key">The key to return. Note that NPCs can override localized strings, which take precedence.</param>
    /// <returns>The localized string for a given key</returns>
    public string GetLocalString(string key)
    {
        if (Obj is IResponseCapable m)
            return m.DefaultGetLocalString(key);
        return Game.World.GetLocalString(key);
    }

    /// <summary>
    ///     Display a main menu (pursuit list) to a player.
    /// </summary>
    /// <param name="invoker">The object invoking the pursuit list (e.g. a player that clicked on the NPC, item, etc)</param>
    public void DisplayPursuits(dynamic invoker)
    {
        if (invoker is null)
        {
            GameLog.ScriptingError("DisplayPursuits: invoker was null, ignoring");
            return;
        }

        if (Obj is IPursuitable pursuitable && invoker is HybrasylUser hybUser)
            pursuitable.DisplayPursuits(hybUser.User);
    }

    /// <summary>
    ///     Permanently destroy this object, if the underlying type is an item, or gold.
    /// </summary>
    public void Destroy()
    {
        if (Obj is ItemObject || Obj is Gold) Game.World.Remove(Obj);
    }

    /// <summary>
    ///     Add a main menu item (pursuit) to this object's menu list.
    /// </summary>
    /// <param name="hybrasylSequence"></param>
    public void AddPursuit(HybrasylDialogSequence hybrasylSequence)
    {
        if (hybrasylSequence is null || hybrasylSequence.Sequence.Dialogs.Count == 0)
        {
            GameLog.ScriptingError(
                "AddPursuit: Dialog sequence (first argument) was null or sequence was empty (no dialogs)");
            return;
        }

        if (Obj is IPursuitable pursuitable)
            pursuitable.AddPursuit(hybrasylSequence.Sequence);
    }

    /// <summary>
    ///     Set a value in an *object's* ephemeral store. The store lasts for the
    ///     lifetime of the object (for mobs, until they're killed; for NPCs, most likely
    ///     until server restart, for players, while they're logged in). This is effectively
    ///     NPC state memory that is player independent.
    /// </summary>
    /// <param name="key">The key we will store</param>
    /// <param name="value">The value (dynamic) we want to store</param>
    public void SetEphemeral(string key, dynamic value)
    {
        if (string.IsNullOrEmpty(key) || value is null)
        {
            GameLog.ScriptingError(
                "SetEphemeral: key (first argument) or value (second argument) was null or empty, ignoring");
            return;
        }

        if (Obj is not IEphemeral ephemeral) return;
        ephemeral.SetEphemeral(key, value);
        GameLog.ScriptingInfo("{Function}: {Name}, stored key {Key} with value {Value}",
            MethodBase.GetCurrentMethod().Name, Obj.Name, key, value);
    }

    /// <summary>
    ///     Set a scoped value in an *object's* ephemeral store. The store lasts for the
    ///     lifetime of the object (for mobs, until they're killed; for NPCs, most likely
    ///     until server restart, for players, while they're logged in). This is effectively
    ///     NPC state memory that is player independent. Scoped means it is tied to a specific
    ///     user.
    /// </summary>
    /// <param name="key">The key we will store</param>
    /// <param name="value">The value (dynamic) we want to store</param>
    public void SetScopedEphemeral(string user, string key, dynamic value)
    {
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(key) || value is null)
        {
            GameLog.ScriptingError(
                "SetEphemeral: user (first argument) or key (second argument) or value (third argument) was null or empty, ignoring");
            return;
        }

        if (Obj is not IEphemeral ephemeral) return;
        ephemeral.SetEphemeral($"{user.ToLower()}:{key}", value);
        GameLog.ScriptingInfo("{Function}: {Name}, stored scoped key {Key} with value {Value} for {user}",
            MethodBase.GetCurrentMethod().Name, Obj.Name, key, value, user);
    }

    /// <summary>
    ///     Remove the specified key from the object's ephemeral store.
    /// </summary>
    /// <param name="key"></param>
    public void ClearEphemeral(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            GameLog.ScriptingError("ClearEphemeral: key (first argument) was null or empty, ignoring");
            return;
        }

        if (Obj is not IEphemeral ephemeral) return;
        ephemeral.ClearEphemeral(key);
    }

    /// <summary>
    ///     Get the value of a specified key from the object's ephemeral store.
    /// </summary>
    /// <param name="key">The key to retrieve</param>
    /// <returns>dynamic value</returns>
    public dynamic GetEphemeral(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            GameLog.ScriptingError("GetEphemeral: key (first argument) was null or empty, returning nil");
            return DynValue.Nil;
        }

        if (Obj is not IEphemeral ephemeral) return DynValue.Nil;
        return ephemeral.TryGetEphemeral(key, out var value) ? value : DynValue.Nil;
    }

    /// <summary>
    ///     Get the value of a scoped ephemeral (a value stored scoped to a specific player) from the object's ephemeral store.
    /// </summary>
    /// <param name="user">The user for the scope</param>
    /// <param name="key">The key to retrieve</param>
    /// <returns>dynamic value</returns>
    public dynamic GetScopedEphemeral(string user, string key)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(user))
        {
            GameLog.ScriptingError(
                "GetScopedEphemeral: user (first argument) or key (second argument) was null or empty, returning nil");
            return DynValue.Nil;
        }

        if (Obj is not IEphemeral ephemeral) return DynValue.Nil;
        return ephemeral.TryGetEphemeral($"{user.ToLower()}:{key}", out var value) ? value : DynValue.Nil;
    }

    /// <summary>
    ///     Register a constructed dialog sequence with the current world object, which makes it available for use by that
    ///     object.
    ///     Dialogs must be registered before they can be used.
    /// </summary>
    /// <param name="hybrasylSequence"></param>
    public void RegisterSequence(HybrasylDialogSequence hybrasylSequence)
    {
        if (hybrasylSequence is null || hybrasylSequence.Sequence.Dialogs.Count == 0)
        {
            GameLog.Error("RegisterSequence: sequence (first argument) was null or contained no dialogs, ignoring");
            return;
        }

        if (Obj is IInteractable interactable)
            interactable.RegisterDialogSequence(hybrasylSequence.Sequence);
    }

    /// <summary>
    ///     Calculate the Manhattan distance between the current world object and a target object. Assumes objects are on the
    ///     same map, otherwise the calculation is meaningless.
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
            GameLog.ScriptingError(
                "Distance: target (first argument, {targetname}) not on same map as {thisname}, returning -1",
                v1.Name, v2.Name);
        }
        else
        {
            GameLog.ScriptingError(
                "Distance: either target (first argument) or this object was not a VisibleObject (not on a map), returning -1");
        }

        return -1;
    }

    /// <summary>
    /// Request an asynchronous dialog with a player. This can be used to ask a different player a question (such as for mentoring, etc).
    /// </summary>
    /// <param name="targetUser">The logged-in player that will receive the dialog</param>
    /// <param name="sourceGuid">The GUID of the source (player, merchant, etc)</param>
    /// <param name="sequenceName">The sequence that will be started for the target player</param>
    /// <param name="origin">The GUID of the origin for the request (castable, item, merchant, whatever). The origin must contain the script that will be used to handle the request.</param>
    /// <param name="requireLocal">Whether or not the player needs to be on the same map as the player causing the request.</param>
    /// <returns>Boolean indicating success</returns>
    public bool RequestDialog(string targetUser, string sourceGuid, string sequenceName, string originGuid,
        bool requireLocal = true)
    {
        IInteractable originInteractable = null;
        WorldObject originObj = null;
        CastableObject originCastable = null;
        if (string.IsNullOrEmpty(sequenceName) || string.IsNullOrEmpty(targetUser))
        {
            GameLog.ScriptingError(
                "RequestDialog: player (first argument) or sequence (second argument) was null or empty");
            return false;
        }

        if (!System.Guid.TryParse(sourceGuid, out var source) || !System.Guid.TryParse(originGuid, out var origin))
        {
            GameLog.ScriptingError($"RequestDialog: source or origin guid {sourceGuid} / {originGuid} is invalid");
            return false;
        }

        if (!Game.World.TryGetActiveUser(targetUser, out var user))
        {
            GameLog.ScriptingWarning($"RequestDialog: {targetUser} is not online");
            return false;
        }

        if (!Game.World.WorldState.TryGetWorldObject(origin, out originObj) &&
            !Game.World.WorldState.TryGetValueByIndex(origin, out originCastable))
        {
            GameLog.ScriptingWarning($"RequestDialog: {originGuid} not found");
            return false;
        }

        originInteractable = originObj == null ? originCastable : originObj as IInteractable;

        if (!originInteractable.SequenceIndex.ContainsKey(sequenceName) &&
            !Game.World.GlobalSequences.ContainsKey(sequenceName))
        {
            GameLog.ScriptingError($"RequestDialog: {targetUser} - sequence {sequenceName} was not found");
            return false;
        }

        if (!Game.World.WorldState.TryGetWorldObject(source, out WorldObject worldObj))
        {
            GameLog.ScriptingError($"RequestDialog: source guid {sourceGuid} could not be found");
            return false;
        }

        var session = new AsyncDialogSession(sequenceName, originInteractable, worldObj as IVisible, user);
        return Game.World.TryAsyncDialog(session);
    }

    /// <summary>
    ///     Speak as the current world object ("white message").
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
        {
            GameLog.ScriptingError("Say: only visible objects can speak, ignoring");
        }
    }

    /// <summary>
    ///     Refresh this object to a player. Can be used for sprite / other display changes.
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
        {
            GameLog.ScriptingError("ShowTo: only monsters can use this currently, ignoring");
        }
    }

    /// <summary>
    ///     Gets the objects facing direction.
    /// </summary>
    /// <returns>
    ///     Returns the direction for all world objects, if a form of creature (merchant, monster, user) returns
    ///     direction, all others return north.
    /// </returns>
    public Direction GetFacingDirection()
    {
        if (Obj is Creature creature)
            return creature.Direction;
        return Direction.North;
    }

    /// <summary>
    ///     Checks the specified X/Y point is free of creatures and is not a wall, if the object is a creature base type.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true/false</returns>
    public bool IsFreePoint(int x, int y)
    {
        if (Obj is Creature creature)
        {
            if (!creature.Map.IsWall(x, y))
            {
                if (!creature.Map.GetTileContents(x, y).Any(predicate: o => o is Creature))
                    return true;
                return false;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    ///     Display a special effect visible to players.
    /// </summary>
    /// <param name="effect">ushort id of effect (references client datfile)</param>
    /// <param name="speed">speed of the effect (generally 100)</param>
    /// <param name="global">
    ///     boolean indicating whether or not other players can see the effect, or just the player displaying
    ///     the effect
    /// </param>
    public void DisplayEffect(ushort effect, short speed = 100, bool global = true)
    {
        if (Obj is not VisibleObject vo) return;
        if (!global && Obj is User u)
            u.SendEffect(vo.X, vo.Y, effect, speed);
        else
            vo.Effect(effect, speed);
    }

    /// <summary>
    ///     Display an effect at a given x,y coordinate on the current player's map.
    /// </summary>
    /// <param name="x">X coordinate where effect will be displayed</param>
    /// <param name="y">Y coordinate where effect will be displayed</param>
    /// <param name="effect">ushort id of effect (references client datfile)</param>
    /// <param name="speed">speed of the effect (generally 100)</param>
    /// <param name="global">
    ///     boolean indicating whether or not other players can see the effect, or just the player displaying
    ///     boolean indicating whether or not other players can see the effect, or just the player displaying
    ///     the effect
    /// </param>
    public void DisplayEffectAtCoords(short x, short y, ushort effect, short speed = 100, bool global = true)
    {
        if (Obj is not VisibleObject vo) return;

        if (!global && Obj is User u)
            u.SendEffect(x, y, effect, speed);
        else
            vo.Effect(x, y, effect, speed);
    }

    /// <summary>
    ///     Play a sound effect.
    /// </summary>
    /// <param name="sound">byte id of the sound, referencing a sound effect in client datfiles.</param>
    public void SoundEffect(byte sound)
    {
        if (Obj is not VisibleObject vo) return;

        vo.PlaySound(sound);
    }

    /// <summary>
    /// Change the sprite of an object in the world. A Show() is automatically called to display the new sprite to any nearby players.
    /// </summary>
    /// <param name="sprite">ushort id of the sprite, referencing a sprite in client datfiles.</param>
    public void SetSprite(ushort sprite)
    {
        if (Obj is not VisibleObject vo) return;
        vo.Sprite = sprite;
        vo.Show();
    }

    /// <summary>
    ///     Teleport the object to an x,y coordinate location on the specified map.
    /// </summary>
    /// <param name="location">The map name</param>
    /// <param name="x">X coordinate target</param>
    /// <param name="y">Y coordinate target</param>
    public void Teleport(string location, int x, int y)
    {
        if (Obj is not VisibleObject vo) return;

        if (string.IsNullOrEmpty(location))
        {
            GameLog.ScriptingError(
                "Teleport: {user} - location name (first argument) was null or empty - aborting for safety", vo.Name);
            return;
        }

        vo.Teleport(location, (byte)x, (byte)y);
    }

    /// <summary>
    /// Teleport the object to an x,y coordinate location on its current map.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    public void TeleportToCoords(int x, int y)
    {
        if (Obj is not VisibleObject vo) return;
        vo.Teleport(vo.Map.Id, (byte)x, (byte)y);
    }
}