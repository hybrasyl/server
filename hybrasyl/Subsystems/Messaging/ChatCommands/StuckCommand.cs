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

using System.Drawing;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class StuckCommand : ChatCommand
{
    public new static string Command = "stuck";
    public new static string ArgumentText = "<string reason>";

    public new static string HelpText =
        "Use if you are stuck and cannot move. Abuse of this command will have severe consequences.";

    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        GameLog.UserActivityInfo("/stuck: {User}: {Reason}", user.Name, args[0]);
        if (user.Location.Map == null)
        {
            GameLog.UserActivityError("/stuck: {User} is not on a map...?", user.Name);
        }
        else
        {
            GameLog.UserActivityWarning(
                "/stuck: {User}, current location ({X},{Y})@{Map}", user.Name, user.Location.X, user.Location.Y, user.Location.Map.Name);
            // Run some various checks for debugging purposes
            if (!user.Location.Map.Users.ContainsKey(user.Name))
                GameLog.UserActivityFatal(
                    "/stuck: {User} is on map {Map} but not in user cache", user.Name, user.Location.Map.Name);
            if (!user.Location.Map.Objects.Contains(user))
                GameLog.UserActivityFatal(
                    "/stuck: {User} is on map {Map} but not in object cache", user.Name, user.Location.Map.Name);
            if (!user.Location.Map.EntityTree.Contains(user))
                GameLog.UserActivityFatal(
                    "/stuck: {User} is on map {Map} but not in quadtree", user.Name, user.Location.Map.Name);
            if (user.Location.X > user.Location.Map.X || user.Location.Y > user.Location.Map.Y)
                GameLog.UserActivityFatal("/stuck: {User} out of bounds", user.Name);
            // Gather nearby objects
            foreach (var obj in user.Location.Map.EntityTree.GetObjects(new Rectangle(user.Location.X, user.Location.Y,
                         1, 1)))
                GameLog.UserActivityInfo(
                    "/stuck: {User}: coords {X},{Y}: Quadtree rectangle contains {ObjectType} ({ObjectName})", user.Name, user.Location.X, user.Location.Y, obj.Type, obj.Name);
            if (user.DialogState.InDialog)
                GameLog.UserActivityInfo(
                    "/stuck: {User}: in dialog, with {Associate}", user.Name, user.DialogState.Associate?.Name ?? "unknown");
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