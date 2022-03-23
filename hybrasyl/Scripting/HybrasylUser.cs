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
using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
public class HybrasylUser
{
    public void DebugFunction(string x)
    {
        GameLog.ScriptingWarning(x);
    }
    internal User User { get; set; }
    internal HybrasylWorld World { get; set; }
    public HybrasylMap Map { get; set; }
    /// <summary>
    /// The name of the player.
    /// </summary>
    public string Name => User.Name;
    /// <summary>
    /// The current X coordinate of the player.
    /// </summary>
    public byte X => User.X;
    /// <summary>
    /// The current Y coordinate of the player.
    /// </summary>
    public byte Y => User.Y;
    /// <summary>
    /// The user's class (e.g. Rogue, Warrior, etc)
    /// </summary>
    public Xml.Class Class => User.Class;

    public string MapName => User.Map?.Name ?? "Unknown Kadath";

    /// <summary>
    /// The user's previous class, if a subpath.
    /// </summary>
    public Xml.Class PreviousClass => User.PreviousClass;

    // TODO: determine a better way to do this in lua via moonsharp
    /// <summary>
    /// The type of object this is. This is a shortcut to reference in scripting as evaluating type is annoying; so you can check the Type property instead.
    /// e.g. invoker.Type == "player"
    /// </summary>
    public static string Type => "player";

    // TODO: object inheritance for scripting objects
    public static bool IsPlayer => true;
    /// <summary>
    /// The direction this object is facing.
    /// </summary>
    public Xml.Direction Direction => User.Direction;


    /// <summary>
    /// The gender of the player. For Darkages purpose, this will evaluate to Male or Female.
    /// </summary>
    public Xml.Gender Gender => User.Gender;

    /// <summary>
    /// The current HP (hit points) of the user. This can be set to an arbitrary value; the player's HP display is automatically updated.
    /// </summary>
    public uint Hp
    {
        get => User.Stats.Hp;
        set {
            User.Stats.Hp = value;
            User.UpdateAttributes(StatUpdateFlags.Current);
        }
    }

    /// <summary>
    /// The current level of the user. Client supports up to level 255; Hybrasyl has the same level cap as usda, 99. 
    /// </summary>
    public int Level => User.Stats.Level;

    /// <summary>
    /// Amount of gold the user currently has.
    /// </summary>

    public uint Gold => User.Stats.Gold;

    /// <summary>
    /// Access the StatInfo of the specified user directly (all stats).
    /// </summary>
    public StatInfo Stats => User.Stats;

    /// <summary>
    /// Whether the user is alive or not.
    /// </summary>
    public bool Alive => User.Condition.Alive;

    /// <summary>
    /// Give the specified amount of gold to the user.
    /// </summary>
    /// <param name="gold">Amount of gold to give.</param>
    /// <returns></returns>
    public bool AddGold(uint gold) => User.AddGold(gold);

    /// <summary>
    /// Take the specified amount of gold from the user.
    /// </summary>
    /// <param name="gold">Amount of gold to take.</param>
    /// <returns></returns>
    public bool RemoveGold(uint gold) => User.RemoveGold(gold);

    /// <summary>
    /// Removes a skill from the user's skillbook
    /// </summary>
    /// <param name="name">Skill to be removed</param>
    /// <returns>boolean indicating success</returns>
    public bool RemoveSkill(string name)
    {
        var slot = User.SkillBook.SlotOf(name);
        if (!User.SkillBook.Remove(slot)) return false;
        User.SendClearSkill(slot);
        return true;
    }

    /// <summary>
    /// Removes a spel from the user's spellbook
    /// </summary>
    /// <param name="name">Spell to be removed</param>
    /// <returns>boolean indicating success</returns>
    public bool RemoveSpell(string name)
    {
        var slot = User.SpellBook.SlotOf(name);
        if (!User.SpellBook.Remove(slot)) return false;
        User.SendClearSkill(slot);
        return true;
    }


    /// <summary>
    /// The current MP (magic points) of the user. This can be set to an arbitrary value; the player's MP display is automatically updated.
    /// </summary>
    public uint Mp
    {
        get => User.Stats.Mp;
        set
        {
            User.Stats.Mp = value;
            User.UpdateAttributes(StatUpdateFlags.Current);
        }
    }

    public string GetNation() => User.Nation?.Name ?? string.Empty;

    public bool SetNation(string nationName) => User.ChangeCitizenship(nationName);

    public HybrasylUser(User user)
    {
        User = user;
        World = new HybrasylWorld(user.World);
        Map = new HybrasylMap(user.Map);
    }
    /// <summary>
    /// Get a list of objects in the viewport of the player. This represents all visible objects (items, players, creatures) contained in the client's viewport (the drawable map area).
    /// </summary>
    /// <returns></returns>
    public List<HybrasylWorldObject> GetViewportObjects()
    {
        return new List<HybrasylWorldObject>();
    }

    /// <summary>
    /// Get a list of players in the viewport of the player. This represents only players contained in the client's viewport (the drawable map area).
    /// </summary>
    /// <returns></returns>
    public List<HybrasylUser> GetViewportPlayers()
    {
        return new List<HybrasylUser>();
    }

    /// <summary>
    /// Resurrect a user. They respawn in their home map with 1HP/1MP and with scars, if configured in the death handler.
    /// </summary>
    public void Resurrect()
    {
        if (!User.Condition.Alive)
            User.Resurrect();
    }

