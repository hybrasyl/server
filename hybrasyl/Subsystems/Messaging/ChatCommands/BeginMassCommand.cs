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
using Hybrasyl.Subsystems.Players;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class BeginMassCommand : ChatCommand
{
    public new static string Command = "beginmass";
    public new static string ArgumentText = "<string deity>";
    public new static string HelpText = "Begin a mass.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.GlobalMessage,
            $"{user.Name}'s {char.ToUpper(args[0][0])}{args[0][1..]} mass is starting."));
        if (Game.World.WorldState.TryGetSocialEvent(user, out var _))
            return Fail("An event is already occurring here.");
        var e = new SocialEvent(user, SocialEventType.Mass, args[0]);
        Game.World.WorldState.SetWithIndex(user.Name, e, user.Map.Id);
        user.SendSystemMessage("Bring the light of creativity into this world.");
        return Success();
    }
}