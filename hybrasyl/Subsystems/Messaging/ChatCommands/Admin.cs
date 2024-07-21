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

using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Internals.Metafiles;
using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Xml.Objects;
using System;
using System.IO;
using System.Linq;
using System.Net;
using Creature = Hybrasyl.Xml.Objects.Creature;
using MessageType = Hybrasyl.Internals.Enums.MessageType;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;
// Various admin commands are implemented here.

internal class ShowCookies : ChatCommand
{
    public new static string Command = "showcookies";
    public new static string ArgumentText = "<string playername>";
    public new static string HelpText = "Show permanent and session cookies set for a specified player";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out User target))
        {
            var cookies = $"User {target.Name} Cookie List\n\n---Permanent Cookies---\n";
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

internal class ClearCookie : ChatCommand
{
    public new static string Command = "clearcookie";
    public new static string ArgumentText = "<string playername> <string cookie>";
    public new static string HelpText = "Clear a given (permament) cookie for a specified player";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out User target))
        {
            if (target.HasCookie(args[1]))
            {
                target.DeleteCookie(args[1]);
                return Success($"User {target.Name}: cookie {args[1]} deleted");
            }

            return Fail($"User {args[0]} doesn't have cookie {args[1]}");
        }

        return Fail($"User {args[0]} not logged in");
    }
}

internal class DestroyItemCommand : ChatCommand
{
    public new static string Command = "destroyitem";
    public new static string ArgumentText = "<byte slot>";
    public new static string HelpText = "Destroy the inventory item in the specified slot";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (byte.TryParse(args[0], out var slot))
        {
            user.RemoveItem(slot);
            return Success("Destroyed.");
        }

        return Fail("That's not a slot.");
    }
}

internal class ClearSessionCookie : ChatCommand
{
    public new static string Command = "clearsessioncookie";
    public new static string ArgumentText = "<string playername> <string cookie>";
    public new static string HelpText = "Clear a given session cookie for a specified player";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out User target))
        {
            if (target.HasSessionCookie(args[1]))
            {
                target.DeleteSessionCookie(args[1]);
                return Success($"User {target.Name}: session cookie {args[1]} deleted");
            }

            return Fail($"User {args[0]} doesn't have session cookie {args[1]}");
        }

        return Fail($"User {args[0]} not logged in");
    }
}

internal class DumpMetadata : ChatCommand
{
    public new static string Command = "dumpmetadata";
    public new static string ArgumentText = "<string metadatafile>";
    public new static string HelpText = "Dump (in hex) a metadata file ";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.ContainsKey<CompiledMetafile>(args[0]))
        {
            var file = Game.World.WorldState.Get<CompiledMetafile>(args[0]);
            var filepath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Hybrasyl");
            File.WriteAllBytes($"{filepath}\\{args[0]}.mdf", file.Data);
            return Success($"{filepath}\\{args[0]}.mdf written to disk");
        }

        return Fail("Look chief idk about all that");
    }
}

internal class SetCookie : ChatCommand
{
    public new static string Command = "setcookie";
    public new static string ArgumentText = "<string playername> <string cookie> <string value>";
    public new static string HelpText = "Set a given (permament) cookie for a specified player";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out User target))
        {
            target.SetCookie(args[1], args[2]);
            return Success($"User {target.Name}: cookie {args[1]} set");
        }

        return Fail($"User {args[0]} not logged in");
    }
}

internal class Immortal : ChatCommand
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
        return Success("You return to the realm of the mortal.");
    }
}

internal class ShowEphemeral : ChatCommand
{
    public new static string Command = "showephemeral";
    public new static string ArgumentText = "<string mundane>";
    public new static string HelpText = "Show ephemeral values set for a specified mundane";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out Merchant merchant))
        {
            var ephemerals = $"Mundane {merchant.Name} Ephemeral Store\n\n";
            foreach (var kv in (merchant as IEphemeral).GetEphemeralValues())
                ephemerals = $"{ephemerals}\n{kv.Item1} : {kv.Item2}\n";
            return Success($"{ephemerals}", MessageTypes.SLATE_WITH_SCROLLBAR);
        }

        return Fail($"Mundane {args[0]} could not be found");
    }
}

