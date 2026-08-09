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
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class ShowCookiesCommand : ChatCommand
{
    public new static string Command = "showcookies";
    public new static string ArgumentText = "<string playername>";
    public new static string HelpText = "Show permanent and session cookies set for a specified player";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue<User>(args[0], out var target))
        {
            var cookies = $"User {target.Name} Cookie List\n\n---Permanent Cookies---\n";
            foreach (var cookie in target.GetCookies())
                cookies = $"{cookies}\n{cookie.Key} : {cookie.Value}\n";
            cookies = $"{cookies}\n---Session Cookies---\n";
            foreach (var cookie in target.GetSessionCookies())
                cookies = $"{cookies}\n{cookie.Key} : {cookie.Value}\n";
            return Success($"{cookies}", MessageTypes.SLATE_WITH_SCROLLBAR);
        }

        return Fail($"User {args[0]} not logged in");
    }
}