    /// <summary>
    /// Get the player, if any, that the current player is facing ("looking at").
    /// </summary>
    /// <returns>HybrasylUser object for the player facing this player, or nil, if the player isn't directly facing another player.</returns>
    public HybrasylUser GetFacingUser()
    {
        var facing = User.GetFacingUser();
        return facing != null ? new HybrasylUser(facing) : null;
    }

    /// <summary>
    /// Get the objects a player is facing (for instance, items on the ground in front of the player)
    /// </summary>
    /// <param name="distance"></param>
    /// <returns>A list of HybrasylWorldObjects that the player is facing.</returns>
    public List<HybrasylWorldObject> GetFacingObjects(int distance = 1)
    {
        return User.GetFacingObjects(distance).Select(item => new HybrasylWorldObject(item)).ToList();
    }

    /// <summary>
    /// Get the monster that the placer is facing.
    /// </summary>
    /// <returns>A HybrasylMonster object.</returns>
    public HybrasylMonster GetFacingMonster()
    {
        var facing = (Monster)(User.GetFacingObjects().Where(X => X is Monster).FirstOrDefault());
        return facing != null ? new HybrasylMonster(facing) : null;
    }

    /// <summary>
    /// Get the direction the user is facing.
    /// </summary>
    /// <returns>A string representation of the user's direction.</returns>
    public string GetDirection()
    {
        return Enum.GetName(typeof(Xml.Direction), User.Direction);
    }

    /// <summary>
    /// Set the users facing direction.
    /// </summary>
    /// <param name="direction"></param>
    public void ChangeDirection(string direction)
    {
        Enum.TryParse(typeof(Xml.Direction), direction, out var result);
        User.Direction = (Xml.Direction)result;
    }

    /// <summary>
    /// End coma state (e.g. beothaich was used)
    /// </summary>
    public void EndComa()
    {
        User.EndComa();
    }

    /// <summary>
    /// Teleport a user to their "home" (ordinary spawnpoint), or a map of last resort.
    /// </summary>
    public void SendHome()
    {
        if (User.Nation.SpawnPoints.Count != 0)
        {
            var spawnpoint = User.Nation.RandomSpawnPoint;
            if (spawnpoint != null) 
                User.Teleport(spawnpoint.MapName, spawnpoint.X, spawnpoint.Y);
            return;
        }
        User.Teleport((ushort)500, (byte)50, (byte)(50));
    }

    /// <summary>
    /// Get a legend mark from the current player's legend (a list of player achievements and accomplishments which is visible by anyone in the world), given a legend
    /// prefix. All legend marks have invisible prefixes (keys) for editing / storage capabilities.
    /// </summary>
    /// <param name="prefix">The prefix we want to retrieve (legend key)</param>
    /// <returns></returns>
    public dynamic GetLegendMark(string prefix)
    {
        LegendMark mark;
        return User.Legend.TryGetMark(prefix, out mark) ? mark : (object)null;
    }

    /// <summary>
    /// Check to see if a player has a legend mark with the specified prefix in their legend.
    /// </summary>
    /// <param name="prefix">Prefix of the mark to check</param>
    /// <returns>boolean</returns>
    public bool HasLegendMark(string prefix) => User.Legend.TryGetMark(prefix, out LegendMark _);

    /// <summary>
    /// Check to see if the player has an item equipped with the specified name.
    /// </summary>
    /// <param name="item"></param>
    /// <returns>boolean</returns>
    public bool HasEquipment(string item) => User.Equipment.ContainsId(item, 1);

    /// <summary>
    /// Change the class of a player to a new class. The player's class will immediately change and they will receive a legend mark that 
    /// reads "newClass by oath of oathGiver, XXX".
    /// </summary>
    /// <param name="newClass">The player's new class./param>
    /// <param name="oathGiver">The name of the NPC or player who gave oath for this class change.</param>
    public void ChangeClass(Xml.Class newClass, string oathGiver)
    {
        User.Class = newClass;
        User.UpdateAttributes(StatUpdateFlags.Full);
        LegendIcon icon;
        string legendtext;
        // this is annoying af
        switch (newClass)
        {
            case Xml.Class.Monk:
                icon = LegendIcon.Monk;
                legendtext = $"Monk by oath of {oathGiver}";
                break;
            case Xml.Class.Priest:
                icon = LegendIcon.Priest;
                legendtext = $"Priest by oath of {oathGiver}";
                break;
            case Xml.Class.Rogue:
                icon = LegendIcon.Rogue;
                legendtext = $"Rogue by oath of {oathGiver}";
                break;
            case Xml.Class.Warrior:
                icon = LegendIcon.Warrior;
                legendtext = $"Warrior by oath of {oathGiver}";
                break;
            case Xml.Class.Wizard:
                icon = LegendIcon.Wizard;
                legendtext = $"Wizard by oath of {oathGiver}";
                break;
            default:
                GameLog.ScriptingError("ChangeClass: {user} - unknown class (first argument) passed", User.Name);
                throw new ArgumentException("Invalid class");
        }
        User.Legend.AddMark(icon, LegendColor.White, legendtext, "CLS");
    }

