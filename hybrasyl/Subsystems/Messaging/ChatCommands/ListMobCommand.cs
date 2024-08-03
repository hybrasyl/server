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

using System.Linq;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class ListMobCommand : ChatCommand
{
    public new static string Command = "listmob";
    public new static string ArgumentText = "None";
    public new static string HelpText = "List all mobs on the current map.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var moblist = string.Format("{0,25}", "Name") + " " + string.Format("{0,40}", "Details") + "\n";
        foreach (var mob in user.Map.Objects.Where(predicate: x => x is Monster).Select(selector: y => y as Monster))
        {
            var mobdetails = $"({mob.X},{mob.Y}) {mob.Stats}";
            moblist += string.Format("{0,25}", "Name") + " " + string.Format("{0,40}", mobdetails) + "\n";
        }

        return Success(moblist, MessageTypes.SLATE_WITH_SCROLLBAR);
    }
}