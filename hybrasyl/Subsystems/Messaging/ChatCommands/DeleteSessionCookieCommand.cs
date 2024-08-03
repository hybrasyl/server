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

internal class DeleteSessionCookieCommand : ChatCommand
{
    public new static string Command = "deletesessioncookie";
    public new static string ArgumentText = "<string cookie> | <string playername> <string cookie>";

    public new static string HelpText =
        "Clear (delete) a given session (transient) scripting cookie. This is useful when working with scripts that modify player state.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length == 1)
        {
            user.DeleteSessionCookie(args[0]);
            return Success($"Session flag {args[0]} deleted.");
        }

        var target = Game.World.WorldState.Get<User>(args[0]);

        if (target.AuthInfo.IsExempt)
            return Fail($"User {target.Name} is exempt from your meddling.");
        target.DeleteSessionCookie(args[1]);
        return Success($"Player {target.Name}: flag {args[1]} removed.");
    }
}