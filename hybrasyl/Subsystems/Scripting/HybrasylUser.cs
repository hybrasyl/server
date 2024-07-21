// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Statuses;
using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using Reactor = Hybrasyl.Objects.Reactor;

namespace Hybrasyl.Subsystems.Scripting;

[MoonSharpUserData]
public class HybrasylUser : HybrasylWorldObject
{
    public HybrasylUser(User user) : base(user)
    {
        World = new HybrasylWorld(user.World);
        Map = new HybrasylMap(user.Map);
    }

    internal User User => WorldObject as User;
    internal HybrasylWorld World { get; set; }
    public HybrasylMap Map { get; set; }
    public new string Guid => User.Guid.ToString();
    public Direction Direction => User.Direction;


    /// <summary>
    ///     The item in the first inventory slot of the player.
    /// </summary>
    public HybrasylItemObject FirstInventorySlot
    {
        get
        {
            var f = User.Inventory[1];
            var hio = new HybrasylItemObject(f);
            return f is null ? null : hio;
        }
    }

    public Class Class => User.Class;

    public string MapName => User.Map?.Name ?? "Unknown Kadath";

    /// <summary>
    ///     The user's previous class, if a subpath.
    /// </summary>
    public Class PreviousClass => User.PreviousClass;

    public override bool IsPlayer => true;

    public bool IsPrivileged => User.AuthInfo.IsPrivileged;

    /// <summary>
    ///     The gender of the player. For Darkages purpose, this will evaluate to Male or Female.
    /// </summary>
    public Gender Gender => User.Gender;

    /// <summary>
    ///     The current level of the user. Client supports up to level 255; Hybrasyl has the same level cap as usda, 99.
    /// </summary>
    public int Level => User.Stats.Level;

    /// <summary>
    ///     Amount of gold the user currently has.
    /// </summary>

    public uint Gold => User.Stats.Gold;

    /// <summary>
    ///     Access the StatInfo of the specified user directly (all stats).
    /// </summary>
    public StatInfo Stats => User.Stats;

    /// <summary>
    ///     Whether the user is alive or not.
    /// </summary>
    public bool Alive => User.Condition.Alive;

    public ushort WeaponSmallDamage => User.WeaponSmallDamage;

    /// <summary>
    ///     Give the specified amount of gold to the user.
    /// </summary>
    /// <param name="gold">Amount of gold to give.</param>
    /// <returns></returns>
    public bool AddGold(uint gold) => User.AddGold(gold);

    /// <summary>
    ///     Take the specified amount of gold from the user.
    /// </summary>
    /// <param name="gold">Amount of gold to take.</param>
    /// <returns></returns>
    public bool RemoveGold(uint gold) => User.RemoveGold(gold);

    /// <summary>
    ///     Removes a skill from the user's skillbook
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
    ///     Removes a spell from the user's spellbook
    /// </summary>
    /// <param name="name">Spell to be removed</param>
    /// <returns>boolean indicating success</returns>
    public bool RemoveSpell(string name)
    {
        var slot = User.SpellBook.SlotOf(name);
        if (!User.SpellBook.Remove(slot)) return false;
        User.SendClearSpell(slot);
        return true;
    }

    public string GetNation() => User.Nation?.Name ?? string.Empty;

    public bool SetNation(string nationName) => User.ChangeCitizenship(nationName);

    /// <summary>
    ///     Get a list of objects in the viewport of the player. This represents all visible objects (items, players,
    ///     creatures) contained in the client's viewport (the drawable map area).
    /// </summary>
    /// <returns>A list of <see cref="HybrasylWorldObject" /> objects in the player's viewport.</returns>
    public List<HybrasylWorldObject> GetViewportObjects() => new();

    /// <summary>
    ///     Get a list of players in the viewport of the player. This represents only players contained in the client's
    ///     viewport (the drawable map area).
    /// </summary>
    /// <returns>A list of <see cref="HybrasylUser" /> objects in the player's viewport.</returns>
    public List<HybrasylUser> GetViewportPlayers() => new();

    /// <summary>
    ///     Resurrect a user. They respawn in their home map with 1HP/1MP and with scars, if configured in the death handler.
    /// </summary>
    public void Resurrect()
    {
        if (!User.Condition.Alive)
            User.Resurrect();
    }

    /// <summary>
    ///     Get the player, if any, that the current player is facing ("looking at").
    /// </summary>
    /// <returns>
    ///     <see cref="HybrasylUser" /> object for the player facing this player, or nil, if the player isn't directly facing
    ///     another
    ///     player.
    /// </returns>
    public HybrasylUser GetFacingUser()
    {
        var facing = User.GetFacingUser();
        return facing != null ? new HybrasylUser(facing) : null;
    }

    /// <summary>
    ///     Get the objects a player is facing (for instance, items on the ground in front of the player)
    /// </summary>
    /// <param name="distance">Maximum distance to consider in front of the user.</param>
    /// <returns>A list of <see cref="HybrasylWorldObject" /> objects facing the player.</returns>
    public List<HybrasylWorldObject> GetFacingObjects(int distance = 1)
    {
        return User.GetFacingObjects(distance).Select(selector: item => new HybrasylWorldObject(item)).ToList();
    }

    /// <summary>
    ///     Get the monster that the player is facing.
    /// </summary>
    /// <returns>A <see cref="HybrasylMonster" /> object.</returns>
    public HybrasylMonster GetFacingMonster()
    {
        var facing = (Monster) User.GetFacingObjects().FirstOrDefault(predicate: x => x is Monster);
        return facing != null ? new HybrasylMonster(facing) : null;
    }

