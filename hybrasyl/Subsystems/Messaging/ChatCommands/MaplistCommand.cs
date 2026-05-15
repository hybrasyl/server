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
using System.Text.RegularExpressions;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class MaplistCommand : ChatCommand
{
    public new static string Command = "maplist";
    public new static string ArgumentText = "<string searchTerm>";
    public new static string HelpText = "Searches for maps with the specified search term.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var searchstring = args[0];
        if (args.Length > 1)
            searchstring = string.Join(" ", args);

        try
        {
            var term = new Regex($"{searchstring}");
            var queryMaps = from amap in Game.World.WorldState.Values<MapObject>()
                where term.IsMatch(amap.Name)
                select amap;

            var result = queryMaps.Aggregate("", func: (current, map) => current + $"{map.Id} - {map.Name}\n");
            if (result.Length > 65400)
                result = $"{result.Substring(0, 65400)}\n(Results truncated)";

            result = $"Search Results\n--------------\n\n{result}";
            return Success(result, MessageTypes.SLATE_WITH_SCROLLBAR);
        }
        catch
        {
            return Fail("Search string could not be parsed as a regular expression. Try again.");
        }
    }
}