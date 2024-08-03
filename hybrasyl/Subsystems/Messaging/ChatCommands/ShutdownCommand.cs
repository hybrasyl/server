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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Servers;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

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