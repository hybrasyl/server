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

using Hybrasyl.Objects;
using Hybrasyl.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Hybrasyl.Messaging
{
    // Various admin commands are implemented here.
    
    class ShowCookies : ChatCommand
    {
        public new static string Command = "showcookies";
        public new static string ArgumentText = "<string playername>";
        public new static string HelpText = "Show permanent and session cookies set for a specified player";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string [] args)
        {
            if (Game.World.WorldData.TryGetValue<User>(args[0], out User target))
            {
                string cookies = $"User {target.Name} Cookie List\n\n---Permanent Cookies---\n";
                foreach (var cookie in target.GetCookies())
                    cookies = $"{cookies}\n{cookie.Key} : {cookie.Value}\n";
                cookies = $"{cookies}\n---Session Cookies---\n";
                foreach (var cookie in target.GetSessionCookies())
                    cookies = $"{cookies}\n{cookie.Key} : {cookie.Value}\n";
                return Success($"{cookies}", MessageTypes.SLATE_WITH_SCROLLBAR);
            }
            return Fail($"User {args[0]} not logged in");
        }
    }

    class ClearCookie : ChatCommand
    {
        public new static string Command = "clearcookie";
        public new static string ArgumentText = "<string playername> <string cookie>";
        public new static string HelpText = "Clear a given (permament) cookie for a specified player";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValue<User>(args[0], out User target))
            {
                if (target.HasCookie(args[1]))
                {
                    target.DeleteCookie(args[1]);
                    return Success($"User {target.Name}: cookie {args[1]} deleted");
                }
                else return Fail($"User {args[0]} doesn't have cookie {args[1]}");
            }
            return Fail($"User {args[0]} not logged in");
        }
    }

    class ClearSessionCookie : ChatCommand
    {
        public new static string Command = "clearsessioncookie";
        public new static string ArgumentText = "<string playername> <string cookie>";
        public new static string HelpText = "Clear a given session cookie for a specified player";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValue<User>(args[0], out User target))
            {
                if (target.HasSessionCookie(args[1]))
                {
                    target.DeleteSessionCookie(args[1]);
                    return Success($"User {target.Name}: session cookie {args[1]} deleted");
                }
                else return Fail($"User {args[0]} doesn't have session cookie {args[1]}");
            }
            return Fail($"User {args[0]} not logged in");
        }
    }

    class DumpMetadata : ChatCommand
    {
        public new static string Command = "dumpmetadata";
        public new static string ArgumentText = "<string metadatafile>";
        public new static string HelpText = "Dump (in hex) a metadata file ";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.ContainsKey<CompiledMetafile>(args[0]))
            {
                var file = Game.World.WorldData.Get<CompiledMetafile>(args[0]);
                var filepath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Hybrasyl");
                File.WriteAllBytes($"{filepath}\\{args[0]}.mdf", file.Data);
                return Success($"{filepath}\\{args[0]}.mdf written to disk");
            }
            return Fail("Look chief idk about all that");
        }
    }
   
    class SetCookie : ChatCommand
    {
        public new static string Command = "setcookie";
        public new static string ArgumentText = "<string playername> <string cookie> <string value>";
        public new static string HelpText = "Set a given (permament) cookie for a specified player";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValue<User>(args[0], out User target))
            {
                target.SetCookie(args[1], args[2]);
                return Success($"User {target.Name}: cookie {args[1]} set");
            }
            return Fail($"User {args[0]} not logged in");
        }
    }

    class Immortal : ChatCommand
    {
        public new static string Command = "immortal";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Make yourself immune to all damage";
        public new static bool Privileged = true;
        
        public new static ChatCommandResult Run(User user, params string[] args)
        {
            user.AbsoluteImmortal = !user.AbsoluteImmortal;
            user.MagicalImmortal = !user.MagicalImmortal;
            user.PhysicalImmortal = !user.PhysicalImmortal;
            if (user.AbsoluteImmortal)
                return Success("You cannot be harmed.");
            else
                return Success("You return to the realm of the mortal.");
        }
    }

    class ShowEphemeral : ChatCommand
    {
        public new static string Command = "showephemeral";
        public new static string ArgumentText = "<string mundane>";
        public new static string HelpText = "Show ephemeral values set for a specified mundane";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValue(args[0], out Merchant merchant))
            {
                string ephemerals = $"Mundane {merchant.Name} Ephemeral Store\n\n";
                foreach (var kv in merchant.GetEphemeralValues())
                    ephemerals = $"{ephemerals}\n{kv.Item1} : {kv.Item2}\n";
                return Success($"{ephemerals}", MessageTypes.SLATE_WITH_SCROLLBAR);
            }
            return Fail($"Mundane {args[0]} could not be found");
        }
    }

    class SetEphemeral : ChatCommand
    {
        public new static string Command = "setephemeral";
        public new static string ArgumentText = "<string mundane> <string key> <string value>";
        public new static string HelpText = "Set a given ephemeral store value for a specified mundane";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValue(args[0], out Merchant merchant))
            {
                merchant.SetEphemeral(args[1], args[2]);
                return Success($"{merchant.Name}: {args[1]} set to {args[2]}");
            }
            else return Fail($"NPC {args[0]} not found.");
        }
    }

    class ClearEphemeral : ChatCommand
    {
        public new static string Command = "clearephemeral";
        public new static string ArgumentText = "<string mundane> <string key>";
        public new static string HelpText = "Clear a given ephemeral store value for a specified mundane";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValue(args[0], out Merchant merchant))
            {
                merchant.ClearEphemeral(args[1]);
                return Success($"{merchant.Name}: {args[1]} set to {args[2]}");
            }
            else return Fail($"NPC {args[0]} not found.");
        }
    }

    class SetSessionCookie : ChatCommand
    {
        public new static string Command = "setcookie";
        public new static string ArgumentText = "<string playername> <string cookie> <string value>";
        public new static string HelpText = "Set a given (permament) cookie for a specified player";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValue<User>(args[0], out User target))
            {
                target.SetCookie(args[1], args[2]);
                return Success($"User {target.Name}: cookie {args[1]} set");
            }
            return Fail($"User {args[0]} not logged in");
        }
    }

    class DeleteSessionCookie : ChatCommand
    {
        public new static string Command = "deletesessioncookie";
        public new static string ArgumentText = "<string cookie> | <string playername> <string cookie>";
        public new static string HelpText = "Clear (delete) a given session (transient) scripting cookie. This is useful when working with scripts that modify player state.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (args.Length == 1)
            {
                user.DeleteSessionCookie(args[0]);
                return Success($"Session flag {args[0]} deleted.");
            }
            else
            {
                var target = Game.World.WorldData.Get<User>(args[0]);

                if (target.IsExempt)
                    return Fail($"User {target.Name} is exempt from your meddling.");
                else
                    target.DeleteSessionCookie(args[1]);
                return Success($"Player {target.Name}: flag {args[1]} removed.");
            }
        }
    }

    class DeleteCookie : ChatCommand
    {
        public new static string Command = "deletecookie";
        public new static string ArgumentText = "<string cookie> | <string playername> <string cookie>";
        public new static string HelpText = "Clear (delete) a given scripting cookie. This is useful when working with scripts that modify player state.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (args.Length == 1)
            {
                user.DeleteCookie(args[0]);
                return Success($"Session flag {args[0]} deleted.");
            }
            else
            {
                var target = Game.World.WorldData.Get<User>(args[0]);

                if (target.IsExempt)
                    return Fail($"User {target.Name} is exempt from your meddling.");
                else
                    target.DeleteCookie(args[1]);
                return Success($"Player {target.Name}: flag {args[1]} removed.");
            }
        }
    }

    class SummonCommand : ChatCommand
    {
        public new static string Command = "summon";
        public new static string ArgumentText = "<string playerName>";
        public new static string HelpText = "Summon a specified player to you";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!Game.World.WorldData.ContainsKey<User>(args[0]))
                return Fail($"User {args[0]} not logged in");

            var target = Game.World.WorldData.Get<User>(args[0]);

            if (target.IsExempt)
                return Fail($"User {user.Name} is exempt from your meddling");

            target.Teleport(user.Location.MapId, user.Location.X, user.Location.Y);
            return Success($"User {user.Name} has been summoned.");
        }
    }

    class KickCommand : ChatCommand
    {
        public new static string Command = "kick";
        public new static string ArgumentText = "<string playerName>";
        public new static string HelpText = "Kick a specified player off the server";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!Game.World.WorldData.ContainsKey<User>(args[0]))
                return Fail($"User {args[0]} not logged in");

            var target = Game.World.WorldData.Get<User>(args[0]);

            if (target.IsExempt)
                return Fail($"User {target.Name} is exempt from your meddling");
            else
                target.Logoff();

            return Success($"User {target.Name} was kicked.");
        }

    }

    class GcmCommand : ChatCommand
    {
        public new static string Command = "gcm";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Dump a bunch of debugging information about server connections.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            var gcmContents = "Contents of Global Connection Manifest\n";
            var userContents = "Contents of User Dictionary\n";
            var ActiveUserContents = "Contents of ActiveUsers Concurrent Dictionary\n";
            foreach (var pair in GlobalConnectionManifest.ConnectedClients)
            {
                var serverType = string.Empty;
                switch (pair.Value.ServerType)
                {
                    case ServerTypes.Lobby:
                        serverType = "Lobby";
                        break;

                    case ServerTypes.Login:
                        serverType = "Login";
                        break;

                    default:
                        serverType = "World";
                        break;
                }
                try
                {
                    gcmContents = gcmContents + string.Format("{0}:{1} - {2}:{3}\n", pair.Key,
                        ((IPEndPoint)pair.Value.Socket.RemoteEndPoint).Address.ToString(),
                        ((IPEndPoint)pair.Value.Socket.RemoteEndPoint).Port, serverType);
                }
                catch
                {
                    gcmContents = gcmContents + string.Format("{0}:{1} disposed\n", pair.Key, serverType);
                }
            }
            foreach (var tehuser in Game.World.WorldData.Values<User>())
            {
                userContents = userContents + tehuser.Name + "\n";
            }
            foreach (var tehotheruser in Game.World.ActiveUsersByName)
            {
                ActiveUserContents = ActiveUserContents +
                                     string.Format("{0}: {1}\n", tehotheruser.Value, tehotheruser.Key);
            }

            // Report to the end user
            return Success($"{gcmContents}\n\n{userContents}\n\n{ActiveUserContents}",
                MessageTypes.SLATE_WITH_SCROLLBAR);
        }

    }

    class MuteCommand : ChatCommand
    {
        public new static string Command = "mute";
        public new static string ArgumentText = "<string playerName>";
        public new static string HelpText = "Mute the specified player (whisper/shout/talk).";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!Game.World.WorldData.ContainsKey<User>(args[0]))
                return Fail($"User {args[0]} not logged in.");

            var target = Game.World.WorldData.Get<User>(args[0]);

            if (target.IsExempt)
                return Fail($"User {target.Name} is exempt from your meddling");
            else
                target.IsMuted = true;

            return Success($"User {target.Name} was muted.");
        }

    }

    class UnmuteCommand : ChatCommand
    {
        public new static string Command = "unmute";
        public new static string ArgumentText = "<string playerName>";
        public new static string HelpText = "Unmute the specified player.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!Game.World.WorldData.ContainsKey<User>(args[0]))
                return Fail($"User {args[0]} not logged in");

            var target = Game.World.WorldData.Get<User>(args[0]);

            if (target.IsExempt)
                return Fail($"User {target.Name} is exempt from your meddling");
            else
                target.IsMuted = false;

            return Success($"User {target.Name} was unmuted.");
        }

    }

    class ReloadCommand : ChatCommand
    {
        public new static string Command = "reload";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Reload world data.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            return Success("This feature is not yet implemented.");
        }
    }

    class WallsCommand : ChatCommand
    {
        public new static string Command = "walls";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Enable or disable wallwalking.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            var disableCollisions = user.Flags.ContainsKey("disablecollisions") ? user.Flags["disablecollisions"] : false;
            user.Flags["disablecollisions"] = !disableCollisions;
            var msg = user.Flags["disablecollisions"] ? Success("Wall walking enabled") : Success("Wall walking disabled");
            user.UpdateAttributes(Enums.StatUpdateFlags.Primary);
            return msg;
        }
    }

    class ShutdownCommand : ChatCommand
    {
        public new static string Command = "shutdown";
        public new static string ArgumentText = "<string shutdownpassword>";
        public new static string HelpText = "Request an orderly shutdown of the server.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (string.Equals(args[0], Constants.ShutdownPassword))
                World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.ShutdownServer, user.Name));
            return Success("Chaos is rising up. Please re-enter in a few minutes");
        }
    }

    class ScriptingCommand : ChatCommand
    {
        public new static string Command = "scripting";
        public new static string ArgumentText = "<reload|disable|enable|status> <string scriptname>";
        public new static string HelpText = "Reload, disable, enable or request status on the specified script.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {

            if (Game.World.ScriptProcessor.TryGetScript(args[1].Trim(), out Script script))
            {
                if (args[0].ToLower() == "reload")
                {
                    script.Disabled = true;
                    if (script.Run())
                    {
                        script.Disabled = false;
                        return Success($"Script {script.Name}: reloaded.");
                    }
                    else
                    {
                        return Fail($"Script {script.Name}: load/parse error, check scripting log.");
                    }
                }
                else if (args[0].ToLower() == "enable")
                {
                    script.Disabled = false;
                    return Success($"Script {script.Name}: enabled.");
                }
                else if (args[0].ToLower() == "disable")
                {
                    script.Disabled = true;
                    return Success($"Script {script.Name}: disabled.");
                }
                else if (args[0].ToLower() == "status")
                {
                    var scriptStatus = string.Format("{0}:", script.Name);
                    string errorSummary = "--- Error Summary ---\n";

                    if (script.LastRuntimeError == string.Empty &&
                        script.CompilationError == string.Empty)
                        errorSummary = string.Format("{0} no errors", errorSummary);
                    else
                    {
                        if (script.CompilationError != string.Empty)
                            errorSummary = string.Format("{0} compilation error: {1}", errorSummary,
                                script.CompilationError);
                        if (script.LastRuntimeError != string.Empty)
                            errorSummary = string.Format("{0} runtime error: {1}", errorSummary,
                                script.LastRuntimeError);
                    }

                    // Report to the end user
                    return Success($"{scriptStatus}\n\n{errorSummary}", MessageTypes.SLATE_WITH_SCROLLBAR);
                }
            }
            return Fail($"Script {args[1].Trim()}: not found.");
        }
    }

    class ReloadnpcCommand : ChatCommand
    {
        public new static string Command = "reloadnpc";
        public new static string ArgumentText = "<string npcname>";
        public new static string HelpText = "Reload the given NPC (dump the script and reload from disk)";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValue(args[0], out Merchant merchant))
            {
                if (Game.World.ScriptProcessor.TryGetScript(merchant.Name, out Script script))
                {
                    script.Reload();
                    merchant.Ready = false; // Force reload next time NPC processes an interaction
                    merchant.Say("...What? What happened?");
                    return Success($"NPC {args[0]} - script {script.Name} reloaded. Clicking should re-run OnSpawn.");
                }
                else return Fail("NPC found but script not found...?");
            }
            else return Fail($"NPC {args[0]} not found.");

        }
    }

    class TeleportCommand : ChatCommand
    {
        public new static string Command = "teleport";
        public new static string ArgumentText = "<string playername> | <uint mapnumber> <byte x> <byte y>";
        public new static string HelpText = "Teleport to the specified player, or the given map number and coordinates.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {

            if (args.Length == 1)
            {
                if (!Game.World.WorldData.ContainsKey<User>(args[0]))
                {
                    return Fail("That player cannot be found");
                }
                else
                {
                    var target = Game.World.WorldData.Get<User>(args[0]);
                    user.Teleport(target.Location.MapId, target.Location.X, target.Location.Y);
                    return Success($"Teleported to {target.Name} - {target.Map.Name} ({target.Location.X},{target.Location.Y})");
                }
            }
            else
            {
                if (ushort.TryParse(args[0], out ushort mapnum) && byte.TryParse(args[1], out byte x) && byte.TryParse(args[2], out byte y))
                {

                    if (Game.World.WorldData.ContainsKey<Map>(mapnum))
                    {
                        var map = Game.World.WorldData.Get<Map>(mapnum);
                        if (x < map.X && y < map.Y)
                        {
                            user.Teleport(mapnum, x, y);
                            return Success($"Teleported to {map.Name} ({x},{y}).");
                        }
                        else
                            return Fail("Invalid x/y specified (hint: mapsize is {map.X}x{map.Y})");
                    }
                    else
                        return Fail("Map number {mapnum} not found");
                }
                else
                {
                    return Fail("Couldn't parse map number or coordinates (uint / byte / byte)");
                }
            }
        }
    }

    class DebugCommand : ChatCommand
    {
        public new static string Command = "debug";
        public new static string ArgumentText = "None";
        public new static string HelpText = "Toggle whether or not debugging is enabled on the server.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            var enabled = Game.World.ToggleDebug();
            if (enabled)
                return Success("Debugging enabled");
            return Success("Debugging disabled");
        }
    }

    class SpawnCommand : ChatCommand
    {
        public new static string Command = "spawn";
        public new static string ArgumentText = "<string monsterName> [<uint hp> <uint mp> <uint str> <uint int> <uint wis> <uint con> <uint dex> <uint xp> <uint gold>]";
        public new static string HelpText = "Spawn the specified monster at your current coordinates, with optionally specified stats.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {

            if (Game.World.WorldData.TryGetValue(args[0], out Xml.Creature creature))
            {
                Xml.Spawn spawn = new Xml.Spawn();
                spawn.Castables = new List<Xml.SpawnCastable>();
                spawn.Stats.Hp = 100;
                spawn.Stats.Mp = 100;
                spawn.Stats.Str = 3;
                spawn.Stats.Int = 3;
                spawn.Stats.Wis = 3;
                spawn.Stats.Con = 3;
                spawn.Stats.Dex = 3;
                spawn.Loot.Xp = 1;
                spawn.Loot.Gold = new Xml.LootGold
                {
                    Min = 1,
                    Max = 1
                };
                if (args.Length >= 2)
                    spawn.Stats.Hp = uint.Parse(args[1]);
                if (args.Length >= 3)
                    spawn.Stats.Mp = uint.Parse(args[2]);
                if (args.Length >= 4)
                    spawn.Stats.Str = byte.Parse(args[3]);
                if (args.Length >= 5)
                    spawn.Stats.Int = byte.Parse(args[4]);
                if (args.Length >= 6)
                    spawn.Stats.Wis = byte.Parse(args[5]);
                if (args.Length >= 7)
                    spawn.Stats.Con = byte.Parse(args[6]);
                if (args.Length >= 8)
                    spawn.Stats.Dex = byte.Parse(args[7]);
                if (args.Length >= 9)
                {
                    spawn.Loot.Xp = UInt32.Parse(args[8]);
                }
                if (args.Length >= 10)
                {
                    spawn.Loot.Gold.Min = UInt32.Parse(args[9]);
                    spawn.Loot.Gold.Max = UInt32.Parse(args[9]);
                }
                Monster newMob = new Monster(creature, spawn, user.Location.MapId);
                user.World.Insert(newMob);
                user.Map.Insert(newMob, user.X, user.Y);
                return Success($"{creature.Name} spawned.");
            }
            else return Fail("Creature {args[0]} not found");

        }

    }

    class SpawnXCommand : ChatCommand
    {
        public new static string Command = "spawnx";
        public new static string ArgumentText = "<string> name <int> quantity";
        public new static string HelpText = "Spawn the specified number of monster at your random coordinates, with base stats. [FOR TESTING]";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {

            if (Game.World.WorldData.TryGetValue(args[0], out Xml.Creature creature))
            {
                var b = int.TryParse(args[1], out int n);
                if (b)
                {
                    var rand = new Random();
                    var map = Game.World.WorldData.Get<Map>(user.Map.Id);
                    for (var i = 0; i < n; i++)
                    {
                        Xml.Spawn spawn = new Xml.Spawn();
                        spawn.Castables = new List<Xml.SpawnCastable>();
                        spawn.Stats.Hp = 100;
                        spawn.Stats.Mp = 100;
                        spawn.Stats.Str = 3;
                        spawn.Stats.Int = 3;
                        spawn.Stats.Wis = 3;
                        spawn.Stats.Con = 3;
                        spawn.Stats.Dex = 3;
                        spawn.Loot.Xp = 1;
                        spawn.Loot.Gold = new Xml.LootGold
                        {
                            Min = 1,
                            Max = 1
                        };
                        Monster newMob = new Monster(creature, spawn, user.Location.MapId);
                        user.World.Insert(newMob);
                        user.Map.Insert(newMob, (byte)rand.Next(0,map.X), (byte)rand.Next(0, map.Y));
                    }
                }
                
                return Success($"{creature.Name} spawned.");
            }
            else return Fail("Creature {args[0]} not found");

        }

    }
}


