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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Hybrasyl.ChatCommands
{
    // Various admin commands are implemented here.

    class ShowCookies : ChatCommand
    {
        public new static string Command = "showcookies";
        public new static string ArgumentText = "<string playername>";
        public new static string HelpText = "Show permanent and session cookies set for a specified player";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
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

                if (target.AuthInfo.IsExempt)
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

                if (target.AuthInfo.IsExempt)
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
            if (Game.World.TryGetActiveUser(args[0], out User target))
            {
                if (target.AuthInfo.IsExempt)
                    return Fail($"User {target.Name} is exempt from your meddling.");

                target.Teleport(user.Location.MapId, user.Location.X, user.Location.Y);
                return Success($"User {target.Name} has been summoned.");
            }
            else
                return Fail($"User {args[0]} not logged in");

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
            if (Game.World.TryGetActiveUser(args[0], out User target))
            {
                if (target.AuthInfo.IsExempt)
                    return Fail($"User {target.Name} is exempt from your meddling");
                else
                    target.Logoff(true);

                return Success($"User {target.Name} was kicked.");
            }
            return Fail($"User {args[0]} not logged in");


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
            var userContents = "Active Users\n";

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

            // Report to the end user
            return Success($"{gcmContents}\n\n{userContents}",
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
            if (Game.World.TryGetActiveUser(args[0], out User target))
            {
                if (target.AuthInfo.IsExempt)
                    return Fail($"User {target.Name} is exempt from your meddling.");
                else
                    target.IsMuted = true;

                return Success($"User {target.Name} was muted.");
            }
            return Fail($"User {args[0]} not logged in.");

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
            if (Game.World.TryGetActiveUser(args[0], out User target))
            {
                if (target.AuthInfo.IsExempt)
                    return Fail($"User {target.Name} is exempt from your meddling");
                else
                    target.IsMuted = false;

                return Success($"User {target.Name} was unmuted.");
            }
            return Fail($"User {args[0]} not logged in");
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
        public new static string ArgumentText = "<string noreally> [<int delay>]";
        public new static string HelpText = "Request an orderly shutdown of the server, optionally with a delay.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (string.Equals(args[0], "noreally"))
            {
                var delay = 0;
                if (args.Length == 2)
                    if (!int.TryParse(args[1], out delay))
                        return Fail("Delay must be a number");

                World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.ShutdownServer, user.Name, delay));
                return Success("Shutdown request submitted.");
            }
            return Fail("You have to really mean it.");
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
        public new static string ArgumentText = "<string playername> | <string npcname> | <string mapname> <byte x> <byte y> | <uint mapnumber> <byte x> <byte y>";
        public new static string HelpText = "Teleport to the specified player, or the given map number and coordinates.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {

            if (args.Length == 1)
            {
                // Either user or npc
                if (Game.World.WorldData.ContainsKey<User>(args[0]))
                {
                    var target = Game.World.WorldData.Get<User>(args[0]);
                    user.Teleport(target.Location.MapId, target.Location.X, target.Location.Y);
                    return Success($"Teleported to {target.Name} - {target.Map.Name} ({target.Location.X},{target.Location.Y})");
                }
                if (Game.World.WorldData.TryGetValue(args[0], out Merchant merchant))
                {
                    (var x, var y) = merchant.Map.FindEmptyTile((byte)(merchant.Map.X / 2), (byte)(merchant.Map.Y / 2));
                    if (x > 0 && y > 0)
                    {
                        user.Teleport(merchant.Map.Id, x, y);
                        return Success($"Teleported to {merchant.Name} - {merchant.Map.Name} ({x}, {y})");
                    }
                    return Fail("Sorry, something went wrong (empty tile could not be found..?)");
                }
                return Fail($"Sorry, user or npc {args[0]} not found.");
            }
            else
            {
                ushort? mapnum = null;
                if (ushort.TryParse(args[0], out ushort num))
                    mapnum = num;
                else if (Game.World.WorldData.TryGetValueByIndex<Map>(args[0], out Map targetMap))
                    mapnum = targetMap.Id;
                else
                    return Fail("Unknown map id or map name");

                if (Game.World.WorldData.TryGetValue<Map>(mapnum, out Map map))
                {
                    if (byte.TryParse(args[1], out byte x) && byte.TryParse(args[2], out byte y))
                    {

                        if (x < map.X && y < map.Y)
                        {
                            user.Teleport(map.Id, x, y);
                            return Success($"Teleported to {map.Name} ({x},{y}).");
                        }
                        else
                            return Fail("Invalid x/y specified (hint: mapsize is {map.X}x{map.Y})");
                    }
                    else
                    {
                        return Fail("Couldn't parse map number or coordinates (uint / byte / byte)");
                    }
                }
                else
                    return Fail("Map number {mapnum} not found");
            }

        }
    }

    class QueuedepthCommand : ChatCommand
    {
        public new static string Command = "queuedepth";
        public new static string ArgumentText = "None";
        public new static string HelpText = "Display current queue depths.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            return Success($"Packet Queue Depth: {World.MessageQueue.Count}\n\nControl Message Queue Depth: {World.ControlMessageQueue.Count}",
                MessageTypes.SLATE_WITH_SCROLLBAR);
        }

    }
    class ResurrectCommand : ChatCommand
    {
        public new static string Command = "resurrect";
        public new static string ArgumentText = "None";
        public new static string HelpText = "Resurrect yourself, if dead.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!user.Condition.Alive)
            {
                user.Resurrect();
                return Success("Saved from the clutches of Sgrios.");
            }
            return Success("You're already alive..");
        }
    }

    class ReturnCommand : ChatCommand
    {
        public new static string Command = "return";
        public new static string ArgumentText = "None";
        public new static string HelpText = "Return (alive) to the exact point of your last death.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!user.Condition.Alive)
            {
                user.Resurrect(true);
                return Success("Be more careful next time.");
            }
            user.Teleport(user.Location.DeathMap.Id, user.Location.DeathMapX, user.Location.DeathMapY);
            return Success("Recalled.");
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
                spawn.Castables = new Xml.CastableGroup();
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
                        spawn.Castables = new Xml.CastableGroup();
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
                        user.Map.Insert(newMob, (byte)rand.Next(0, map.X + 1), (byte)rand.Next(0, map.Y + 1));
                    }
                }

                return Success($"{creature.Name} spawned.");
            }
            else return Fail("Creature {args[0]} not found");

        }
    }

    class ReloadXml : ChatCommand
    {
        public new static string Command = "reloadxml";
        public new static string ArgumentText = "<string> type <string> filename";
        public new static string HelpText = "Reloads a specified xml file into world data, i.e. \"castable\" \"all_psk_assail\" (Valid arguments are:\ncastable npc item element lootset nation map itemvariant spawngroup status worldmap localization";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (args.Length < 2) return Fail($"Wrong number of arguments supplied.");

            switch (args[0].ToLower())
            {
                case "castable":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedCastable = Xml.Castable.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedCastable.Id, out Xml.Castable castable))
                        {
                            Game.World.WorldData.Remove<Xml.Castable>(castable.Id);
                            Game.World.WorldData.SetWithIndex(reloadedCastable.Id, reloadedCastable, reloadedCastable.Name);
                            foreach (var activeuser in Game.World.ActiveUsers)
                            {
                                if (reloadedCastable.Book == Xml.Book.PrimarySkill || reloadedCastable.Book == Xml.Book.SecondarySkill || reloadedCastable.Book == Xml.Book.UtilitySkill)
                                {
                                    if (activeuser.SkillBook.Contains(reloadedCastable.Id))
                                    {
                                        activeuser.SkillBook[activeuser.SkillBook.SlotOf(reloadedCastable.Id)].Castable = reloadedCastable;
                                    }
                                }
                                else
                                {
                                    if (activeuser.SpellBook.Contains(reloadedCastable.Id))
                                    {
                                        activeuser.SpellBook[activeuser.SpellBook.SlotOf(reloadedCastable.Id)].Castable = reloadedCastable;
                                    }
                                }
                            }

                            return Success($"Castable {reloadedCastable.Name} set to world data");
                        }
                        return Fail($"{args[0]} {args[1]} was not found");
                    }
                case "npc":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedNpc = Xml.Npc.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedNpc.Name, out Xml.Npc npc))
                        {
                            Game.World.WorldData.Remove<Xml.Npc>(npc.Name);
                            Game.World.WorldData.Set(reloadedNpc.Name, reloadedNpc);
                            return Success($"Npc {reloadedNpc.Name} set to world data. Reload NPC to activate.");
                        }
                        return Fail($"{args[0]} {args[1]} was not found");
                    }
                case "lootset":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedLootSet = Xml.LootSet.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedLootSet.Id, out Xml.LootSet lootSet))
                        {
                            Game.World.WorldData.Remove<Xml.LootSet>(lootSet.Id);
                            Game.World.WorldData.SetWithIndex(reloadedLootSet.Id, reloadedLootSet, reloadedLootSet.Name);
                            return Success($"LootSet {reloadedLootSet.Name} set to world data");
                        }
                        return Fail($"{args[0]} {args[1]} was not found");
                    }
                case "nation":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedNation = Xml.Nation.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedNation.Name, out Xml.Nation nation))
                        {
                            Game.World.WorldData.Remove<Xml.Nation>(nation.Name);
                            Game.World.WorldData.Set(reloadedNation.Name, reloadedNation);
                            return Success($"Nation {reloadedNation.Name} set to world data");
                        }
                        return Fail($"{args[0]} {args[1]} was not found");
                    }
                case "map":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedMap = Xml.Map.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedMap.Id, out Map map))
                        {

                            var newMap = new Map(reloadedMap, Game.World);
                            Game.World.WorldData.RemoveIndex<Map>(map.Name);
                            Game.World.WorldData.Remove<Map>(map.Id);
                            Game.World.WorldData.SetWithIndex(newMap.Id, newMap, newMap.Name);
                            var mapObjs = map.Objects.ToList();
                            for (var i = 0; i < mapObjs.Count; i++)
                            {
                                var obj = mapObjs[i];
                                map.Remove(obj);
                                if (obj is User usr)
                                {
                                    newMap.Insert(usr, usr.X, usr.Y);
                                }
                                if (obj is Monster mob)
                                {
                                    Game.World.Remove(mob);
                                }
                                if (obj is ItemObject itm)
                                {
                                    Game.World.Remove(itm);
                                }
                            }

                            return Success($"Map {reloadedMap.Name} set to world data");
                        }
                        return Fail($"{args[0]} {args[1]} was not found");
                    }
                case "item":
                    {
                        return Fail("Not yet supported.");
                    }
                case "itemvariant":
                    {
                        return Fail("Not supported.");
                    }
                case "spawngroup":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedSpawnGroup = Xml.SpawnGroup.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedSpawnGroup.Id, out Xml.SpawnGroup spawngroup))
                        {
                            Game.World.WorldData.Remove<Xml.SpawnGroup>(spawngroup.Id);
                            Game.World.WorldData.SetWithIndex(reloadedSpawnGroup.Id, reloadedSpawnGroup, reloadedSpawnGroup.Filename);
                            return Success($"SpawnGroup {reloadedSpawnGroup.Filename} set to world data");
                        }
                        return Fail($"{args[0]} {args[1]} was not found");
                    }
                case "status":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedStatus = Xml.Status.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedStatus.Name, out Xml.Status status))
                        {
                            Game.World.WorldData.Remove<Xml.Status>(status.Name);
                            Game.World.WorldData.Set(reloadedStatus.Name, reloadedStatus);
                            return Success($"Status {reloadedStatus.Name} set to world data");
                        }
                        return Fail($"{args[0]} {args[1]} was not found");
                    }
                case "worldmap":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedWorldMap = Xml.WorldMap.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedWorldMap.Name, out Xml.WorldMap status))
                        {
                            Game.World.WorldData.Remove<Xml.WorldMap>(status.Name);
                            Game.World.WorldData.Set(reloadedWorldMap.Name, reloadedWorldMap);
                            return Success($"WorldMap {reloadedWorldMap.Name} set to world data");
                        }
                        return Fail($"{args[0]} {args[1]} was not found");
                    }
                case "element":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedElementTable = Xml.ElementTable.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue("ElementTable", out Xml.ElementTable table))
                        {
                            Game.World.WorldData.Remove<Xml.ElementTable>("ElementTable");
                            Game.World.WorldData.Set("ElementTable", reloadedElementTable);
                            return Success($"ElementTable set to world data");
                        }
                        return Fail($"{args[0]} {args[1]} was not found");
                    }
                case "localization":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        Game.World.Strings = Xml.LocalizedStrings.LoadFromFile(reloaded);
                        return Success($"Localization strings set to World");
                    }
                default:
                    return Fail("Bad input.");
            }

        }
    }

    class LoadXml : ChatCommand
    {
        public new static string Command = "loadxml";
        public new static string ArgumentText = "<string> type <string> filename";
        public new static string HelpText = "Loads a specified xml file into world data, i.e. \"castable\" \"wizard_psp_srad\" (Valid arguments are: \n\n castable npc item lootset nation map itemvariant spawngroup status worldmap";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (args.Length < 2) return Fail($"Wrong number of arguments supplied.");

            switch (args[0].ToLower())
            {
                case "castable":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedCastable = Xml.Castable.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedCastable.Id, out Xml.Castable castable))
                        {
                            return Fail($"{args[0]} {args[1]} already exists.");
                        }
                        Game.World.WorldData.SetWithIndex(reloadedCastable.Id, reloadedCastable, reloadedCastable.Name);
                        return Success($"Castable {reloadedCastable.Name} set to world data");
                    }
                case "npc":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedNpc = Xml.Npc.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedNpc.Name, out Xml.Npc npc))
                        {
                            return Fail($"{args[0]} {args[1]} already exists.");
                        }
                        Game.World.WorldData.Set(reloadedNpc.Name, reloadedNpc);
                        return Success($"Npc {reloadedNpc.Name} set to world data.");
                    }
                case "lootset":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedLootSet = Xml.LootSet.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedLootSet.Id, out Xml.LootSet lootSet))
                        {
                            return Fail($"{args[0]} {args[1]} already exists.");
                        }
                        Game.World.WorldData.SetWithIndex(reloadedLootSet.Id, reloadedLootSet, reloadedLootSet.Name);
                        return Success($"Npc {reloadedLootSet.Name} set to world data.");
                    }
                case "nation":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedNation = Xml.Nation.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedNation.Name, out Xml.Nation nation))
                        {
                            return Fail($"{args[0]} {args[1]} already exists.");
                        }
                        Game.World.WorldData.Set(reloadedNation.Name, reloadedNation);
                        return Success($"Nation {reloadedNation.Name} set to world data");
                    }
                case "map":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedMap = Xml.Map.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedMap.Id, out Map map))
                        {
                            return Fail($"{args[0]} {args[1]} already exists.");
                        }
                        var newMap = new Map(reloadedMap, Game.World);
                        Game.World.WorldData.SetWithIndex(newMap.Id, newMap, newMap.Name);
                        return Success($"Map {reloadedMap.Name} set to world data");
                    }
                case "item":
                    {
                        return Fail("Not yet supported.");
                    }
                case "itemvariant":
                    {
                        return Fail("Not supported.");
                    }
                case "spawngroup":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedSpawnGroup = Xml.SpawnGroup.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedSpawnGroup.Id, out Xml.SpawnGroup spawngroup))
                        {
                            return Fail($"{args[0]} {args[1]} already exists.");
                        }
                        Game.World.WorldData.SetWithIndex(reloadedSpawnGroup.Id, reloadedSpawnGroup, reloadedSpawnGroup.Filename);
                        return Success($"SpawnGroup {reloadedSpawnGroup.Filename} set to world data");
                    }
                case "status":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedStatus = Xml.Status.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedStatus.Name, out Xml.Status status))
                        {
                            return Fail($"{args[0]} {args[1]} already exists.");
                        }
                        Game.World.WorldData.Set(reloadedStatus.Name, reloadedStatus);
                        return Success($"Status {reloadedStatus.Name} set to world data");
                    }
                case "worldmap":
                    {
                        var reloaded = Game.World.GetXmlFile(args[0], args[1]);
                        var reloadedWorldMap = Xml.WorldMap.LoadFromFile(reloaded);

                        if (Game.World.WorldData.TryGetValue(reloadedWorldMap.Name, out Xml.WorldMap status))
                        {
                            return Fail($"{args[0]} {args[1]} already exists.");
                        }
                        Game.World.WorldData.Set(reloadedWorldMap.Name, reloadedWorldMap);
                        return Success($"WorldMap {reloadedWorldMap.Name} set to world data");
                    }
                default:
                    return Fail("Bad input.");
            }

        }
    }

    class GenerateArmor : ChatCommand
    {
        public static int GeneratedId = 0;
        public new static string Command = "generate";
        public new static string ArgumentText = "<string> type <string> gender <ushort> sprite <ushort> sprite";
        public new static string HelpText = "Used for testing sprite vs display sprite. armor Female 1 1";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (args.Length < 4) return Fail($"Wrong number of arguments supplied.");
            ushort sprite;
            ushort displaysprite;
            if(!ushort.TryParse(args[2], out sprite)) return Fail($"Sprite must be a number.");
            if (!ushort.TryParse(args[3], out displaysprite)) return Fail($"Displaysprite must be a number.");
            switch (args[0].ToLower())
            {
                case "armor":
                    {
                        var item = new Xml.Item()
                        {
                            Name = "GeneratedArmor" + GeneratedId,
                            Properties = new Xml.ItemProperties()
                            {
                                Stackable = new Xml.Stackable() { Max = 1 },
                                Physical = new Xml.Physical()
                                {
                                    Durability = 1000,
                                    Value = 1,
                                    Weight = 1
                                },
                                Restrictions = new Xml.ItemRestrictions()
                                {
                                    Gender = (Xml.Gender)Enum.Parse(typeof(Xml.Gender), args[1]),
                                    Level = new Xml.RestrictionsLevel()
                                    {
                                        Min = 1
                                    }
                                },
                                Appearance = new Xml.Appearance()
                                {
                                    BodyStyle = (Xml.ItemBodyStyle)Enum.Parse(typeof(Xml.ItemBodyStyle), args[1]),
                                    Sprite = sprite,
                                    DisplaySprite = displaysprite
                                },
                                Equipment = new Xml.Equipment()
                                {
                                    Slot = Xml.EquipmentSlot.Armor
                                }
                            }
                        };
                        Game.World.WorldData.SetWithIndex<Xml.Item>(item.Id, item, item.Name);
                        user.AddItem(item.Name, 1);
                    }
                    break;
                case "coat":
                    {
                        var item = new Xml.Item()
                        {
                            Name = "GeneratedArmor" + GeneratedId,
                            Properties = new Xml.ItemProperties()
                            {
                                Stackable = new Xml.Stackable() { Max = 1 },
                                Physical = new Xml.Physical()
                                {
                                    Durability = 1000,
                                    Value = 1,
                                    Weight = 1
                                },
                                Restrictions = new Xml.ItemRestrictions()
                                {
                                    Gender = (Xml.Gender)Enum.Parse(typeof(Xml.Gender), args[1]),
                                    Level = new Xml.RestrictionsLevel()
                                    {
                                        Min = 1
                                    }
                                },
                                Appearance = new Xml.Appearance()
                                {
                                    BodyStyle = (Xml.ItemBodyStyle)Enum.Parse(typeof(Xml.ItemBodyStyle), args[1]),
                                    Sprite = sprite,
                                    DisplaySprite = displaysprite
                                },
                                Equipment = new Xml.Equipment()
                                {
                                    Slot = Xml.EquipmentSlot.Trousers
                                }
                            }
                        };
                        Game.World.WorldData.SetWithIndex<Xml.Item>(item.Id, item, item.Name);
                        user.AddItem(item.Name, 1);
                    }
                    break;
            }
            GeneratedId++;
            return Success($"GeneratedArmor{GeneratedId -1} added to World Data.");
        }
    }
}