    /// <summary>
    ///     Get a list of monsters facing the user.
    /// </summary>
    /// #2
    /// <param name="distance">The maximum distance to consider from the user.</param>
    /// <returns>A list of <see cref="HybrasylMonster" /> objects facing the user.</returns>
    public List<HybrasylMonster> GetFacingMonsters(int distance = 1)
    {
        return User.GetFacingObjects(distance).Where(predicate: x => x is Monster).Cast<Monster>()
            .Select(selector: y => new HybrasylMonster(y)).ToList();
    }

    /// <summary>
    ///     Get a list of players facing the user.
    /// </summary>
    /// #2
    /// <param name="distance">The maximum distance to consider from the user.</param>
    /// <returns>A list of <see cref="HybrasylUser" /> objects facing the user.</returns>
    public List<HybrasylUser> GetFacingUsers(int distance = 1)
    {
        return User.GetFacingObjects(distance).Where(predicate: x => x is User).Cast<User>()
            .Select(selector: y => new HybrasylUser(y))
            .ToList();
    }

    /// <summary>
    ///     Return a list of monsters in a specified direction.
    /// </summary>
    /// <param name="direction">The <see cref="Direction" /> to examine.</param>
    /// <param name="radius">The number of tiles to examine in the given direction.</param>
    /// <returns></returns>
    public List<HybrasylMonster> GetMonstersInDirection(Direction direction, int radius = 1) =>
        User.GetDirectionalTargets(direction).Where(predicate: x => x is Monster).Cast<Monster>()
            .Select(selector: y => new HybrasylMonster(y)).ToList();

    /// <summary>
    ///     Return a list of players in a specified direction.
    /// </summary>
    /// <param name="direction">The <see cref="Direction" /> to examine.</param>
    /// <param name="radius">The number of tiles to examine in the given direction.</param>
    /// <returns>List of <see cref="HybrasylPlayer" />. </returns>
    public List<HybrasylUser> GetUsersInDirection(Direction direction, int radius = 1) =>
        User.GetDirectionalTargets(direction).Where(predicate: x => x is User).Cast<User>()
            .Select(selector: y => new HybrasylUser(y)).ToList();

    /// <summary>
    ///     Get the coordinates of a tile in a specific direction and number of tiles away from current location.
    /// </summary>
    /// <param name="direction">The <see cref="Direction" /> to use.</param>
    /// <param name="tiles">The number of tiles away from the current location in the specified direction.</param>
    /// <returns>A <see cref="Coordinate" /> representing the X,Y of the calculated tile.</returns>
    public Coordinate GetTileInDirection(Direction direction, int tiles = 1)
    {
        int x = X;
        int y = Y;
        switch (direction)
        {
            case Direction.North:
                x = X;
                y = Y - tiles;
                break;
            case Direction.South:
                x = X;
                y = Y + tiles;
                break;
            case Direction.West:
                x = X - tiles;
                y = Y;
                break;
            case Direction.East:
                x = X + tiles;
                y = Y;
                break;
        }

        return Coordinate.FromInt(x, y);
    }

    /// <summary>
    ///     Set the users facing direction.
    /// </summary>
    /// <param name="direction">A cardinal direction (north, south, east, west).</param>
    public void ChangeDirection(string direction)
    {
        Enum.TryParse(typeof(Direction), direction, out var result);
        User.Direction = (Direction) result;
    }

    /// <summary>
    ///     End coma state (e.g. beothaich was used)
    /// </summary>
    public void EndComa()
    {
        User.EndComa();
    }

    /// <summary>
    ///     Teleport a user to their "home" (ordinary spawnpoint), or a map of last resort.
    /// </summary>
    public void SendHome()
    {
        if (User.Nation.SpawnPoints.Count != 0)
        {
            var spawnpoint = User.Nation.RandomSpawnPoint;
            if (spawnpoint != null)
            {
                User.Teleport(spawnpoint.MapName, spawnpoint.X, spawnpoint.Y);
                return;
            }
        }

        // Fallback to something if we have no spawnpoints
        User.Teleport(500, 50, 50);
    }

    /// <summary>
    ///     Get a legend mark from the current player's legend (a list of player achievements and accomplishments which is
    ///     visible by anyone in the world), given a legend
    ///     prefix. All legend marks have invisible prefixes (keys) for editing / storage capabilities.
    /// </summary>
    /// <param name="prefix">The prefix we want to retrieve (legend key)</param>
    /// <returns></returns>
    public dynamic GetLegendMark(string prefix)
    {
        LegendMark mark;
        return User.Legend.TryGetMark(prefix, out mark) ? mark : (object) null;
    }

    /// <summary>
    ///     Check to see if a player has a legend mark with the specified prefix in their legend.
    /// </summary>
    /// <param name="prefix">Prefix of the mark to check</param>
    /// <returns>boolean</returns>
    public bool HasLegendMark(string prefix) => User.Legend.TryGetMark(prefix, out var _);

    /// <summary>
    ///     Check to see if the player has an item equipped with the specified name.
    /// </summary>
    /// <param name="item"></param>
    /// <returns>boolean</returns>
    public bool HasEquipment(string item) => User.Equipment.ContainsId(item);