internal class SetEphemeral : ChatCommand
{
    public new static string Command = "setephemeral";
    public new static string ArgumentText = "<string mundane> <string key> <string value>";
    public new static string HelpText = "Set a given ephemeral store value for a specified mundane";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out Merchant merchant))
        {
            (merchant as IEphemeral).SetEphemeral(args[1], args[2]);
            return Success($"{merchant.Name}: {args[1]} set to {args[2]}");
        }

        return Fail($"NPC {args[0]} not found.");
    }
}

internal class ClearEphemeral : ChatCommand
{
    public new static string Command = "clearephemeral";
    public new static string ArgumentText = "<string mundane> <string key>";
    public new static string HelpText = "Clear a given ephemeral store value for a specified mundane";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out Merchant merchant))
        {
            (merchant as IEphemeral).ClearEphemeral(args[1]);
            return Success($"{merchant.Name}: {args[1]} set to {args[2]}");
        }

        return Fail($"NPC {args[0]} not found.");
    }
}

internal class SetSessionCookie : ChatCommand
{
    public new static string Command = "setsessioncookie";
    public new static string ArgumentText = "<string playername> <string cookie> <string value>";
    public new static string HelpText = "Set a given (permament) cookie for a specified player";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out User target))
        {
            target.SetSessionCookie(args[1], args[2]);
            return Success($"User {target.Name}: cookie {args[1]} set");
        }

        return Fail($"User {args[0]} not logged in");
    }
}

internal class DeleteSessionCookie : ChatCommand
{
    public new static string Command = "deletesessioncookie";
    public new static string ArgumentText = "<string cookie> | <string playername> <string cookie>";

    public new static string HelpText =
        "Clear (delete) a given session (transient) scripting cookie. This is useful when working with scripts that modify player state.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length == 1)
        {
            user.DeleteSessionCookie(args[0]);
            return Success($"Session flag {args[0]} deleted.");
        }

        var target = Game.World.WorldState.Get<User>(args[0]);

        if (target.AuthInfo.IsExempt)
            return Fail($"User {target.Name} is exempt from your meddling.");
        target.DeleteSessionCookie(args[1]);
        return Success($"Player {target.Name}: flag {args[1]} removed.");
    }
}

internal class DeleteCookie : ChatCommand
{
    public new static string Command = "deletecookie";
    public new static string ArgumentText = "<string cookie> | <string playername> <string cookie>";

    public new static string HelpText =
        "Clear (delete) a given scripting cookie. This is useful when working with scripts that modify player state.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length == 1)
        {
            user.DeleteCookie(args[0]);
            return Success($"Session flag {args[0]} deleted.");
        }

        var target = Game.World.WorldState.Get<User>(args[0]);

        if (target.AuthInfo.IsExempt)
            return Fail($"User {target.Name} is exempt from your meddling.");
        target.DeleteCookie(args[1]);
        return Success($"Player {target.Name}: flag {args[1]} removed.");
    }
}

internal class SummonCommand : ChatCommand
{
    public new static string Command = "summon";
    public new static string ArgumentText = "<string playerName>";
    public new static string HelpText = "Summon a specified player to you";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.TryGetActiveUser(args[0], out var target))
        {
            if (target.AuthInfo.IsExempt)
                return Fail($"User {target.Name} is exempt from your meddling.");

            target.Teleport(user.Location.MapId, user.Location.X, user.Location.Y);
            return Success($"User {target.Name} has been summoned.");
        }

        return Fail($"User {args[0]} not logged in");
    }
}

