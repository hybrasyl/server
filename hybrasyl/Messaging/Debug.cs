using Hybrasyl.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Messaging
{
    class ClearDialogCommand : ChatCommand
    {
        public new static string Command = "cleardialog";
        public new static string ArgumentText = "<string username>";
        public new static string HelpText = "Completely clear the dialog state for a given user.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!Game.World.WorldData.ContainsKey<User>(args[0]))
                return Fail($"User {args[0]} not logged in");

            var target = Game.World.WorldData.Get<User>(args[0]);

            if (target.IsExempt)
                return Fail($"User {target.Name} is exempt from your meddling.");
            else
                target.ClearDialogState();

            return Success($"User {target.Name}: dialog state cleared.");
        }
    }
}