    /// <summary>
    ///     Change the class of a player to a new class. The player's class will immediately change and they will receive a
    ///     legend mark that
    ///     reads "newClass by oath of oathGiver, XXX".
    /// </summary>
    /// <param name="newClass">
    ///     The player's new class./param>
    ///     <param name="oathGiver">The name of the NPC or player who gave oath for this class change.</param>
    public void ChangeClass(Class newClass, string oathGiver)
    {
        User.Class = newClass;
        User.UpdateAttributes(StatUpdateFlags.Full);
        LegendIcon icon;
        string legendtext;
        // this is annoying af
        switch (newClass)
        {
            case Class.Monk:
                icon = LegendIcon.Monk;
                legendtext = $"Monk by oath of {oathGiver}";
                break;
            case Class.Priest:
                icon = LegendIcon.Priest;
                legendtext = $"Priest by oath of {oathGiver}";
                break;
            case Class.Rogue:
                icon = LegendIcon.Rogue;
                legendtext = $"Rogue by oath of {oathGiver}";
                break;
            case Class.Warrior:
                icon = LegendIcon.Warrior;
                legendtext = $"Warrior by oath of {oathGiver}";
                break;
            case Class.Wizard:
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
    ///     Generate a list of reactors in the current user's viewport.
    /// </summary>
    /// <returns>List of <see cref="HybrasylReactor" /> objects in the player's viewport.</returns>
    public List<HybrasylReactor> GetReactorsInViewport()
    {
        return User.Map.EntityTree.GetObjects(User.GetViewport()).Where(predicate: x => x is Reactor)
            .Select(selector: r => new HybrasylReactor(r as Reactor)).ToList();
    }

    /// <summary>
    ///     Check to see whether the user has killed a named monster, optionally in the last n minutes.
    /// </summary>
    /// <param name="name">The name of the monster to check</param>
    /// <param name="since">Specify that the kills should have occurred after the given Unix timestamp.</param>
    /// <param name="quantity">Specify that a certain number of kills should have occurred.</param>
    /// <returns>Boolean indicating whether the specified requirements have been met.</returns>
    public bool HasKilledSince(string name, int since, int quantity = 1)
    {
        var matches = User.RecentKills.Where(predicate: z =>
            string.Equals(z.Name, name, StringComparison.CurrentCultureIgnoreCase));
        matches = matches.Where(predicate: y =>
            y.Timestamp.ToUniversalTime() >= DateTimeOffset.FromUnixTimeSeconds(since).UtcDateTime);
        return matches.Count() >= quantity;
    }

    /// <summary>
    ///     Return the number of named monsters the player has killed since a specific timestamp.
    /// </summary>
    /// <param name="name">The name of the monster to check</param>
    /// <param name="since">Specify that the kills should have occurred after the given Unix timestamp.</param>
    /// <returns>Number of named monsters killed.</returns>
    public int NumberKilled(string name, int since = 0)
    {
        var matches = User.RecentKills
            .Where(predicate: x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));
        return since > 0
            ? matches.Count(predicate: x =>
                x.Timestamp.ToUniversalTime() >= DateTimeOffset.FromUnixTimeSeconds(since).UtcDateTime)
            : matches.Count();
    }

    /// <summary>
    ///     Check to see whether the user has killed a quantity of a named monster, optionally in the last n minutes.
    /// </summary>
    /// <param name="name">The name of the monster to check</param>
    /// <param name="quantity">Specify that a certain number of kills should have occurred. Default is 1.</param>
    /// <param name="minutes">The number of minutes to limit the check. 0 is default and means no limit.</param>
    /// <returns>Boolean indicating whether or not the specified requirements were met.</returns>
    public bool HasKilled(string name, int quantity = 1, int minutes = 0)
    {
        var matches = User.RecentKills.Where(predicate: x =>
            string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));

        return minutes > 0
            ? matches.Count(predicate: x =>
                x.Timestamp.ToUniversalTime() >=
                (DateTime.Now.ToUniversalTime() - TimeSpan.FromMinutes(minutes)).ToUniversalTime()) >= quantity
            : matches.Count() >= quantity;
    }

    /// <summary>
    ///     Return the player's entire legend.
    /// </summary>
    /// <returns>A <see cref="Legend" /> object representing the player's legend. </returns>
    public Legend GetLegend() => User.Legend;