internal class KickCommand : ChatCommand
{
    public new static string Command = "kick";
    public new static string ArgumentText = "<string playerName>";
    public new static string HelpText = "Kick a specified player off the server";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.TryGetActiveUser(args[0], out var target))
        {
            if (target.AuthInfo.IsExempt)
                return Fail($"User {target.Name} is exempt from your meddling");
            target.Logoff(true);

            return Success($"User {target.Name} was kicked.");
        }

        return Fail($"User {args[0]} not logged in");
    }
}

internal class GcmCommand : ChatCommand
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
                    ((IPEndPoint)pair.Value.Socket.RemoteEndPoint).Address,
                    ((IPEndPoint)pair.Value.Socket.RemoteEndPoint).Port, serverType);
            }
            catch
            {
                gcmContents = gcmContents + string.Format("{0}:{1} disposed\n", pair.Key, serverType);
            }
        }

        foreach (var tehuser in Game.World.WorldState.Values<User>()) userContents = userContents + tehuser.Name + "\n";

        // Report to the end user
        return Success($"{gcmContents}\n\n{userContents}",
            MessageTypes.SLATE_WITH_SCROLLBAR);
    }
}

internal class MuteCommand : ChatCommand
{
    public new static string Command = "mute";
    public new static string ArgumentText = "<string playerName>";
    public new static string HelpText = "Mute the specified player (whisper/shout/talk).";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.TryGetActiveUser(args[0], out var target))
        {
            if (target.AuthInfo.IsExempt)
                return Fail($"User {target.Name} is exempt from your meddling.");
            target.IsMuted = true;

            return Success($"User {target.Name} was muted.");
        }

        return Fail($"User {args[0]} not logged in.");
    }
}

internal class UnmuteCommand : ChatCommand
{
    public new static string Command = "unmute";
    public new static string ArgumentText = "<string playerName>";
    public new static string HelpText = "Unmute the specified player.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.TryGetActiveUser(args[0], out var target))
        {
            if (target.AuthInfo.IsExempt)
                return Fail($"User {target.Name} is exempt from your meddling");
            target.IsMuted = false;

            return Success($"User {target.Name} was unmuted.");
        }

        return Fail($"User {args[0]} not logged in");
    }
}

internal class ReloadCommand : ChatCommand
{
    public new static string Command = "reload";
    public new static string ArgumentText = "none";
    public new static string HelpText = "Reload world data.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args) =>
        Success("This feature is not yet implemented.");
}

internal class WallsCommand : ChatCommand
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
        user.UpdateAttributes(StatUpdateFlags.Primary);
        return msg;
    }
}

internal class ShutdownCommand : ChatCommand
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

            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.ShutdownServer, user.Name, delay));
            return Success("Shutdown request submitted.");
        }

        return Fail("You have to really mean it.");
    }
}

//class ScriptingCommand : ChatCommand
//{
//    public new static string Command = "scripting";
//    public new static string ArgumentText = "<reload|disable|enable|status> <string scriptname>";
//    public new static string HelpText = "Reload, disable, enable or request status on the specified script.";
//    public new static bool Privileged = true;

//    public new static ChatCommandResult Run(User user, params string[] args)
//    {

//        if (Game.World.ScriptProcessor.TryGetScript(args[1].Trim(), out Script script))
//        {
//            switch (args[0].ToLower())
//            {
//                case "reload":
//                {
//                    script.Disabled = true;
//                    if (script.Run().Result != ScriptResult.Success)
//                        return Fail($"Script {script.Name}: load/parse error, check scripting log.");
//                    script.Disabled = false;
//                    return Success($"Script {script.Name}: reloaded.");

//                }
//                case "enable":
//                    script.Disabled = false;
//                    return Success($"Script {script.Name}: enabled.");
//                case "disable":
//                    script.Disabled = true;
//                    return Success($"Script {script.Name}: disabled.");
//                case "status":
//                {
//                    var scriptStatus = string.Format("{0}:", script.Name);
//                    string errorSummary = "--- Error Summary ---\n";