    /// <summary>
    /// Generate a list of reactors in the current user's viewport.
    /// </summary>
    /// <returns>List of HybrasylReactors in the viewport</returns>
    public List<HybrasylReactor> GetReactorsInViewport() => User.Map.EntityTree.GetObjects(User.GetViewport()).Where(x => x is Reactor).Select(r => new HybrasylReactor(r as Reactor)).ToList();

    /// <summary>
    /// Check to see whether the user has killed a named monster, optionally in the last n minutes.
    /// </summary>
    /// <param name="name">The name of the monster to check</param>
    /// <param name="minutes">The number of minutes to limit the check. 0 is default and means no limit.</param>
    /// <returns></returns>
    public bool HasKilled(string name, int minutes = 0)
    {
        var ts = DateTime.Now;
        var matches = User.RecentKills.Where(x => x.Name.ToLower() == name.ToLower()).ToList();
        switch (matches.Count)
        {
            case > 0 when minutes == 0:
                return true;
            case > 0 when minutes > 0:
            {
                if (matches.Any(rec => (ts - rec.Timestamp).TotalMinutes <= minutes))
                {
                    return true;
                }

                break;
            }
        }
        return false;
    }

    /// <summary>
    /// Return the player's entire legend.
    /// </summary>
    /// <returns></returns>
    public Legend GetLegend()
    {
        return User.Legend;
    }