    /// <summary>
    ///     Add a legend mark with the specified icon, color, text, and prefix to a player's legend, which will default to
    ///     being issued now (current in-game time).
    /// </summary>
    /// <param name="icon">A <see cref="LegendIcon" /> enum indicating the icon to be used for the mark (heart, sword, etc)</param>
    /// <param name="color">
    ///     A <see cref="LegendColor" /> indicating the color the mark will be rendered in (blue, yellow,
    ///     orange, etc)
    /// </param>
    /// <param name="text">The actual text of the legend mark.</param>
    /// <param name="prefix">
    ///     An invisible key (stored in the beginning of the mark) that can be used to refer to the mark
    ///     later.
    /// </param>
    /// <param name="isPublic">
    ///     Whether or not this legend mark can be seen by other players. By convention, private marks are
    ///     prefixed with " - ".
    /// </param>
    /// <param name="quantity">
    ///     Quantity of the legend mark. For instance "Mentored Dude (2)". Also by convention, quantity is
    ///     expressed in parenthesis at the end of the mark.
    /// </param>
    /// <param name="displaySeason">Whether or not to display the season of a mark (e.g. Fall, Summer)</param>
    /// <param name="displayTimestamp">Whether or not to display the in-game time of a mark (e.g. Hybrasyl 5)</param>
    /// <returns>Boolean indicating success or failure.</returns>
    public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, string prefix = default,
        bool isPublic = true,
        int quantity = 0, bool displaySeason = true, bool displayTimestamp = true) =>
        AddLegendMark(icon, color, text, DateTime.Now, prefix, isPublic, quantity, displaySeason,
            displayTimestamp);

    /// <summary>
    ///     Add a legend mark with the specified icon, color, text, timestamp and prefix to a player's legend.
    /// </summary>
    /// <param name="icon">A <see cref="LegendIcon" /> enum indicating the icon to be used for the mark (heart, sword, etc)</param>
    /// <param name="color">
    ///     A <see cref="LegendColor" /> indicating the color the mark will be rendered in (blue, yellow,
    ///     orange, etc)
    /// </param>
    /// <param name="text">The actual text of the legend mark.</param>
    /// <param name="timestamp">The in-game time the legend was awarded.</param>
    /// <param name="prefix">
    ///     An invisible key (stored in the beginning of the mark) that can be used to refer to the mark
    ///     later.
    /// </param>
    /// <returns>Boolean indicating success or failure.</returns>
    public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, HybrasylTime timestamp, string prefix) =>
        User.Legend.AddMark(icon, color, text, timestamp.TerranDateTime, prefix);

    /// <summary>
    ///     Add a legend mark to a player's legend.
    /// </summary>
    /// <param name="icon">A <see cref="LegendIcon" /> enum indicating the icon to be used for the mark (heart, sword, etc)</param>
    /// <param name="color">
    ///     A <see cref="LegendColor" /> indicating the color the mark will be rendered in (blue, yellow,
    ///     orange, etc)
    /// </param>
    /// <param name="text">The actual text of the legend mark.</param>
    /// <param name="timestamp">The Terran time the legend was awarded.</param>
    /// <param name="prefix">
    ///     An invisible key (stored in the beginning of the mark) that can be used to refer to the mark
    ///     later.
    /// </param>
    /// <param name="isPublic">
    ///     Whether or not this legend mark can be seen by other players. By convention, private marks are
    ///     prefixed with " - ".
    /// </param>
    /// <param name="quantity">
    ///     Quantity of the legend mark. For instance "Mentored Dude (2)". Also by convention, quantity is
    ///     expressed in parenthesis at the end of the mark.
    /// </param>
    /// <param name="displaySeason">Whether or not to display the season of a mark (e.g. Fall, Summer)</param>
    /// <param name="displayTimestamp">Whether or not to display the in-game time of a mark (e.g. Hybrasyl 5)</param>
    /// <returns>Boolean indicating success or failure.</returns>
    public bool AddLegendMark(LegendIcon icon, LegendColor color, string text, DateTime timestamp,
        string prefix = default,
        bool isPublic = true, int quantity = 0, bool displaySeason = true, bool displayTimestamp = true)
    {
        try
        {
            return User.Legend.AddMark(icon, color, text, timestamp, prefix, isPublic, quantity, displaySeason,
                displayTimestamp);
        }
        catch (ArgumentException)
        {
            GameLog.ScriptingError("AddLegendMark: {user} - duplicate prefix {prefix}", User.Name, prefix);
        }

        return false;
    }

    /// <summary>
    ///     Remove the given legend mark from a player's legend.
    /// </summary>
    /// <param name="prefix">The prefix key of the legend mark to be removed.</param>
    /// <returns>Boolean indicating success or failure.</returns>
    public bool RemoveLegendMark(string prefix) => User.Legend.RemoveMark(prefix);

    /// <summary>
    ///     Modify a previously created legend mark. You can set a new quantity, or set an existing mark as public or private.
    /// </summary>
    /// <param name="prefix">Prefix key of the legend mark to be modified.</param>
    /// <param name="quantity">A quantity to be assigned to the mark.</param>
    /// <param name="isPublic">Whether or not the mark should be public or not.</param>
    /// <returns>Boolean indicating whether the mark for modification was found or not</returns>
    public bool ModifyLegendMark(string prefix, int quantity, bool isPublic)
    {
        if (!User.Legend.TryGetMark(prefix, out var mark)) return false;
        mark.Quantity = quantity;
        mark.Public = isPublic;
        return true;
    }

    /// <summary>
    ///     Increment a legend mark with a quantity.
    /// </summary>
    /// <param name="prefix">The legend mark prefix to modify.</param>
    /// <param name="preserveDate">Whether or not to preserve the date. If true, the date of the mark is not updated.</param>
    /// <returns>Boolean indicating whether the mark existed / was updated</returns>
    public bool IncrementLegendMark(string prefix, bool preserveDate = true)
    {
        if (!User.Legend.TryGetMark(prefix, out var mark))
            return false;
        mark.Quantity++;
        if (!preserveDate)
            mark.Timestamp = DateTime.Now;
        return true;
    }

    /// <summary>
    ///     Decrement a legend mark with a quantity.
    /// </summary>
    /// <param name="prefix">The legend mark prefix to modify.</param>
    /// <param name="preserveDate">Whether or not to preserve the date. If true, the date of the mark is not updated.</param>
    /// <returns>Boolean indicating whether the mark existed / was updated</returns>
    public bool DecrementLegendMark(string prefix, bool preserveDate = true)
    {
        if (!User.Legend.TryGetMark(prefix, out var mark))
            return false;
        mark.Quantity--;
        if (!preserveDate)
            mark.Timestamp = DateTime.Now;
        return true;
    }

    /// <summary>
    ///     Set a session cookie. A cookie is a key-value pair with a dynamic value (of any type) associated to a given name (a
    ///     string key). NPCs and other scripting functionality can
    ///     use this to store independent state to track quest progress / etc. Session cookies are deleted when a player is
    ///     logged out.
    /// </summary>
    /// <param name="cookieName">Name of the cookie</param>
    /// <param name="value">Dynamic (any type) value to be stored with the given name.</param>
    public void SetSessionCookie(string cookieName, dynamic value)
    {
        if (string.IsNullOrEmpty(cookieName) || value is null)
        {
            GameLog.ScriptingError(
                "SetSessionCookie: {user} - session cookie name (first argument) or value (second) was null or empty",
                User.Name);
            return;
        }

        try
        {
            User.SetSessionCookie(cookieName, value is string ? (string) value : (string) value.ToString());
            GameLog.DebugFormat("{0} - set session cookie {1} to {2}", User.Name, cookieName, value);
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.ScriptingError(
                "SetSessionCookie: {user}: value (second argument) could not be converted to string? {error}",
                User.Name, e);
        }
    }

    /// <summary>
    ///     Set a cookie. A cookie is a key-value pair with a dynamic value (of any type) associated to a given name (a string
    ///     key). NPCs and other scripting functionality can
    ///     use this to store independent state to track quest progress / etc. Cookies set by SetCookie are permanent.
    /// </summary>
    /// <param name="cookieName">Name of the cookie</param>
    /// <param name="value">Dynamic (any type) value to be stored with the given name.</param>
    public void SetCookie(string cookieName, dynamic value)
    {
        if (string.IsNullOrEmpty(cookieName) || value is null)
            GameLog.ScriptingError(
                "SetCookie: {user} - session cookie name (first argument) or value (second) was null or empty",
                User.Name);
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
            GameLog.ScriptingError(
                "SetCookie: {user} - value (second argument) could not be converted to string? {exception}", User.Name,
                e.ToString());
        }
    }

    /// <summary>
    ///     Get the value of a session cookie, if it exists.
    /// </summary>
    /// <param name="cookieName">The name of the cookie to fetch</param>
    /// <returns>string representation of the cookie value</returns>
    public string GetSessionCookie(string cookieName)
    {
        if (string.IsNullOrEmpty(cookieName))
        {
            GameLog.ScriptingError(
                "GetSessionCookie: {user} - cookie name (first argument) was null or empty - returning nil", User.Name);
            return null;
        }

        return User.GetSessionCookie(cookieName);
    }

    /// <summary>
    ///     Get the value of a cookie, if it exists.
    /// </summary>
    /// <param name="cookieName">The name of the cookie to fetch</param>
    /// <returns>string representation of the cookie value</returns>
    public string GetCookie(string cookieName)
    {
        if (!string.IsNullOrEmpty(cookieName)) return User.GetCookie(cookieName);
        GameLog.ScriptingError("GetCookie: {user} - cookie name (first argument) was null or empty - returning nil",
            User.Name);
        return null;
    }

    /// <summary>
    ///     Check to see if a player has a specified cookie or not.
    /// </summary>
    /// <param name="cookieName">Cookie name to check</param>
    /// <returns>Boolean indicating whether or not the named cookie exists</returns>
    public bool HasCookie(string cookieName)
    {
        if (!string.IsNullOrEmpty(cookieName)) return User.HasCookie(cookieName);
        GameLog.ScriptingError("HasCookie: {user} - cookie name (first argument) was null or empty - returning false",
            User.Name);
        return false;
    }

    /// <summary>
    ///     Check to see if a player has a specified session cookie or not.
    /// </summary>
    /// <param name="cookieName">Cookie name to check</param>
    /// <returns>Boolean indicating whether or not the named cookie exists</returns>
    public bool HasSessionCookie(string cookieName)
    {
        if (!string.IsNullOrEmpty(cookieName)) return User.HasSessionCookie(cookieName);
        GameLog.ScriptingError(
            "HasSessionCookie: {user} - cookie name (first argument) was null or empty - returning false", User.Name);
        return false;
    }

    /// <summary>
    ///     Permanently remove a cookie from a player.
    /// </summary>
    /// <param name="cookieName">The name of the cookie to be deleted.</param>
    /// <returns></returns>
    public bool DeleteCookie(string cookieName)
    {
        if (!string.IsNullOrEmpty(cookieName)) return User.DeleteCookie(cookieName);
        GameLog.ScriptingError("DeleteCookie: {user} cookie name (first argument) was null or empty - returning false",
            User.Name);
        return false;
    }

    /// <summary>
    ///     Permanently remove a session cookie from a player.
    /// </summary>
    /// <param name="cookieName">The name of the cookie to be deleted.</param>
    /// <returns></returns>
    public bool DeleteSessionCookie(string cookieName)
    {
        if (!string.IsNullOrEmpty(cookieName)) return User.DeleteSessionCookie(cookieName);
        GameLog.ScriptingError(
            "DeleteSessionCookie: {user} cookie name (first argument) was null or empty - returning false", User.Name);
        return false;
    }

    /// <summary>
    ///     Display a motion on the user
    /// </summary>
    /// <param name="motionId">the motion to display</param>
    /// <param name="speed">speed of the diplayed motion</param>
    public void DisplayMotion(int motionId, short speed = 20)
    {
        User.Motion((byte) motionId, speed);
    }

    /// <summary>
    ///     Heal a player to full HP.
    /// </summary>
    public void HealToFull()
    {
        User.Heal(User.Stats.MaximumHp);
    }

    /// <summary>
    ///     Heal a player for the specified amount of HP.
    /// </summary>
    /// <param name="heal">Integer amount of HP to be restored.</param>
    public void Heal(int heal)
    {
        User.Heal(heal);
    }

    /// <summary>
    ///     Deal damage to the current player.
    /// </summary>
    /// <param name="damage">Integer amount of damage to deal.</param>
    /// <param name="element">Element of the damage (e.g. fire, air)</param>
    /// <param name="damageType">Type of damage (direct, magical, etc)</param>
    public void Damage(int damage, ElementType element = ElementType.None,
        DamageType damageType = DamageType.Direct)
    {
        User.Damage(damage, element, damageType);
    }

    /// <summary>
    ///     Deal physical (direct) damage to the current player.
    /// </summary>
    /// <param name="damage">Integer amount of damage to deal.</param>
    /// <param name="fatal">
    ///     Whether or not the damage should kill the player. If false, damage > current HP is reduced to
    ///     (hp-1).
    /// </param>
    public void Damage(int damage, bool fatal = true)
    {
        if (fatal)
            User.Damage(damage, ElementType.None, DamageType.Direct, DamageFlags.Nonlethal);
        else
            User.Damage(damage);
    }

    /// <summary>
    ///     Give an instance of an item to a player.
    /// </summary>
    /// <param name="obj">HybrasylWorldObject, representing an item existing in the world, to give to the player.</param>
    /// <returns>Boolean indicating whether or not it was successful (player may have full inventory, etc)</returns>
    public bool GiveItem(HybrasylWorldObject obj)
    {
        if (obj.Obj is ItemObject io)
            return User.AddItem(io.Name);
        GameLog.ScriptingError("GiveItem: {user} - object (first argument) was either null, or not an item", User.Name);
        return false;
    }

    /// <summary>
    ///     Check to see if a user has the specified cookie; if not, set it, give experience, and optionally, send them a
    ///     system message.
    /// </summary>
    /// <param name="cookie">Name of the cookie to be set.</param>
    /// <param name="xp">Amount of XP to award.</param>
    /// <param name="completionMessage">A system message that will be sent to the user.</param>
    /// <returns>Boolean indicating whether or not the user was awarded XP.</returns>
    public bool CompletionAward(string cookie, uint xp = 0, string completionMessage = null)
    {
        if (string.IsNullOrEmpty(cookie))
        {
            GameLog.ScriptingError(
                "CompletionAward: {user} - cookie name (first parameter) cannot be null or empty - returning false",
                User.Name);
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
    ///     Give a new instance of the named item to a player, optionally with a specified quantity.
    /// </summary>
    /// <param name="name">The name of the item to be created.</param>
    /// <param name="count">The count (stack) of the item to be created.</param>
    /// <returns>Boolean indicating whether or not it was successful (player may have full inventory, etc)</returns>
    public bool GiveItem(string name, int count = 1)
    {
        if (string.IsNullOrEmpty(name))
        {
            GameLog.ScriptingError("GiveItem: {user}: item name (first parameter) was null or empty - returning false",
                User.Name);
            return false;
        }

        // Does the item exist?
        if (Game.World.WorldData.TryGetValueByIndex(name, out Item template))
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

        GameLog.ScriptingError("GiveItem: {user} - item name {name} could not be found", User.Name, name);
        return false;
    }

    /// <summary>
    ///     Check to see if a player has an item, optionally with a specified quantity.
    /// </summary>
    /// <param name="name">The name of the item to check.</param>
    /// <param name="count">The quantity that will be checked.</param>
    /// <returns></returns>
    public bool HasItem(string name, int count = 1)
    {
        if (!string.IsNullOrEmpty(name)) return User.Inventory.ContainsName(name, count);
        GameLog.ScriptingError("HasItem: {user} - item name (first parameter) was null or empty - returning false",
            User.Name);
        return false;
    }

    /// <summary>
    ///     Take an item with a given name and an optional quantity from the current player's inventory.
    /// </summary>
    /// <param name="name">The name of the item to be removed.</param>
    /// <param name="count">The quantity to be removed.</param>
    /// <param name="force">Whether or not to force remove the item (override whether it is bound, etc)</param>
    /// <returns>Boolean indicating whether or not it the item was successfully removed from the player's inventory.</returns>
    public bool TakeItem(string name, int count = 1, bool force = true)
    {
        if (string.IsNullOrEmpty(name))
        {
            GameLog.ScriptingError("TakeItem: {user} - item name (first parameter) was null or empty - returning false",
                User.Name);
            return false;
        }

        if (User.Inventory.ContainsName(name))
        {
            if (User.RemoveItem(name, (ushort) count, true, force))
                return true;
            GameLog.ScriptingWarning("TakeItem: {user} - failed for {item}, might be bound", User.Name, name);
        }
        else
        {
            GameLog.ScriptingWarning("TakeItem: {user} doesn't have {item}", User.Name, name);
        }

        return false;
    }

    /// <summary>
    ///     Give experience to the current player.
    /// </summary>
    /// <param name="exp">Integer amount of experience to be awarded.</param>
    /// <returns>true</returns>
    public void GiveExperience(int exp)
    {
        User.GiveExperience((uint) exp);
    }

    public void GiveScaledExperience(float scaleFactor, int levelMaximum, int expMinimum, int expMaximum)
    {
        if (User.Stats.Level > levelMaximum)
        {
            User.GiveExperience((uint) expMaximum);
            return;
        }

        User.GiveExperience((uint) (scaleFactor * User.ExpToLevel > expMinimum
            ? scaleFactor * User.ExpToLevel
            : expMinimum));
    }

    /// <summary>
    ///     Take experience from the current player.
    /// </summary>
    /// <param name="exp">Integer amount of experience to be deducted.</param>
    /// <returns>
    ///     Whether or not the experience was removed (if the requested amount exceeds total experience, none will be
    ///     removed).
    /// </returns>
    public bool TakeExperience(int exp)
    {
        if ((uint) exp > User.Stats.Experience)
            return false;
        User.Stats.Experience -= (uint) exp;
        SystemMessage($"Your world spins as your insight leaves you ((-{exp} experience!))");
        User.UpdateAttributes(StatUpdateFlags.Experience);
        return true;
    }

    /// <summary>
    ///     Add a given skill to a player's skillbook.
    /// </summary>
    /// <param name="skillname">The name of the skill to be added.</param>
    /// <returns>Boolean indicating success</returns>
    public bool AddSkill(string skillname)
    {
        if (string.IsNullOrEmpty(skillname))
        {
            GameLog.ScriptingError("AddSkill: {user} - skill name (first argument) cannot be null or empty");
        }
        else if (Game.World.WorldData.TryGetValueByIndex(skillname, out Castable result))
        {
            User.AddSkill(result);
            return true;
        }
        else
        {
            GameLog.ScriptingError("AddSkill: {user} - skill {skill} not found", User.Name, skillname);
        }

        return false;
    }


    /// <summary>
    ///     Check to see if the specified skill exists in the user's skill book.
    /// </summary>
    /// <param name="skillname">Name of the skill to find.</param>
    /// <returns>Boolean indicating whether or not the user knows the skill.</returns>
    public bool HasSkill(string skillname)
    {
        if (string.IsNullOrEmpty(skillname))
            GameLog.ScriptingError("HasSkill: {user} - skill name (first argument) cannot be null or empty");
        else if (Game.World.WorldData.TryGetValueByIndex(skillname, out Castable result))
            return User.SkillBook.Contains(result.Id);
        else
            GameLog.ScriptingError("HasSkill: {user} - skill {skill} not found", User.Name, skillname);
        return false;
    }

    /// <summary>
    ///     Add a given spell to a player's spellbook.
    /// </summary>
    /// <param name="spellname">The name of the spell to be added.</param>
    /// <returns>Boolean indicating success</returns>
    public bool AddSpell(string spellname)
    {
        if (string.IsNullOrEmpty(spellname))
        {
            GameLog.ScriptingError("AddSpell: {user} - spell name (first argument) cannot be null or empty");
        }
        else if (Game.World.WorldData.TryGetValueByIndex(spellname, out Castable result))
        {
            User.AddSpell(result);
            return true;
        }
        else
        {
            GameLog.ScriptingError("AddSpell: {user} - spell {spell} not found", User.Name, spellname);
        }

        return false;
    }

    /// <summary>
    ///     Check to see if the specified spell exists in the user's spell book.
    /// </summary>
    /// <param name="skillname">Name of the spell to find.</param>
    /// <returns>Boolean indicating whether or not the user knows the spell.</returns>
    public bool HasSpell(string spellname)
    {
        if (string.IsNullOrEmpty(spellname))
            GameLog.ScriptingError("HasSpell: {user} - spell name (first argument) cannot be null or empty");
        else if (Game.World.WorldData.TryGetValueByIndex(spellname, out Castable result))
            return User.SpellBook.Contains(result.Id);
        else
            GameLog.ScriptingError("HasSpell: {user} - spell {spell} not found", User.Name, spellname);
        return false;
    }

    /// <summary>
    ///     Send a system message ("orange message") to the current player.
    /// </summary>
    /// <param name="message"></param>
    public void SystemMessage(string message)
    {
        // This is a typical client "orange message"
        User.SendMessage(message, MessageTypes.SYSTEM_WITH_OVERHEAD);
    }

    /// <summary>
    ///     Indicates whether the current player is a peasant, or has an assigned class.
    /// </summary>
    /// <returns>Boolean indicating whether or not current player is a peasant.</returns>
    public bool IsPeasant() => User.Class == Class.Peasant;

    /// <summary>
    ///     Indicates whether the current player is in a guild.
    /// </summary>
    /// <returns>Boolean indicating whether or not current player is in a guild.</returns>
    public bool IsInGuild() => User.GuildGuid != System.Guid.Empty;

    /// <summary>
    ///     Sends a whisper ("blue message") from a given name to the current player.
    /// </summary>
    /// <param name="name">The name to be used for the whisper (e.g. who it is from)</param>
    /// <param name="message">The message.</param>
    public void Whisper(string name, string message)
    {
        User.SendWhisper(name, message);
    }

    /// <summary>
    ///     Shout something as the user.
    /// </summary>
    /// <param name="message">The message to shout.</param>
    public void Shout(string message)
    {
        User.Shout(message);
    }

    public void SendMessage(string message, int type)
    {
        User.SendMessage(message, (byte) type);
    }

    /// Close any active dialogs for the current player.
    /// </summary>
    public void EndDialog()
    {
        User.DialogState.EndDialog();
        User.ActiveDialogSession = null;
        User.SendCloseDialog();
        //GameLog.Info("Dialog: closed by script");
    }

    // Helper override 

    /// <summary>
    ///     Start a dialog sequence for the current player. This will display the first dialog in the sequence to the player.
    /// </summary>
    /// <param name="sequenceName">The name of the sequence to start</param>
    /// <param name="associateOverride">An IInteractable to associate with the dialog as the origin.</param>
    public void StartSequence(string sequenceName, dynamic associateOverride = null)
    {
        if (sequenceName == null)
        {
            GameLog.ScriptingError("StartSequence: {user} - sequence name (first argument) cannot be null or empty",
                User.Name);
            return;
        }

        DialogSequence sequence = null;
        IInteractable associate = null;
        GameLog.DebugFormat("{0} starting sequence {1}", User.Name, sequenceName);

        // First: is this a global sequence?
        Game.World.GlobalSequences.TryGetValue(sequenceName, out sequence);

        // Next: what object are we associated with?
        if (associateOverride == null)
        {
            if (User.DialogState.Associate != null)
                associate = User.DialogState.Associate;
            else if (User.LastAssociate != null)
                associate = User.LastAssociate;
        }
        else
        {
            // Deal with some Lua vagaries here. We use a dynamic in the signature so we can handle a variety of object types
            // coming back from Lua, which in general, doesn't like interfaces (and also we have to deal with our own wrapping)
            associate = associateOverride is HybrasylWorldObject hwo
                ? hwo.WorldObject as IInteractable
                : associateOverride as IInteractable;
        }

        // If we didn't get a sequence before, try with our associate. Either we know it implements an Interactable 
        // interface or it's null
        if (sequence == null && associate != null)
            associate.SequenceIndex.TryGetValue(sequenceName, out sequence);

        // We should hopefully have a sequence now...
        if (sequence == null)
        {
            GameLog.ScriptingError(
                "StartSequence: {user} - called from {associate}: sequence name {seq} cannot be found!",
                User.Name, associate?.Name ?? "globalsequence", sequenceName);
            // To be safe, terminate all dialog state
            User.DialogState.EndDialog();
            // If the user was previously talking to a merchant, and we can't find a sequence,
            // simply display the main menu again. If it's a reactor....oh well.
            if (associate is IPursuitable ip)
                ip.DisplayPursuits(User);
            return;
        }

        // If we're here, sequence should now be our target sequence, 
        // let's end the current state and start a new one
        if (User.DialogState.InDialog)
            // Transition between current and new dialog
            User.DialogState.TransitionDialog(associate, sequence);
        //GameLog.Info($"Transitioning between dialog {User.DialogState.PreviousPursuitId} and {User.DialogState.CurrentPursuitId}");
        //GameLog.Info($"Transition Dialog NPC associate {associate?.Name ?? "null"}");
        else
            // Start a new dialog
            User.DialogState.StartDialog(associate, sequence);
        //GameLog.Info($"StartDialog Dialog NPC associate {associate?.Name ?? "null"}");

        // Lastly, show the new dialog
        var invocation = associate switch
        {
            // Get the raw interactable (underlying unwrapped object) and use that to start the dialog
            IScriptable { WorldObject: IInteractable interactable } => new DialogInvocation(interactable, User, User),
            not null => new DialogInvocation(associate, User, User),
            _ => null
        };
        if (invocation is not null)
            User.DialogState.ActiveDialog.ShowTo(invocation);
    }

    /// <summary>
    ///     Set a user's hairstyle from a script
    /// </summary>
    /// <param name="hairStyle">The target hairstyle</param>
    public void SetHairstyle(int hairStyle)
    {
        User.SetHairstyle((ushort) hairStyle);
    }

    /// <summary>
    ///     Set's a user's haircolor from a script
    /// </summary>
    /// <param name="itemColor">The color to apply</param>
    public void SetHairColor(string itemColor)
    {
        var color = (ItemColor) Enum.Parse(typeof(ItemColor), itemColor);
        User.SetHairColor(color);
    }

    /// <summary>
    ///     Trigger or clear a cooldown for a specific spell or skill.
    /// </summary>
    /// <param name="name">The name of the spell or skill</param>
    /// <param name="clear">Whether or not to trigger or clear. True clears; false triggers.</param>
    public void SetCooldown(string name, bool clear = false)
    {
        var castable = User.SpellBook.IndexOf(name) == -1
            ? User.SkillBook.GetSlotByName(name)
            : User.SpellBook.GetSlotByName(name);
        if (castable == null)
        {
            GameLog.ScriptingError("SetCooldown: {name} not found in user's castables");
            return;
        }

        if (clear)
            castable.ClearCooldown();
        else
            castable.TriggerCooldown();

        User.SendCooldown(castable, clear);
    }

    /// <summary>
    ///     Send an update to the client that stats have changed.
    /// </summary>
    public void UpdateAttributes() => User.UpdateAttributes(StatUpdateFlags.Full);

    /// <summary>
    ///     Apply a given status to a player.
    /// </summary>
    /// <param name="statusName">The name of the status</param>
    /// <param name="duration">The duration of the status, if zero, use default </param>
    /// <param name="tick">How often the tick should fire on the status (eg OnTick), if zero, use default</param>
    /// <param name="intensity">The intensity of the status (damage modifier), defaults to 1.0</param>
    /// <returns>boolean indicating whether or not the status was applied</returns>
    public bool ApplyStatus(string statusName, int duration = 0, int tick = 0, double intensity = 1)
    {
        var status = Game.World.WorldData.Get<Status>(statusName);
        if (status == null)
        {
            GameLog.ScriptingError("ApplyStatus: status {statusName} not found");
            return false;
        }

        return User.ApplyStatus(new CreatureStatus(status, User, null, null,
            duration == 0 ? status.Duration : duration,
            tick == 0 ? status.Tick : tick,
            intensity));
    }

    public void RemoveAllStatuses() => User.RemoveAllStatuses();
}