//                    errorSummary = script.LastExecutionResult.Result == ScriptResult.Success ? 
//                        $"{errorSummary} no errors": $"{errorSummary} result {script.LastExecutionResult.Result}, {script.LastExecutionResult.Error}";

//                    // Report to the end user
//                    return Success($"{scriptStatus}\n\n{errorSummary}", MessageTypes.SLATE_WITH_SCROLLBAR);
//                }
//            }
//        }
//        return Fail($"Script {args[1].Trim()}: not found.");
//    }
//}

internal class NpcstatusCommand : ChatCommand
{
    public new static string Command = "npcstatus";
    public new static string ArgumentText = "<string npcname>";
    public new static string HelpText = "Display detailed debugging info for the given NPC";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args) =>
        Game.World.WorldState.TryGetValue(args[0], out Merchant merchant)
            ? Success(merchant.Status(), (byte)MessageType.SlateScrollbar)
            : Fail($"NPC {args[0]} not found.");
}

internal class ReloadnpcCommand : ChatCommand
{
    public new static string Command = "reloadnpc";
    public new static string ArgumentText = "<string npcname>";
    public new static string HelpText = "Reload the given NPC (dump the script and reload from disk)";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out Merchant merchant))
        {
            if (Game.World.ScriptProcessor.TryGetScript(merchant.Name, out var script))
            {
                script.Reload();
                merchant.Ready = false; // Force reload next time NPC processes an interaction
                merchant.Say("...What? What happened?");
                return Success($"NPC {args[0]} - script {script.Name} reloaded. Clicking should re-run OnSpawn.");
            }

            return Fail("NPC found but script not found...?");
        }

        return Fail($"NPC {args[0]} not found.");
    }
}

internal class TeleportCommand : ChatCommand
{
    public new static string Command = "teleport";

    public new static string ArgumentText =
        "<string playername> | <string npcname> | <string mapname> <byte x> <byte y> | <uint mapnumber> <byte x> <byte y>";

    public new static string HelpText = "Teleport to the specified player, or the given map number and coordinates.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length == 1)
        {
            // Either user or npc
            if (Game.World.WorldState.ContainsKey<User>(args[0]))
            {
                var target = Game.World.WorldState.Get<User>(args[0]);
                user.Teleport(target.Location.MapId, target.Location.X, target.Location.Y);
                return Success(
                    $"Teleported to {target.Name} - {target.Map.Name} ({target.Location.X},{target.Location.Y})");
            }

            if (Game.World.WorldState.TryGetValue(args[0], out Merchant merchant))
            {
                var (x, y) = merchant.Map.FindEmptyTile((byte)(merchant.Map.X / 2), (byte)(merchant.Map.Y / 2));
                if (x > 0 && y > 0)
                {
                    user.Teleport(merchant.Map.Id, x, y);
                    return Success($"Teleported to {merchant.Name} - {merchant.Map.Name} ({x}, {y})");
                }

                return Fail("Sorry, something went wrong (empty tile could not be found..?)");
            }

            return Fail($"Sorry, user or npc {args[0]} not found.");
        }

        ushort? mapnum = null;
        if (ushort.TryParse(args[0], out var num))
            mapnum = num;
        else if (Game.World.WorldState.TryGetValueByIndex(args[0], out MapObject targetMap))
            mapnum = targetMap.Id;
        else
            return Fail("Unknown map id or map name");

        if (Game.World.WorldState.TryGetValue(mapnum, out MapObject map))
        {
            if (byte.TryParse(args[1], out var x) && byte.TryParse(args[2], out var y))
            {
                if (x < map.X && y < map.Y)
                {
                    user.Teleport(map.Id, x, y);
                    return Success($"Teleported to {map.Name} ({x},{y}).");
                }

                return Fail("Invalid x/y specified (hint: mapsize is {map.X}x{map.Y})");
            }

            return Fail("Couldn't parse map number or coordinates (uint / byte / byte)");
        }