    /// <summary>
    /// Add a legend mark with the specified icon, color, text, and prefix to a player's legend, which will default to being issued now (current in-game time).
    /// </summary>
    /// <param name="icon">The icon to be used for the mark (heart, sword, etc)</param>
    /// <param name="color">The color the mark will be rendered in (blue, yellow, orange, etc)</param>
    /// <param name="text">The actual text of the legend mark.</param>
    /// <param name="prefix">An invisible key (stored in the beginning of the mark) that can be used to refer to the mark later.</param>
    /// <param name="isPublic">Whether or not this legend mark can be seen by other players. By convention, private marks are prefixed with " - ".</param>
    /// <param name="quantity">Quantity of the legend mark. For instance "Mentored Dude (2)". Also by convention, quantity is expressed in parenthesis at the end of the mark.</param>
    /// <param name="displaySeason">Whether or not to display the season of a mark (e.g. Fall, Summer)</param>
    /// <param name="displayTimestamp">Whether or not to display the in-game time of a mark (e.g. Hybrasyl 5)</param>
    /// <returns></returns>
    public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, string prefix=default(string), bool isPublic = true, 
        int quantity = 0, bool displaySeason=true, bool displayTimestamp=true)
    {
        return AddLegendMark(icon, color, text, DateTime.Now, prefix, isPublic, quantity, displaySeason, displayTimestamp);
    }

    /// <summary>
    /// Add a legend mark with the specified icon, color, text, timestamp and prefix to a player's legend.
    /// </summary>
    /// <param name="icon">The icon to be used for the mark (heart, sword, etc)</param>
    /// <param name="color">The color the mark will be rendered in (blue, yellow, orange, etc)</param>
    /// <param name="text">The actual text of the legend mark.</param>
    /// <param name="timestamp">The in-game time the legend was awarded.</param>
    /// <param name="prefix">An invisible key (stored in the beginning of the mark) that can be used to refer to the mark later.</param>
    /// <returns></returns>
    public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, HybrasylTime timestamp, string prefix) => User.Legend.AddMark(icon, color, text, timestamp.TerranDateTime, prefix);

    /// <summary>
    /// Add a legend mark to a player's legend.
    /// </summary>
    /// <param name="icon">The icon to be used for the mark (heart, sword, etc)</param>
    /// <param name="color">The color the mark will be rendered in (blue, yellow, orange, etc)</param>
    /// <param name="text">The actual text of the legend mark.</param>
    /// <param name="timestamp">The Terran time the legend was awarded.</param>
    /// <param name="prefix">An invisible key (stored in the beginning of the mark) that can be used to refer to the mark later.</param>
    /// <param name="isPublic">Whether or not this legend mark can be seen by other players. By convention, private marks are prefixed with " - ".</param>
    /// <param name="quantity">Quantity of the legend mark. For instance "Mentored Dude (2)". Also by convention, quantity is expressed in parenthesis at the end of the mark.</param>
    /// <param name="displaySeason">Whether or not to display the season of a mark (e.g. Fall, Summer)</param>
    /// <param name="displayTimestamp">Whether or not to display the in-game time of a mark (e.g. Hybrasyl 5)</param>
    /// <returns></returns>
    public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, DateTime timestamp, string prefix = default(string), 
        bool isPublic = true, int quantity = 0, bool displaySeason=true, bool displayTimestamp=true)
    {
        try
        {
            return User.Legend.AddMark(icon, color, text, timestamp, prefix, isPublic, quantity, displaySeason, displayTimestamp);
        }
        catch (ArgumentException)
        {
            GameLog.ScriptingError("AddLegendMark: {user} - duplicate prefix {prefix}", User.Name, prefix);
        }
        return false;
    }

    /// <summary>
    /// Remove the given legend mark from a player's legend.
    /// </summary>
    /// <param name="prefix">The prefix key of the legend mark to be removed.</param>
    /// <returns>Boolean indicating success or failure.</returns>
    public bool RemoveLegendMark(string prefix)
    {
        return User.Legend.RemoveMark(prefix);
    }

    /// <summary>
    /// Modify a previously created legend mark. You can set a new quantity, or set an existing mark as public or private.
    /// </summary>
    /// <param name="prefix">Prefix key of the legend mark to be modified.</param>
    /// <param name="quantity">A quantity to be assigned to the mark.</param>
    /// <param name="isPublic">Whether or not the mark should be public or not.</param>
    /// <returns>Boolean indicating whether the mark for modification was found or not</returns>
    public bool ModifyLegendMark(string prefix, int quantity, bool isPublic)
    {
        if (!User.Legend.TryGetMark(prefix, out LegendMark mark)) return false;
        mark.Quantity = quantity;
        mark.Public = isPublic;
        return true;
    }

    /// <summary>
    /// Increment a legend mark with a quantity.
    /// </summary>
    /// <param name="prefix">The legend mark prefix to modify.</param>
    /// <param name="preserveDate">Whether or not to preserve the date. If true, the date of the mark is not updated.</param>
    /// <returns>Boolean indicating whether the mark existed / was updated</returns>
    public bool IncrementLegendMark(string prefix, bool preserveDate=true)
    {
        if (!User.Legend.TryGetMark(prefix, out LegendMark mark))
            return false;
        mark.Quantity++;
        if (!preserveDate)
            mark.Timestamp = DateTime.Now;
        return true;
    }

    /// <summary>
    /// Decrement a legend mark with a quantity.
    /// </summary>
    /// <param name="prefix">The legend mark prefix to modify.</param>
    /// <param name="preserveDate">Whether or not to preserve the date. If true, the date of the mark is not updated.</param>
    /// <returns>Boolean indicating whether the mark existed / was updated</returns>
    public bool DecrementLegendMark(string prefix, bool preserveDate=true)
    {
        if (!User.Legend.TryGetMark(prefix, out LegendMark mark))
            return false;
        mark.Quantity--;
        if (!preserveDate)
            mark.Timestamp = DateTime.Now;
        return true;
    }

    /// <summary>
    /// Request a sequence between two players. This is primarily used to start asynchronous dialog sequences (for things like mentoring or religion where confirmation from a second
    /// player is required).
    /// </summary>
    /// <param name="sequence">The sequence name to start</param>
    /// <param name="invoker">The player invoking the asynchronous dialog.</param>
    /// <returns>Boolean indicating whether or not the request was successful.</returns>
    public bool RequestDialog(string sequence, string invoker = "")
    {
        if (string.IsNullOrEmpty(sequence))
        {
            GameLog.ScriptingError("RequestDialog: {user} - sequence (first argument) was null or empty", User.Name);
            return false;
        }

        DialogSequence sequenceObj = null;
        VisibleObject invokerObj = null;

        if (Game.World.TryGetActiveUser(invoker, out User user))
            invokerObj = user as VisibleObject;
        else if (Game.World.WorldData.TryGetValue<Merchant>(invoker, out Merchant merchant))
            invokerObj = merchant as VisibleObject;

        if (invokerObj != null)
            invokerObj.SequenceCatalog.TryGetValue(sequence, out sequenceObj);

        if (sequenceObj == null)
            // Try global catalog
            Game.World.GlobalSequences.TryGetValue(sequence, out sequenceObj);

        if (invokerObj != null && sequenceObj != null)
            return Game.World.TryAsyncDialog(invokerObj, User, sequenceObj);

        GameLog.ScriptingWarning("RequestDialog: {user} - invoker {invoker} or sequence {sequence} not found", user, invoker, sequence);
        return false;
    }
    /// <summary>
    /// Set a session cookie. A cookie is a key-value pair with a dynamic value (of any type) associated to a given name (a string key). NPCs and other scripting functionality can 
    /// use this to store independent state to track quest progress / etc. Session cookies are deleted when a player is logged out.
    /// </summary>
    /// <param name="cookieName">Name of the cookie</param>
    /// <param name="value">Dynamic (any type) value to be stored with the given name.</param>
    public void SetSessionCookie(string cookieName, dynamic value)
    {
        if (string.IsNullOrEmpty(cookieName) || value is null)
        {
            GameLog.ScriptingError("SetSessionCookie: {user} - session cookie name (first argument) or value (second) was null or empty", User.Name);
            return;
        }
        try
        {
            if (value.GetType() == typeof(string))
                User.SetSessionCookie(cookieName, value);
            else
                User.SetSessionCookie(cookieName, value.ToString());
            GameLog.DebugFormat("{0} - set session cookie {1} to {2}", User.Name, cookieName, value);
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.ScriptingError("SetSessionCookie: {user}: value (second argument) could not be converted to string? {error}", User.Name, e);
        }
    }

    /// <summary>
    /// Set a cookie. A cookie is a key-value pair with a dynamic value (of any type) associated to a given name (a string key). NPCs and other scripting functionality can 
    /// use this to store independent state to track quest progress / etc. Cookies set by SetCookie are permanent.
    /// </summary>
    /// <param name="cookieName">Name of the cookie</param>
    /// <param name="value">Dynamic (any type) value to be stored with the given name.</param>
    public void SetCookie(string cookieName, dynamic value)
    {
        if (string.IsNullOrEmpty(cookieName) || value is null)
        {
            GameLog.ScriptingError("SetCookie: {user} - session cookie name (first argument) or value (second) was null or empty", User.Name);
        }
        try
        {
            if (value.GetType() == typeof(string))
                User.SetCookie(cookieName, value);
            else
                User.SetCookie(cookieName, value.ToString());
            GameLog.DebugFormat("{0} - set cookie {1} to {2}", User.Name, cookieName, value);
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.ScriptingError("SetCookie: {user} - value (second argument) could not be converted to string? {exception}", User.Name, e.ToString());
        }

    }

    /// <summary>
    /// Get the value of a session cookie, if it exists.
    /// </summary>
    /// <param name="cookieName">The name of the cookie to fetch</param>
    /// <returns>string representation of the cookie value</returns>
    public string GetSessionCookie(string cookieName)
    {
        if (string.IsNullOrEmpty(cookieName))
        {
            GameLog.ScriptingError("GetSessionCookie: {user} - cookie name (first argument) was null or empty - returning nil", User.Name);
            return null;
        }
        return User.GetSessionCookie(cookieName);
    }

    /// <summary>
    /// Get the value of a cookie, if it exists.
    /// </summary>
    /// <param name="cookieName">The name of the cookie to fetch</param>
    /// <returns>string representation of the cookie value</returns>
    public string GetCookie(string cookieName)
    {
        if (string.IsNullOrEmpty(cookieName))
        {
            GameLog.ScriptingError("GetCookie: {user} - cookie name (first argument) was null or empty - returning nil", User.Name);
            return null;
        }

        return User.GetCookie(cookieName);
    }
    /// <summary>
    /// Check to see if a player has a specified cookie or not.
    /// </summary>
    /// <param name="cookieName">Cookie name to check</param>
    /// <returns>Boolean indicating whether or not the named cookie exists</returns>
    public bool HasCookie(string cookieName)
    {
        if (string.IsNullOrEmpty(cookieName))
        {
            GameLog.ScriptingError("HasCookie: {user} - cookie name (first argument) was null or empty - returning false", User.Name);
            return false;
        }

        return User.HasCookie(cookieName);
    }
    /// <summary>
    /// Check to see if a player has a specified session cookie or not.
    /// </summary>
    /// <param name="cookieName">Cookie name to check</param>
    /// <returns>Boolean indicating whether or not the named cookie exists</returns>
    public bool HasSessionCookie(string cookieName)
    {
        if (string.IsNullOrEmpty(cookieName))
        {
            GameLog.ScriptingError("HasSessionCookie: {user} - cookie name (first argument) was null or empty - returning false", User.Name);
            return false;
        }

        return User.HasSessionCookie(cookieName);
    }

    /// <summary>
    /// Permanently remove a cookie from a player.
    /// </summary>
    /// <param name="cookieName">The name of the cookie to be deleted.</param>
    /// <returns></returns>
    public bool DeleteCookie(string cookieName)
    {
        if (string.IsNullOrEmpty(cookieName))
        {
            GameLog.ScriptingError("DeleteCookie: {user} cookie name (first argument) was null or empty - returning false", User.Name);
            return false;
        }
        return User.DeleteCookie(cookieName);
    }
    /// <summary>
    /// Permanently remove a session cookie from a player.
    /// </summary>
    /// <param name="cookieName">The name of the cookie to be deleted.</param>
    /// <returns></returns>
    public bool DeleteSessionCookie(string cookieName)
    {
        if (string.IsNullOrEmpty(cookieName))
        {
            GameLog.ScriptingError("DeleteSessionCookie: {user} cookie name (first argument) was null or empty - returning false", User.Name);
            return false;
        }

        return User.DeleteSessionCookie(cookieName);
    }

    /// <summary>
    /// Display a special effect visible to players.
    /// </summary>
    /// <param name="effect">ushort id of effect (references client datfile)</param>
    /// <param name="speed">speed of the effect (generally 100)</param>
    /// <param name="global">boolean indicating whether or not other players can see the effect, or just the player displaying the effect</param>
    public void DisplayEffect(ushort effect, short speed = 100, bool global = true)
    {
        if (!global)
            User.SendEffect(User.Id, effect, speed);
        else
            User.Effect(effect, speed);
    }

    /// <summary>
    /// Display an effect at a given x,y coordinate on the current player's map.
    /// </summary>
    /// <param name="x">X coordinate where effect will be displayed</param>
    /// <param name="y">Y coordinate where effect will be displayed</param>
    /// <param name="effect">ushort id of effect (references client datfile)</param>
    /// <param name="speed">speed of the effect (generally 100)</param>
    /// <param name="global">boolean indicating whether or not other players can see the effect, or just the player displaying the effect</param>
    public void DisplayEffectAtCoords(short x, short y, ushort effect, short speed = 100, bool global = true)
    {
        if (!global)
            User.SendEffect(x, y, effect, speed);
        else
            User.Effect(x, y, effect, speed);
    }
    /// <summary>
    /// Display a motion on the user
    /// </summary>
    /// <param name="motionId">the motion to display</param>
    /// <param name="speed">speed of the diplayed motion</param>
    public void DisplayMotion(int motionId, short speed = 20)
    {
        User.Motion((byte)motionId, speed);
    }

    /// <summary>
    /// Teleport the player to an x,y coordinate location on the specified map.
    /// </summary>
    /// <param name="location">The map name</param>
    /// <param name="x">X coordinate target</param>
    /// <param name="y">Y coordinate target</param>
    public void Teleport(string location, int x, int y)
    {
        if (string.IsNullOrEmpty(location))
        {
            GameLog.ScriptingError("Teleport: {user} - location name (first argument) was null or empty - aborting for safety", User.Name);
            return;
        }
        User.Teleport(location, (byte)x, (byte)y);
    }

    /// <summary>
    /// Play a sound effect.
    /// </summary>
    /// <param name="sound">byte id of the sound, referencing a sound effect in client datfiles.</param>
    public void SoundEffect(byte sound)
    {
        User.SendSound(sound);
    }

    /// <summary>
    /// Heal a player to full HP.
    /// </summary>
    public void HealToFull()
    {
        User.Heal(User.Stats.MaximumHp);
    }

    /// <summary>
    /// Heal a player for the specified amount of HP.
    /// </summary>
    /// <param name="heal">Integer amount of HP to be restored.</param>
    public void Heal(int heal)
    {
        User.Heal(heal);
    }

    /// <summary>
    /// Deal damage to the current player.
    /// </summary>
    /// <param name="damage">Integer amount of damage to deal.</param>
    /// <param name="element">Element of the damage (e.g. fire, air)</param>
    /// <param name="damageType">Type of damage (direct, magical, etc)</param>
    public void Damage(int damage, Xml.ElementType element = Xml.ElementType.None,
        Xml.DamageType damageType = Xml.DamageType.Direct)
    {
        User.Damage(damage, element, damageType);
    }

    /// <summary>
    /// Deal physical (direct) damage to the current player.
    /// </summary>
    /// <param name="damage">Integer amount of damage to deal.</param>
    /// <param name="fatal">Whether or not the damage should kill the player. If false, damage > current HP is reduced to (hp-1).</param>
    public void Damage(int damage, bool fatal=true)
    {
        if (fatal)
            User.Damage(damage, Xml.ElementType.None, Xml.DamageType.Direct, Xml.DamageFlags.Nonlethal);
        else
            User.Damage(damage, Xml.ElementType.None, Xml.DamageType.Direct);

    }

    /// <summary>
    /// Give an instance of an item to a player.
    /// </summary>
    /// <param name="obj">HybrasylWorldObject, representing an item existing in the world, to give to the player.</param>
    /// <returns>Boolean indicating whether or not it was successful (player may have full inventory, etc)</returns>
    public bool GiveItem(HybrasylWorldObject obj)
    {
        if (obj.Obj is ItemObject io)
            return User.AddItem(io.Name, 1);
        else
        {
            GameLog.ScriptingError("GiveItem: {user} - object (first argument) was either null, or not an item", User.Name);
        }
        return false;
    }

    /// <summary>
    /// Check to see if a user has the specified cookie; if not, set it, give experience, and optionally, send them a system message.
    /// </summary>
    /// <param name="cookie">Name of the cookie to be set.</param>
    /// <param name="xp">Amount of XP to award.</param>
    /// <param name="completionMessage">A system message that will be sent to the user.</param>
    /// <returns>Boolean indicating whether or not the user was awarded XP.</returns>
    public bool CompletionAward(string cookie, uint xp = 0, string completionMessage = null)
    {
        if (string.IsNullOrEmpty(cookie))
        {
            GameLog.ScriptingError("CompletionAward: {user} - cookie name (first parameter) cannot be null or empty - returning false", User.Name);
            return false;
        }
        if (User.HasCookie(cookie))
            return false;
        User.SetCookie(cookie, new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds().ToString());
        if (xp > 0)
            User.GiveExperience(xp);
        if (!string.IsNullOrEmpty(completionMessage))
            User.SendSystemMessage(completionMessage);
        return true;
    }

    /// <summary>
    /// Give a new instance of the named item to a player, optionally with a specified quantity.
    /// </summary>
    /// <param name="name">The name of the item to be created.</param>
    /// <param name="count">The count (stack) of the item to be created.</param>
    /// <returns>Boolean indicating whether or not it was successful (player may have full inventory, etc)</returns>
    public bool GiveItem(string name, int count = 1)
    {
        if (string.IsNullOrEmpty(name))
        {
            GameLog.ScriptingError("GiveItem: {user}: item name (first parameter) was null or empty - returning false", User.Name);
            return false;
        }
        // Does the item exist?
        if (Game.World.WorldData.TryGetValueByIndex(name, out Xml.Item template))
        {
            if (template.Stackable)
            {
                var item = Game.World.CreateItem(template.Id);
                if (count >= 1)
                    item.Count = count > item.MaximumStack ? item.MaximumStack : count;
                else
                    item.Count = item.MaximumStack;
                Game.World.Insert(item);
                User.AddItem(item);
                return true;
            }
            else
            {
                var success = true;
                // Actually add N of the item. Note that if the user's inventory is full, or
                // becomes full, the items will drop to the ground.
                for (var i = 0; i < count; i++)
                {
                    var item = Game.World.CreateItem(template.Id);
                    Game.World.Insert(item);
                    success = success && User.AddItem(item);                       
                }
                return success;
            }
        }
        GameLog.ScriptingError("GiveItem: {user} - item name {name} could not be found", User.Name, name);
        return false;
    }

    /// <summary>
    /// Check to see if a player has an item, optionally with a specified quantity.
    /// </summary>
    /// <param name="name">The name of the item to check.</param>
    /// <param name="count">The quantity that will be checked.</param>
    /// <returns></returns>
    public bool HasItem(string name, int count = 1)
    {
        if (string.IsNullOrEmpty(name))
        {
            GameLog.ScriptingError("HasItem: {user} - item name (first parameter) was null or empty - returning false", User.Name);
            return false;
        }
        if (count == 1)
            return User.Inventory.ContainsName(name);
        return User.Inventory.ContainsId(name, count);
    }

    /// <summary>
    /// Take an item with a given name and an optional quantity from the current player's inventory.
    /// </summary>
    /// <param name="name">The name of the item to be removed.</param>
    /// <param name="count">The quantity to be removed.</param>
    /// <returns>Boolean indicating whether or not it the item was successfully removed from the player's inventory.</returns>
    public bool TakeItem(string name, int count = 1)
    {
        if (string.IsNullOrEmpty(name))
        {
            GameLog.ScriptingError("TakeItem: {user} - item name (first parameter) was null or empty - returning false", User.Name);
            return false;
        }

        if (User.Inventory.ContainsName(name))
        {
            if (User.RemoveItem(name, (ushort)count))
                return true;
            else
                GameLog.ScriptingWarning("TakeItem: {user} - failed for {item}", User.Name, name);
        }
        else
            GameLog.ScriptingWarning("TakeItem: {user} doesn't have {item}", User.Name, name);

        return false;
    }

    /// <summary>
    /// Give experience to the current player.
    /// </summary>
    /// <param name="exp">Integer amount of experience to be awarded.</param>
    /// <returns>true</returns>
    public bool GiveExperience(int exp)
    {
        User.GiveExperience((uint)exp);
        return true;
    }

    /// <summary>
    /// Take experience from the current player.
    /// </summary>
    /// <param name="exp">Integer amount of experience to be deducted.</param>
    /// <returns>Whether or not the experience was removed (if the requested amount exceeds total experience, none will be removed).</returns>
    public bool TakeExperience(int exp)
    {
        if ((uint)exp > User.Stats.Experience)
            return false;
        User.Stats.Experience -= (uint)exp;
        SystemMessage($"Your world spins as your insight leaves you ((-{exp} experience!))");
        User.UpdateAttributes(StatUpdateFlags.Experience);
        return true;
    }

    /// <summary>
    /// Add a given skill to a player's skillbook.
    /// </summary>
    /// <param name="skillname">The name of the skill to be added.</param>
    /// <returns>Boolean indicating success</returns>
    public bool AddSkill(string skillname)
    {
        if (string.IsNullOrEmpty(skillname))
            GameLog.ScriptingError("AddSkill: {user} - skill name (first argument) cannot be null or empty");
        else if (Game.World.WorldData.TryGetValueByIndex(skillname, out Xml.Castable result))
        {
            User.AddSkill(result);
            return true;
        }
        else
            GameLog.ScriptingError("AddSkill: {user} - skill {skill} not found", User.Name, skillname);
        return false;
    }


    /// <summary>
    /// Check to see if the specified skill exists in the user's skill book.
    /// </summary>
    /// <param name="skillname">Name of the skill to find.</param>
    /// <returns>Boolean indicating whether or not the user knows the skill.</returns>
    public bool HasSkill(string skillname)
    {
        if (string.IsNullOrEmpty(skillname))
            GameLog.ScriptingError("HasSkill: {user} - skill name (first argument) cannot be null or empty");
        else if (Game.World.WorldData.TryGetValueByIndex(skillname, out Xml.Castable result))
            return User.SkillBook.Contains(result.Id);
        else
            GameLog.ScriptingError("HasSkill: {user} - skill {skill} not found", User.Name, skillname);
        return false;

    }

    /// <summary>
    /// Add a given spell to a player's spellbook.
    /// </summary>
    /// <param name="spellname">The name of the spell to be added.</param>
    /// <returns>Boolean indicating success</returns>
    public bool AddSpell(string spellname)
    {
        if (string.IsNullOrEmpty(spellname))
            GameLog.ScriptingError("AddSpell: {user} - spell name (first argument) cannot be null or empty");
        else if (Game.World.WorldData.TryGetValueByIndex(spellname, out Xml.Castable result))
        {
            User.AddSpell(result);
            return true;
        }
        else
            GameLog.ScriptingError("AddSpell: {user} - spell {spell} not found", User.Name, spellname);
        return false;
    }

    /// <summary>
    /// Check to see if the specified spell exists in the user's spell book.
    /// </summary>
    /// <param name="skillname">Name of the spell to find.</param>
    /// <returns>Boolean indicating whether or not the user knows the spell.</returns>
    public bool HasSpell(string spellname)
    {
        if (string.IsNullOrEmpty(spellname))
            GameLog.ScriptingError("HasSpell: {user} - spell name (first argument) cannot be null or empty");
        else if (Game.World.WorldData.TryGetValueByIndex(spellname, out Xml.Castable result))
            return User.SpellBook.Contains(result.Id);
        else
            GameLog.ScriptingError("HasSpell: {user} - spell {spell} not found", User.Name, spellname);
        return false;

    }

    /// <summary>
    /// Send a system message ("orange message") to the current player.
    /// </summary>
    /// <param name="message"></param>
    public void SystemMessage(string message)
    {
        // This is a typical client "orange message"
        User.SendMessage(message, Hybrasyl.MessageTypes.SYSTEM_WITH_OVERHEAD);
    }

    /// <summary>
    /// Indicates whether the current player is a peasant, or has an assigned class.
    /// </summary>
    /// <returns>Boolean indicating whether or not current player is a peasant.</returns>
    public bool IsPeasant() => User.Class == Xml.Class.Peasant;

    /// <summary>
    /// Indicates whether the current player is in a guild.
    /// </summary>
    /// <returns>Boolean indicating whether or not current player is in a guild.</returns>
    public bool IsInGuild() => User.GuildGuid != Guid.Empty;
        
    /// <summary>
    /// Sends a whisper ("blue message") from a given name to the current player.
    /// </summary>
    /// <param name="name">The name to be used for the whisper (e.g. who it is from)</param>
    /// <param name="message">The message.</param>
    public void Whisper(string name, string message) =>
        User.SendWhisper(name, message);

    /// <summary>
    /// Say something as the user.
    /// </summary>
    /// <param name="message">The message to speak aloud.</param>
    public void Say(string message) => User.Say(message);

    /// <summary>
    /// Shout something as the user.
    /// </summary>
    /// <param name="message">The message to shout.</param>
    public void Shout(string message) => User.Shout(message);

    public void SendMessage(string message, int type) => User.SendMessage(message, (byte)type);

    /// <summary>
    /// Sends an in-game mail to the current player. NOT TESTED.
    /// </summary>
    /// <param name="name">The name to be used for the mail sender (who it is from)</param>
    /// <param name="subject">The message.</param>
    /// <param name="message">The message.</param>
    public void Mail(string name, string subject, string message)
    {
        GameLog.ScriptingFatal("Mail: not currently implemented");
    }

    /// Close any active dialogs for the current player.
    /// </summary>
    public void EndDialog()
    {
        User.DialogState.EndDialog();
        User.SendCloseDialog();
        //GameLog.Info("Dialog: closed by script");
    }



    /// <summary>
    /// Start a dialog sequence for the current player. This will display the first dialog in the sequence to the player.
    /// </summary>
    /// <param name="sequenceName">The name of the sequence to start</param>
    /// <param name="associateOverride">An object to associate with the dialog as the invokee.</param>
    // The use of dynamic here is a temporary offense before god
    public void StartSequence(string sequenceName, dynamic associateOverride = null)
    {
        if (sequenceName == null)
        {
            GameLog.ScriptingError("StartSequence: {user} - sequence name (first argument) cannot be null or empty", User.Name);
            return;
        }
        DialogSequence sequence = null;
        VisibleObject associate = null;
        GameLog.DebugFormat("{0} starting sequence {1}", User.Name, sequenceName);

        // First: is this a global sequence?
        Game.World.GlobalSequences.TryGetValue(sequenceName, out sequence);

        // Next: what object are we associated with?
        if (associateOverride == null)
        {
            if (User.DialogState.Associate != null)
                associate = User.DialogState.Associate as VisibleObject;
            else if (User.LastAssociate != null)
                associate = User.LastAssociate;
        }
        else
            associate = associateOverride.Obj as VisibleObject;

        // If we didn't get a sequence before, try with our associate
        if (sequence == null && associate != null)
            associate.SequenceCatalog.TryGetValue(sequenceName, out sequence);

        // We should hopefully have a sequence now...
        if (sequence == null)
        {
            GameLog.ScriptingError("StartSequence: {user} - called from {associate}: sequence name {seq} cannot be found!",
                User.Name, associate?.Name ?? "globalsequence", sequenceName);
            // To be safe, terminate all dialog state
            User.DialogState.EndDialog();
            // If the user was previously talking to a merchant, and we can't find a sequence,
            // simply display the main menu again. If it's a reactor....oh well.
            if (associate is Merchant)
                associate.DisplayPursuits(User);
            return;
        }

        // If we're here, sequence should now be our target sequence, 
        // let's end the current state and start a new one
        if (User.DialogState.InDialog)
        {
            // Transition between current and new dialog
            User.DialogState.TransitionDialog(associate, sequence);
            //GameLog.Info($"Transitioning between dialog {User.DialogState.PreviousPursuitId} and {User.DialogState.CurrentPursuitId}");
            //GameLog.Info($"Transition Dialog NPC associate {associate?.Name ?? "null"}");
        }
        else
        {
            // Start a new dialog
            User.DialogState.StartDialog(associate, sequence);
            //GameLog.Info($"StartDialog Dialog NPC associate {associate?.Name ?? "null"}");
        }

        // Lastly, show the new dialog
        User.DialogState.ActiveDialog.ShowTo(User, associate);

    }

    /// <summary>
    /// Calculate the Manhattan distance (distance between two points measured along axes at right angles) 
    /// between the current player and a target object.
    /// </summary>
    /// <param name="target">The target object</param>
    /// <returns>The numeric distance</returns>
    public int Distance(HybrasylWorldObject target) => User.Distance(target.Obj);

    /// <summary>
    /// Set a user's hairstyle from a script
    /// </summary>
    /// <param name="hairStyle">The target hairstyle</param>
    public void SetHairstyle(int hairStyle)
    {
        User.SetHairstyle((ushort)hairStyle);
    }

    /// <summary>
    /// Set's a user's haircolor from a script
    /// </summary>
    /// <param name="itemColor">The color to apply</param>
    public void SetHairColor(string itemColor)
    {
        Xml.ItemColor color = (Xml.ItemColor)Enum.Parse(typeof(Xml.ItemColor), itemColor);
        User.SetHairColor(color);
    }
}