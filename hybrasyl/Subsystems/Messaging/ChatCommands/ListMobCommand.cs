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
using System.Linq;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class ListMobCommand : ChatCommand
{
    public new static string Command = "listmob";
    public new static string ArgumentText = "None";
    public new static string HelpText = "List all mobs on the current map.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        string moblist = string.Empty;
        foreach (var mob in user.Map.Objects.Where(predicate: x => x is Monster).Select(selector: y => y as Monster).OrderBy(z=> z.Name))
        {
            var mobdetails = $"{mob.Name}@({mob.X},{mob.Y}) Spawned at ({mob.SpawnPoint.X},{mob.SpawnPoint.Y})";
            mobdetails += $"\n-->ID {mob.Id} {mob.Stats}";
            moblist += $"{mobdetails}\n";
        }

        return Success(moblist, MessageTypes.SLATE_WITH_SCROLLBAR);
    }
}