        return Fail("Map number {mapnum} not found");
    }
}

internal class QueuedepthCommand : ChatCommand
{
    public new static string Command = "queuedepth";
    public new static string ArgumentText = "None";
    public new static string HelpText = "Display current queue depths.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args) =>
        Success(
            $"Packet Queue Depth: {World.MessageQueue.Count}\n\nControl Message Queue Depth: {World.ControlMessageQueue.Count}",
            MessageTypes.SLATE_WITH_SCROLLBAR);
}

internal class ResurrectCommand : ChatCommand
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

internal class ReturnCommand : ChatCommand
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

internal class DebugCommand : ChatCommand
{
    public new static string Command = "debug";
    public new static string ArgumentText = "None";

    public new static string HelpText =
        "Toggle whether or not debug logging is enabled on the server. WARNING: It's a lot of logs.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args) =>
        Success(GameLog.ToggleDebug() ? "Debugging enabled" : "Debugging disabled");
}

internal class LogLevelCommand : ChatCommand
{
    public new static string Command = "loglevel";
    public new static string ArgumentText = "<string type> <string loglevel>";

    public new static string HelpText =
        "Set the log level for a specific logging type. Use /loginfo to get a list of types and levels.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Enum.TryParse<LogType>(args[0], out var logType) && Enum.TryParse<LogLevel>(args[1], out var logLevel))
        {
            if (!GameLog.HasLogger(logType)) return Fail("There is not a separate logger for {logType}.");
            GameLog.SetLevel(logType, logLevel);
            return Success($"{logType} set to {logLevel}");
        }

        return Fail("Log type or log level was invalid. Use /loginfo to get a valid list.");
    }
}

internal class LogInfoCommand : ChatCommand
{
    public new static string Command = "loginfo";
    public new static string ArgumentText = "None";
    public new static string HelpText = "List all mobs on the current map.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var txt = "Current Logging Configuration\n-----------------------------\n";
        foreach (var (type, logger) in GameLog.Loggers)
            txt = $"{txt}\n{type}: {logger.Level.MinimumLevel} ->\n {logger.Path.Replace("\\", "/")}\n";

        txt = $"{txt}\nAvailable Log Types:\n\n";
        txt = Enum.GetValues<LogType>().Aggregate(txt, func: (current, strEnum) => $"{current} {strEnum}");
        txt = $"{txt}\nAvailable Log Levels:\n\n";
        txt = Enum.GetValues<LogLevel>().Aggregate(txt, func: (current, strEnum) => $"{current} {strEnum}");
        return Success(txt, (byte)MessageType.SlateScrollbar);
    }
}

internal class ListMobCommand : ChatCommand
{
    public new static string Command = "listmob";
    public new static string ArgumentText = "None";
    public new static string HelpText = "List all mobs on the current map.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var moblist = string.Format("{0,25}", "Name") + " " + string.Format("{0,40}", "Details") + "\n";
        foreach (var mob in user.Map.Objects.Where(predicate: x => x is Monster).Select(selector: y => y as Monster))
        {
            var mobdetails = $"({mob.X},{mob.Y}) {mob.Stats}";
            moblist += string.Format("{0,25}", "Name") + " " + string.Format("{0,40}", mobdetails) + "\n";
        }

        return Success(moblist, MessageTypes.SLATE_WITH_SCROLLBAR);
    }
}

internal class SpawnCommand : ChatCommand
{
    public new static string Command = "spawn";
    public new static string ArgumentText = "<string creature> <string behaviorSet> <level>";
    public new static string HelpText = "Spawn a monster with the specified name, behavior set, and level.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldData.TryGetValue(args[0], out Creature creature) &&
            Game.World.WorldData.TryGetValue(args[1], out CreatureBehaviorSet cbs))
        {
            if (byte.TryParse(args[2], out var x))
            {
                var newMob = new Monster(creature, SpawnFlags.Active, x, null, cbs);
                user.World.Insert(newMob);
                user.Map.Insert(newMob, user.X, user.Y);
                return Success($"{creature.Name} spawned.");
            }

            return Fail("Level must be a byte between 1 and 255");
        }

        return Fail($"Creature {args[0]} not found");
    }
}

