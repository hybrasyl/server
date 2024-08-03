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

using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

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
            GameLog.UserActivityWarning(
                $"User {user.Name}: /repop usage, current map {user.Location.Map.Name}, last hit by {user.LastHitter?.Name ?? "unknown"}, stats {user.Stats}");
            if (user.Condition.Alive)
                return Fail("You're not dead.");
            user.Teleport(Game.ActiveConfiguration.Handlers.Death.Map.Value,
                Game.ActiveConfiguration.Handlers.Death.Map.X,
                Game.ActiveConfiguration.Handlers.Death.Map.Y);
            if (user.Map.Name != Game.ActiveConfiguration.Handlers.Death.Map.Value)
                GameLog.UserActivityFatal(
                    $"User {user.Name}: teleported, but not in {Game.ActiveConfiguration.Handlers.Death.Map.Value}...?");
        }
        else
        {
            return Fail($"You are already in {Game.ActiveConfiguration.Handlers.Death.Map.Value}.");
        }

        return Success("You are where you should be.");
    }
}