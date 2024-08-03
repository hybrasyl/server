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
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class SetEphemeralCommand : ChatCommand
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