//internal class ReloadXml : ChatCommand
//{
//    public new static string Command = "reloadxml";
//    public new static string ArgumentText = "<string> type <string> filename";

//    public new static string HelpText =
//        "Reloads a specified xml file into world data, i.e. \"castable\" \"all_psk_assail\" (Valid arguments are:\ncastable npc item element lootset nation map itemvariant spawngroup status worldmap localization";

//    public new static bool Privileged = true;

//    public new static ChatCommandResult Run(User user, params string[] args)
//    {
//        if (args.Length < 2) return Fail("Wrong number of arguments supplied.");

//        switch (args[0].ToLower())
//        {
//            case "castable":
//            {
//                //Game.World.Reload(IXmlReloadable);
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedCastable = Castable.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedCastable.Id, out Castable castable))
//                {
//                    Game.World.WorldState.Remove<Castable>(castable.Id);
//                    Game.World.WorldState.SetWithIndex(reloadedCastable.Id, reloadedCastable, reloadedCastable.Name);
//                    foreach (var activeuser in Game.World.ActiveUsers)
//                        if (reloadedCastable.Book == Xml.Objects.Book.PrimarySkill ||
//                            reloadedCastable.Book == Xml.Objects.Book.SecondarySkill ||
//                            reloadedCastable.Book == Xml.Objects.Book.UtilitySkill)
//                        {
//                            if (activeuser.SkillBook.Contains(reloadedCastable.Id))
//                                activeuser.SkillBook[activeuser.SkillBook.SlotOf(reloadedCastable.Id)].Castable =
//                                    reloadedCastable;
//                        }
//                        else
//                        {
//                            if (activeuser.SpellBook.Contains(reloadedCastable.Id))
//                                activeuser.SpellBook[activeuser.SpellBook.SlotOf(reloadedCastable.Id)].Castable =
//                                    reloadedCastable;
//                        }

//                    return Success($"Castable {reloadedCastable.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "npc":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedNpc = Npc.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedNpc.Name, out Npc npc))
//                {
//                    Game.World.WorldState.Remove<Npc>(npc.Name);
//                    Game.World.WorldState.Set(reloadedNpc.Name, reloadedNpc);
//                    return Success($"Npc {reloadedNpc.Name} set to world data. Reload NPC to activate.");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "lootset":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedLootSet = LootSet.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedLootSet.Id, out LootSet lootSet))
//                {
//                    Game.World.WorldState.Remove<LootSet>(lootSet.Id);
//                    Game.World.WorldState.SetWithIndex(reloadedLootSet.Id, reloadedLootSet, reloadedLootSet.Name);
//                    return Success($"LootSet {reloadedLootSet.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "nation":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedNation = Nation.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedNation.Name, out Nation nation))
//                {
//                    Game.World.WorldState.Remove<Nation>(nation.Name);
//                    Game.World.WorldState.Set(reloadedNation.Name, reloadedNation);
//                    return Success($"Nation {reloadedNation.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "map":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedMap = Xml.Objects.Map.LoadFromFile(reloaded);

//                if (!Game.World.WorldState.TryGetValue(reloadedMap.Id, out Map map))
//                    return Fail($"{args[0]} {args[1]} was not found");

