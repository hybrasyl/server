﻿// This file is part of Project Hybrasyl.
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

using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Players.Grouping;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class GroupCommand : ChatCommand
{
    public new static string Command = "group";
    public new static string ArgumentText = "<string username>";
    public new static string HelpText = "Invite the specified player to your group.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (!Game.World.TryGetActiveUser(args[0], out var newMember))
            return Fail($"The user {args[0]} could not be found");
        if (!newMember.Grouping)
            return Fail($"{args[0]} is not accepting group invites.");
        var response = new ServerPacket(0x63);
        response.WriteByte((byte) GroupServerPacketType.Ask);
        response.WriteString8(user.Name);
        response.WriteByte(0);
        response.WriteByte(0);
        newMember.Enqueue(response);
        return Success($"{args[0]} has been invited to your group.");
    }
}