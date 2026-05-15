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
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

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