//                var newMap = new Map(reloadedMap, Game.World);
//                Game.World.WorldState.RemoveIndex<Map>(map.Name);
//                Game.World.WorldState.Remove<Map>(map.Id);
//                var mapObjs = map.Objects.ToList();
//                foreach (var obj in mapObjs) 
//                {
//                    map.Remove(obj);
//                    switch (obj)
//                    {
//                        case User usr:
//                            newMap.Insert(usr, usr.X, usr.Y);
//                            break;
//                        case Monster mob:
//                            Game.World.Remove(mob);
//                            break;
//                        case ItemObject itm:
//                            Game.World.Remove(itm);
//                            break;
//                        case Merchant npc:
//                            npc.Map = newMap;
//                            break;
//                    }
//                }
//                Game.World.WorldState.SetWithIndex(newMap.Id, newMap, newMap.Name);

//                return Success($"Map {reloadedMap.Name} set to world data");

//            }
//            case "item":
//            {
//                return Fail("Not yet supported.");
//            }
//            case "itemvariant":
//            {
//                return Fail("Not supported.");
//            }
//            case "spawngroup":
//            {
//                return Fail("Not supported yet");
//            }
//            case "status":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedStatus = Status.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedStatus.Name, out Status status))
//                {
//                    Game.World.WorldState.Remove<Status>(status.Name);
//                    Game.World.WorldState.Set(reloadedStatus.Name, reloadedStatus);
//                    return Success($"Status {reloadedStatus.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "worldmap":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedWorldMap = Xml.Objects.WorldMap.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedWorldMap.Name, out Xml.Objects.WorldMap status))
//                {
//                    Game.World.WorldState.Remove<Xml.Objects.WorldMap>(status.Name);
//                    Game.World.WorldState.Set(reloadedWorldMap.Name, reloadedWorldMap);
//                    return Success($"WorldMap {reloadedWorldMap.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "element":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedElementTable = ElementTable.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue("ElementTable", out ElementTable table))
//                {
//                    Game.World.WorldState.Remove<ElementTable>("ElementTable");
//                    Game.World.WorldState.Set("ElementTable", reloadedElementTable);
//                    return Success("ElementTable set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "localization":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                Game.World.Strings = LocalizedStringGroup.LoadFromFile(reloaded);
//                return Success("Localization strings set to World");
//            }
//            default:
//                return Fail("Bad input.");
//        }
//    }
//}

//internal class LoadXml : ChatCommand
//{
//    public new static string Command = "loadxml";
//    public new static string ArgumentText = "<string> type <string> filename";

//    public new static string HelpText =
//        "Loads a specified xml file into world data, i.e. \"castable\" \"wizard_psp_srad\" (Valid arguments are: \n\n castable npc item lootset nation map itemvariant spawngroup status worldmap";

//    public new static bool Privileged = true;

//    public new static ChatCommandResult Run(User user, params string[] args)
//    {
//        if (args.Length < 2) return Fail("Wrong number of arguments supplied.");

//        switch (args[0].ToLower())
//        {
//            case "castable":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedCastable = Castable.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedCastable.Id, out Castable castable))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.SetWithIndex(reloadedCastable.Id, reloadedCastable, reloadedCastable.Name);
//                return Success($"Castable {reloadedCastable.Name} set to world data");
//            }
//            case "npc":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedNpc = Npc.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedNpc.Name, out Npc npc))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.Set(reloadedNpc.Name, reloadedNpc);
//                return Success($"Npc {reloadedNpc.Name} set to world data.");
//            }
//            case "lootset":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedLootSet = LootSet.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedLootSet.Id, out LootSet lootSet))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.SetWithIndex(reloadedLootSet.Id, reloadedLootSet, reloadedLootSet.Name);
//                return Success($"Npc {reloadedLootSet.Name} set to world data.");
//            }
//            case "nation":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedNation = Nation.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedNation.Name, out Nation nation))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.Set(reloadedNation.Name, reloadedNation);
//                return Success($"Nation {reloadedNation.Name} set to world data");
//            }
//            case "map":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedMap = Xml.Objects.Map.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedMap.Id, out Map map))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                var newMap = new Map(reloadedMap, Game.World);
//                Game.World.WorldState.SetWithIndex(newMap.Id, newMap, newMap.Name);
//                return Success($"Map {reloadedMap.Name} set to world data");
//            }
//            case "item":
//            {
//                return Fail("Not yet supported.");
//            }
//            case "itemvariant":
//            {
//                return Fail("Not supported.");
//            }
//            case "spawngroup":
//            {
//                return Fail("Not supported, yet");
//            }
//            case "status":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedStatus = Status.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedStatus.Name, out Status status))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.Set(reloadedStatus.Name, reloadedStatus);
//                return Success($"Status {reloadedStatus.Name} set to world data");
//            }
//            case "worldmap":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedWorldMap = Xml.Objects.WorldMap.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedWorldMap.Name, out Xml.Objects.WorldMap status))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.Set(reloadedWorldMap.Name, reloadedWorldMap);
//                return Success($"WorldMap {reloadedWorldMap.Name} set to world data");
//            }
//            default:
//                return Fail("Bad input.");
//        }
//    }
//}

