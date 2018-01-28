using Hybrasyl.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Statuses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

}