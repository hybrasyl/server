using Hybrasyl.Creatures;
using Hybrasyl.Objects;
using Hybrasyl.Scripting;
using System;
using System.Collections.Generic;
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
                return Success($"User {target.Name} Cookies\n\n{target.GetCookies()}\n\nSession Cookies\n\n{target.GetSessionCookies()}", MessageTypes.SLATE_WITH_SCROLLBAR);
            }
            return Fail($"User {args[0]} not logged in");
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

            if (Game.World.WorldData.TryGetValue(args[0], out Creatures.Creature creature))
            {
                Spawn spawn = new Spawn();
                spawn.Castables = new List<Castable>();
                spawn.Stats.Hp = 100;
                spawn.Stats.Mp = 100;
                spawn.Stats.Str = 3;
                spawn.Stats.Int = 3;
                spawn.Stats.Wis = 3;
                spawn.Stats.Con = 3;
                spawn.Stats.Dex = 3;
                spawn.Loot.Xp = new LootXp            
                {
                    Min = 1,
                    Max = 1
                };
                spawn.Loot.Gold = new LootGold
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
                    spawn.Loot.Xp.Min = byte.Parse(args[8]);
                    spawn.Loot.Xp.Max = byte.Parse(args[8]);
                }
                if (args.Length >= 10)
                {
                    spawn.Loot.Gold.Min = byte.Parse(args[9]);
                    spawn.Loot.Gold.Max = byte.Parse(args[9]);
                }
                Monster newMob = new Monster(creature, spawn, user.Location.MapId);
                user.World.Insert(newMob);
                user.Map.Insert(newMob, user.X, user.Y);
                return Success($"{creature.Name} spawned.");
            }
            else return Fail("Creature {args[0]} not found");

        }

    }
}


