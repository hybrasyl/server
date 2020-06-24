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
using System.Reflection;

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]
    public class HybrasylUser
    {
        internal User User { get; set; }
        internal HybrasylWorld World { get; set; }
        internal HybrasylMap Map { get; set; }
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

        // TODO: determine a better way to do this in lua via moonsharp
        /// <summary>
        /// The type of object this is. This is a shortcut to reference in scripting as evaluating type is annoying; so you can check the Type property instead.
        /// e.g. invoker.Type == "player"
        /// </summary>
        public string Type => "player";


        /// <summary>
        /// The gender of the player. For Darkages purpose, this will evaluate to Male or Female.
        /// </summary>
        public Xml.Gender Gender => User.Gender;

        /// <summary>
        /// The current HP (hit points) of the user. This can be set to an arbitrary value; the player's HP display is automatically updated.
        /// </summary>
        public uint Hp
        {
            get { return User.Stats.Hp; }
            set {
                User.Stats.Hp = value;
                User.UpdateAttributes(StatUpdateFlags.Current);
            }
        }

        /// <summary>
        /// The current level of the user. Client supports up to level 255; Hybrasyl has the same level cap as usda, 99. 
        /// </summary>
        public int Level { get => User.Stats.Level; }

        /// <summary>
        /// Amount of gold the user currently has.
        /// </summary>
        public uint Gold { get => User.Gold; }

        /// <summary>
        /// Whether the user is alive or not.
        /// </summary>
        public bool Alive { get => User.Condition.Alive; }

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
        /// The current MP (magic points) of the user. This can be set to an arbitrary value; the player's MP display is automatically updated.
        /// </summary>
        public uint Mp
        {
            get { return User.Stats.Mp; }
            set
            {
                User.Stats.Mp = value;
                User.UpdateAttributes(StatUpdateFlags.Current);
            }
        }


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
        /// Get the objects a player is facing (for instance, items on the ground in front of the player)/
        /// </summary>
        /// <returns>A list of HybrasylWorldObjects that the player is facing.</returns>
        public List<HybrasylWorldObject> GetFacingObjects()
        {
            return User.GetFacingObjects().Select(item => new HybrasylWorldObject(item)).ToList();
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
                    throw new ArgumentException("Invalid class");
            }
            User.Legend.AddMark(icon, LegendColor.White, legendtext, "CLS");
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
                GameLog.ErrorFormat("Legend mark: {0}: duplicate prefix {1}", User.Name, prefix);
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
            LegendMark mark;
            if (!User.Legend.TryGetMark(prefix, out mark)) return false;
            mark.Quantity = quantity;
            mark.Public = isPublic;
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

            GameLog.Warning($"invoker {invoker} or sequence {sequence} not found");
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
                GameLog.WarningFormat("{0}: value could not be converted to string? {1}", User.Name, e.ToString());
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
                GameLog.WarningFormat("{0}: value could not be converted to string? {1}", User.Name, e.ToString());
            }

        }

        /// <summary>
        /// Get the value of a session cookie, if it exists.
        /// </summary>
        /// <param name="cookieName">The name of the cookie to fetch</param>
        /// <returns>string representation of the cookie value</returns>
        public string GetSessionCookie(string cookieName) => User.GetSessionCookie(cookieName);

        /// <summary>
        /// Get the value of a cookie, if it exists.
        /// </summary>
        /// <param name="cookieName">The name of the cookie to fetch</param>
        /// <returns>string representation of the cookie value</returns>
        public string GetCookie(string cookieName) => User.GetCookie(cookieName);

        /// <summary>
        /// Check to see if a player has a specified cookie or not.
        /// </summary>
        /// <param name="cookieName">Cookie name to check</param>
        /// <returns>Boolean indicating whether or not the named cookie exists</returns>
        public bool HasCookie(string cookieName) => User.HasCookie(cookieName);

        /// <summary>
        /// Check to see if a player has a specified session cookie or not.
        /// </summary>
        /// <param name="cookieName">Cookie name to check</param>
        /// <returns>Boolean indicating whether or not the named cookie exists</returns>
        public bool HasSessionCookie(string cookieName) => User.HasSessionCookie(cookieName);

        /// <summary>
        /// Permanently remove a cookie from a player.
        /// </summary>
        /// <param name="cookieName">The name of the cookie to be deleted.</param>
        /// <returns></returns>
        public bool DeleteCookie(string cookieName) => User.DeleteCookie(cookieName);

        /// <summary>
        /// Permanently remove a session cookie from a player.
        /// </summary>
        /// <param name="cookieName">The name of the cookie to be deleted.</param>
        /// <returns></returns>
        public bool DeleteSessionCookie(string cookieName) => User.DeleteSessionCookie(cookieName);

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
        /// Teleport the player to an x,y coordinate location on the specified map.
        /// </summary>
        /// <param name="location">The map name</param>
        /// <param name="x">X coordinate target</param>
        /// <param name="y">Y coordinate target</param>
        public void Teleport(string location, int x, int y)
        {
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
        public void Damage(int damage, Xml.Element element = Xml.Element.None,
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
                User.Damage(damage, Xml.Element.None, Xml.DamageType.Direct, Xml.DamageFlags.Nonlethal);
            else
                User.Damage(damage, Xml.Element.None, Xml.DamageType.Direct);

        }

        /// <summary>
        /// Give an instance of an item to a player.
        /// </summary>
        /// <param name="obj">HybrasylWorldObject, representing an item existing in the world, to give to the player.</param>
        /// <returns>Boolean indicating whether or not it was successful (player may have full inventory, etc)</returns>
        public bool GiveItem(HybrasylWorldObject obj)
        {
            if (obj.Obj is ItemObject)
                return User.AddItem(obj.Obj as ItemObject);
            return false;
        }

        /// <summary>
        /// Give a new instance of the named item to a player, optionally with a specified quantity.
        /// </summary>
        /// <param name="name">The name of the item to be created.</param>
        /// <param name="count">The count (stack) of the item to be created.</param>
        /// <returns>Boolean indicating whether or not it was successful (player may have full inventory, etc)</returns>
        public bool GiveItem(string name, int count = 1)
        {
            // Does the item exist?
            if (Game.World.WorldData.TryGetValueByIndex(name, out Xml.Item template))
            {
                var item = Game.World.CreateItem(template.Id);
                if (count > 1)
                    item.Count = count;
                else
                    item.Count = item.MaximumStack;
                Game.World.Insert(item);
                User.AddItem(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Take an item with a given name from the current player's inventory.
        /// </summary>
        /// <param name="name">The name of the item to be removed.</param>
        /// <returns>Boolean indicating whether or not it the item was successfully removed from the player's inventory.</returns>
        public bool TakeItem(string name)
        {
            if (User.Inventory.ContainsName(name))
            {
                // Find the first instance of the specified item and remove it
                var slots = User.Inventory.SlotByName(name);
                if (slots.Length == 0)
                {
                    GameLog.ScriptingWarning("{Function}: User had {item} a moment ago but now it's gone...?",
                        MethodInfo.GetCurrentMethod().Name, User.Name, name);
                    return false;
                }
                GameLog.ScriptingDebug("{Function}: A script removed {item} from {User}'s inventory ",
                    MethodInfo.GetCurrentMethod().Name, User.Name, name); return User.Inventory.Remove(slots.First());
            }
            GameLog.ScriptingDebug("{Function}: User {User} doesn't have {item}",
                MethodInfo.GetCurrentMethod().Name, User.Name, name);
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
            if (Game.World.WorldData.TryGetValue(skillname, out Xml.Castable result))
            {
                User.AddSkill(result);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Add a given spell to a player's spellbook.
        /// </summary>
        /// <param name="spellname">The name of the spell to be added.</param>
        /// <returns>Boolean indicating success</returns>
        public bool AddSpell(string spellname)
        {
            if (Game.World.WorldData.TryGetValue(spellname, out Xml.Castable result))
            {
                User.AddSpell(result);
                return true;
            }
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
        public bool IsInGuild() => User.GuildUuid != null;
        
        /// <summary>
        /// Sends a whisper ("blue message") from a given name to the current player.
        /// </summary>
        /// <param name="name">The name to be used for the whisper (e.g. who it is from)</param>
        /// <param name="message">The message.</param>
        public void Whisper(string name, string message)
        {
            User.SendWhisper(name, message);
        }

        /// <summary>
        /// Sends an in-game mail to the current player. NOT TESTED.
        /// </summary>
        /// <param name="name">The name to be used for the mail sender (who it is from)</param>
        /// <param name="subject">The message.</param>
        /// <param name="message">The message.</param>
        public void Mail(string name, string subject, string message)
        {
            User.Mailbox.Messages.Add(new Message(User.Name, name, subject, message));
        }

        /// <summary>
        /// Close any active dialogs for the current player.
        /// </summary>
        public void EndDialog()
        {
            User.DialogState.EndDialog();
            User.SendCloseDialog();
        }

        /// <summary>
        /// Start a dialog sequence for the current player. This will display the first dialog in the sequence to the player.
        /// </summary>
        /// <param name="sequenceName">The name of the sequence to start</param>
        /// <param name="associateOverride">An object to associate with the dialog as the invokee.</param>
        public void StartSequence(string sequenceName, HybrasylWorldObject associateOverride = null)
        {
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
                GameLog.ErrorFormat("called from {0}: sequence name {1} cannot be found!",
                    associate?.Name ?? "globalsequence", sequenceName);
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

            User.DialogState.EndDialog();
            User.DialogState.StartDialog(associate, sequence);
            User.DialogState.ActiveDialog.ShowTo(User, associate);
        }

        /// <summary>
        /// Calculate the Manhattan distance (distance between two points measured along axes at right angles) 
        /// between the current player and a target object.
        /// </summary>
        /// <param name="target">The target object</param>
        /// <returns>The numeric distance</returns>
        public int Distance(HybrasylWorldObject target) => User.Distance(target.Obj);
    }
}
