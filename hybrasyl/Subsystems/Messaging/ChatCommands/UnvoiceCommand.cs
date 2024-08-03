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

internal class UnvoiceCommand : ChatCommand
{
    public new static string Command = "unvoice";
    public new static string ArgumentText = "<string username>";
    public new static string HelpText = "Remove a participant's ability to speak at a class.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetSocialEvent(user, out var e))
        {
            if (e.MapId != user.Map.Id)
                return Fail("You are not at the event...?");
            // TODO: this is case sensitive which has the potential to be ungodly annoying
            if (!user.Map.Users.ContainsKey(args[0]))
                return Fail("They are not at this event.");
            e.Speakers.Remove(args[0]);
            return Success($"{args[0]}: speaking privileges removed");
        }

        return Fail("You are not currently running an event.");
    }
}