internal class GenerateArmor : ChatCommand
{
    public static int GeneratedId;
    public new static string Command = "generate";
    public new static string ArgumentText = "<string> type <string> gender <ushort> sprite <ushort> sprite";
    public new static string HelpText = "Used for testing sprite vs display sprite. armor Female 1 1";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length < 4) return Fail("Wrong number of arguments supplied.");
        ushort sprite;
        ushort displaysprite;
        if (!ushort.TryParse(args[2], out sprite)) return Fail("Sprite must be a number.");
        if (!ushort.TryParse(args[3], out displaysprite)) return Fail("Displaysprite must be a number.");
        switch (args[0].ToLower())
        {
            case "armor":
                {
                    var item = new Item
                    {
                        Name = "GeneratedArmor" + GeneratedId,
                        Properties = new ItemProperties
                        {
                            Stackable = new Stackable { Max = 1 },
                            Physical = new Physical
                            {
                                Durability = 1000,
                                Value = 1,
                                Weight = 1
                            },
                            Restrictions = new ItemRestrictions
                            {
                                Gender = (Gender)Enum.Parse(typeof(Gender), args[1]),
                                Level = new RestrictionsLevel
                                {
                                    Min = 1
                                }
                            },
                            Appearance = new Appearance
                            {
                                BodyStyle = (ItemBodyStyle)Enum.Parse(typeof(ItemBodyStyle), args[1]),
                                Sprite = sprite,
                                DisplaySprite = displaysprite
                            },
                            Equipment = new Equipment
                            {
                                Slot = EquipmentSlot.Armor
                            }
                        }
                    };
                    Game.World.WorldData.AddWithIndex(item, item.Id, item.Name);
                    user.AddItem(item.Name);
                }
                break;
            case "coat":
                {
                    var item = new Item
                    {
                        Name = "GeneratedArmor" + GeneratedId,
                        Properties = new ItemProperties
                        {
                            Stackable = new Stackable { Max = 1 },
                            Physical = new Physical
                            {
                                Durability = 1000,
                                Value = 1,
                                Weight = 1
                            },
                            Restrictions = new ItemRestrictions
                            {
                                Gender = (Gender)Enum.Parse(typeof(Gender), args[1]),
                                Level = new RestrictionsLevel
                                {
                                    Min = 1
                                }
                            },
                            Appearance = new Appearance
                            {
                                BodyStyle = (ItemBodyStyle)Enum.Parse(typeof(ItemBodyStyle), args[1]),
                                Sprite = sprite,
                                DisplaySprite = displaysprite
                            },
                            Equipment = new Equipment
                            {
                                Slot = EquipmentSlot.Trousers
                            }
                        }
                    };
                    Game.World.WorldData.AddWithIndex(item, item.Id, item.Name);
                    user.AddItem(item.Name);
                }
                break;
        }

        GeneratedId++;
        return Success($"GeneratedArmor{GeneratedId - 1} added to World Data.");
    }
}