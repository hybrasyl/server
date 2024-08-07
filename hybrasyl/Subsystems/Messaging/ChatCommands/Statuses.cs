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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Statuses;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class StatusCommand : ChatCommand
{
    public new static string Command = "status";
    public new static string ArgumentText = "<string statusName> [<int duration>]";
    public new static string HelpText = "Apply a given status to yourself with an optional duration.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldData.TryGetValue(args[0], out Status xmlStatus))
        {
            var duration = xmlStatus.Duration;
            if (args.Length > 1 && int.TryParse(args[1], out var duroverride))
                duration = duroverride;
            var status = new CreatureStatus(xmlStatus, user, null, null, duration);
            user.ApplyStatus(status);
            return Success($"Status {xmlStatus.Name} applied for {duration} seconds");
        }

        return Fail("Status not found (missing XML file)");
    }
}

internal class ClearStatusCommand : ChatCommand
{
    public new static string Command = "clearstatus";
    public new static string ArgumentText = "<string playername> | none";
    public new static string HelpText = "Clear all statuses and conditions.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        user.RemoveAllStatuses();
        user.Condition.ClearConditions();
        return Success("All statuses and conditions cleared");
    }
}

internal class ClearFlagsCommand : ChatCommand
{
    public new static string Command = "clearflags";
    public new static string ArgumentText = "<string username>";
    public new static string HelpText = "Clear player flags for a specified user.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.TryGetActiveUser(args[0], out var target))
        {
            target.Condition.ClearFlags();
            return Success($"{target.Name}: Alive, all flags cleared");
        }

        return Fail($"{args[1]} is not online");
    }
}

internal class ConditionCommand : ChatCommand
{
    public new static string Command = "condition";
    public new static string ArgumentText = "none";
    public new static string HelpText = "Display current conditions and player flags.";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args) =>
        Success($"Flags: {user.Condition.Flags} Conditions: {user.Condition.Conditions}");
}

internal class StatusesCommand : ChatCommand
{
    public new static string Command = "statuses";
    public new static string ArgumentText = "none";
    public new static string HelpText = "Display information about current statuses.";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var statusReport = string.Empty;
        foreach (var status in user.CurrentStatuses.Values)
            statusReport =
                $"{statusReport}{status.Name}: {status.Remaining} seconds remaining, tick every {status.Tick} seconds\n";
        user.SendMessage(statusReport, MessageTypes.SLATE_WITH_SCROLLBAR);
        return Success();
    }
}