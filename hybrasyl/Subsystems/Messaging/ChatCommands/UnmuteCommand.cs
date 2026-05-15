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

using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

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