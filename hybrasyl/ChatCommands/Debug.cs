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

using System.Drawing;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.ChatCommands;

internal class ClearDialogCommand : ChatCommand
{
    public new static string Command = "cleardialog";
    public new static string ArgumentText = "<string username>";
    public new static string HelpText = "Completely clear the dialog state for a given user.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (!Game.World.WorldState.ContainsKey<User>(args[0]))
            return Fail($"User {args[0]} not logged in");

        var target = Game.World.WorldState.Get<User>(args[0]);

        if (target.AuthInfo.IsExempt)
            return Fail($"User {target.Name} is exempt from your meddling.");
        target.ClearDialogState();

        return Success($"User {target.Name}: dialog state cleared.");
    }
}

internal class MapDebugCommand : ChatCommand
{
    public new static string Command = "mapdebug";
    public new static string ArgumentText = "";
    public new static string HelpText = "Turn on map debugging for the current map.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        user.Map.SpawnDebug = !user.Map.SpawnDebug;
        var str = user.Map.SpawnDebug ? "on" : "off";
        return Success($"Map debugging for {user.Map.Name}: {str}");
    }
}

internal class MapSpawnToggleCommand : ChatCommand
{
    public new static string Command = "mapspawntoggle";
    public new static string ArgumentText = "";
    public new static string HelpText = "Toggle spawning for the current map.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        user.Map.SpawningDisabled = !user.Map.SpawningDisabled;
        var str = user.Map.SpawningDisabled ? "on" : "off";
        return Success($"Spawning on {user.Map.Name}: {str}");
    }
}

internal class SpawnToggleCommand : ChatCommand
{
    public new static string Command = "spawntoggle";
    public new static string ArgumentText = "<string spawngroup>";
    public new static string HelpText = "Toggle whether the specified spawngroup is enabled or disabled.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldData.TryGetValueByIndex(args[0], out SpawnGroup group))
        {
            group.Disabled = !group.Disabled;
            var str = group.Disabled ? "on" : "off";
            return Success($"Spawngroup {args[0]}: spawning {str}");
        }

        return Fail($"Spawngroup {args[0]} not found");
    }
}


internal class RepopCommand : ChatCommand
{
    public new static string Command = "repop";
    public new static string ArgumentText = "";
    public new static string HelpText = "Helps ghosts find their way home";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.ActiveConfiguration.Handlers?.Death == null)
            return Fail("Death is currently disabled.");

        if (string.IsNullOrWhiteSpace(Game.ActiveConfiguration.Handlers.Death.Map?.Value))
            return Fail("Death map not defined.");

        if (user.Location.Map.Name != Game.ActiveConfiguration.Handlers.Death.Map.Value)
        {
            GameLog.UserActivityWarning($"User {user.Name}: /repop usage, current map {user.Location.Map.Name}, last hit by {user.LastHitter?.Name ?? "unknown"}, stats {user.Stats}");
            if (user.Condition.Alive)
                return Fail("You're not dead.");
            user.Teleport(Game.ActiveConfiguration.Handlers.Death.Map.Value,
                Game.ActiveConfiguration.Handlers.Death.Map.X,
                Game.ActiveConfiguration.Handlers.Death.Map.Y);
            if (user.Map.Name != Game.ActiveConfiguration.Handlers.Death.Map.Value)
                GameLog.UserActivityFatal($"User {user.Name}: teleported, but not in {Game.ActiveConfiguration.Handlers.Death.Map.Value}...?");
        }
        else
            return Fail($"You are already in {Game.ActiveConfiguration.Handlers.Death.Map.Value}.");
        
        return Success("You are where you should be.");
    }
}


internal class StuckCommand : ChatCommand
{
    public new static string Command = "stuck";
    public new static string ArgumentText = "<string reason>";
    public new static string HelpText = "Use if you are stuck and cannot move. Abuse of this command will have severe consequences.";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        GameLog.UserActivityInfo($"/stuck: {user.Name}: {args[0]}");
        if (user.Location.Map == null)
            GameLog.UserActivityError($"/stuck: {user.Name} is not on a map...?");
        else
        {
            GameLog.UserActivityWarning(
                $"/stuck: {user.Name}, current location ({user.Location.X},{user.Location.Y})@{user.Location.Map.Name}");
            // Run some various checks for debugging purposes
            if (!user.Location.Map.Users.ContainsKey(user.Name))
                GameLog.UserActivityFatal($"/stuck: {user.Name} is on map {user.Location.Map.Name} but not in user cache");
            if (!user.Location.Map.Objects.Contains(user))
                GameLog.UserActivityFatal($"/stuck: {user.Name} is on map {user.Location.Map.Name} but not in object cache");
            if (!user.Location.Map.EntityTree.Contains(user))
                GameLog.UserActivityFatal($"/stuck: {user.Name} is on map {user.Location.Map.Name} but not in quadtree");
            if (user.Location.X > user.Location.Map.X || user.Location.Y > user.Location.Map.Y)
                GameLog.UserActivityFatal($"/stuck: {user.Name} out of bounds");
            // Gather nearby objects
            foreach (var obj in user.Location.Map.EntityTree.GetObjects(new Rectangle(user.Location.X, user.Location.Y,
                         1, 1)))
            {
                GameLog.UserActivityInfo($"/stuck: {user.Name}: coords {user.Location.X},{user.Location.Y}: Quadtree rectangle contains {obj.Type} ({obj.Name})");
            }
            if (user.DialogState.InDialog)
                GameLog.UserActivityInfo($"/stuck: {user.Name}: in dialog, with {user.DialogState.Associate?.Name ?? "unknown"}");
        }

        if (user.Nation == null)
        {
            user.Teleport("Gate of Lighter Slumber", 5, 5);
            return Success("The gods show mercy for your situation.");
        }

        var spawnPoint = user.Nation.RandomSpawnPoint;
        user.Teleport(spawnPoint.MapName, spawnPoint.X, spawnPoint.Y);
        return Success($"You are rescued by citizens of {user.Nation.Name}.");
    }
}