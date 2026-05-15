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