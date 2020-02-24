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
