/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */
 
using Hybrasyl.Objects;
using Hybrasyl.Xml.Status;

namespace Hybrasyl.Messaging
{

    class StatusCommand : ChatCommand
    {
        public new static string Command = "status";
        public new static string ArgumentText = "<string statusName>";
        public new static string HelpText = "Apply a given status to yourself.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValueByIndex(args[0], out Status status))
            {
                user.ApplyStatus(new CreatureStatus(status, user, null, null));
                return Success();
            }
            return Fail("No such status was found. Missing XML file perhaps?");
        }
    }

    class ClearStatusCommand : ChatCommand
    {
        public new static string Command = "clearstatus";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Clear all statuses and conditions.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {

            user.RemoveAllStatuses();
            user.Condition.ClearConditions();
            return Success("All statuses and conditions cleared");
        }

    }

    class ClearFlagsCommand : ChatCommand
    {
        public new static string Command = "clearflags";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Clear all player flags.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {

            user.Condition.ClearFlags();
            return Success("Alive, all flags cleared");
        }

    }

    class ConditionCommand : ChatCommand
    {
        public new static string Command = "condition";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Display current conditions and player flags.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args) => Success($"Flags: {user.Condition.Flags} Conditions: {user.Condition.Conditions}");
    }

    class StatusesCommand : ChatCommand
    {
        public new static string Command = "statuses";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Display information about current statuses.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            string statusReport = string.Empty;
            foreach (var status in user.CurrentStatusInfo)
                statusReport = $"{statusReport}{status.Name}: {status.Remaining} seconds remaining, tick every {status.Tick} seconds\n";
            user.SendMessage(statusReport, MessageTypes.SLATE_WITH_SCROLLBAR);
            return Success();
        }
    }
}