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

using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hybrasyl.Objects;

namespace Hybrasyl.ChatCommands;

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

internal class VersionCommand : ChatCommand
{
    public new static string Command = "version";
    public new static string ArgumentText = "none";
    public new static string HelpText = "Displays the current version of the running server.";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args) =>
        Success(
            $"Hybrasyl {Game.Assemblyinfo.Version}\n\nRunning commit {Game.Assemblyinfo.GitHash}:\n\n{Game.CommitLog}\n\n(C) 2020 ERISCO, LLC",
            MessageTypes.SLATE_WITH_SCROLLBAR);
}

internal class RecentkillsCommand : ChatCommand
{
    public new static string Command = "recentkills";
    public new static string ArgumentText = "none";
    public new static string HelpText = "Displays your last 25 (monster) kills, along with timestamps.";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        return Success(
            $"Kill List\n---------\n\n{string.Join("", user.RecentKills.Select(selector: x => $"{x.Name} - {x.Timestamp}\n").ToList())}",
            MessageTypes.SLATE_WITH_SCROLLBAR);
    }
}

internal class HelpCommand : ChatCommand
{
    public new static string Command = "help";
    public new static string ArgumentText = "none";
    public new static string HelpText = "Displays a list of commands.";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var helpString = "Command Help\n------------\n\n";
        helpString =
            $"{helpString}Note: String arguments containing spaces should be quoted when used, \"like this\".\n\n";

        if (args.Length == 0)
        {
            foreach (var x in typeof(ChatCommand).Assembly.GetTypes()
                         .Where(predicate: type => type.IsSubclassOf(typeof(ChatCommand))))
            {
                var command = (string) x.GetField("Command", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                var argtext = (string) x.GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null);
                var priv = (bool) x.GetField("Privileged", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                var helptext =
                    (string) x.GetField("HelpText", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                if (priv && !user.AuthInfo.IsPrivileged) continue;
                helpString = $"{helpString}/{command} - {argtext}\n  {helptext}\n\n";
            }
        }
        else
        {
            if (World.CommandHandler.TryGetHandler(args[0], out var handler))
            {
                var command = (string) handler.GetField("Command", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null);
                var argtext = (string) handler.GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null);
                var priv = (bool) handler.GetField("Privileged", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null);
                var helptext = (string) handler.GetField("HelpText", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null);
                if (priv && !user.AuthInfo.IsPrivileged) return Fail("Access denied");
                helpString = $"{helpString}/{command} - {argtext}\n  {helptext}\n\n";
            }
            else
            {
                return Fail("Command not found");
            }
        }

        return Success(helpString, MessageTypes.SLATE_WITH_SCROLLBAR);
    }
}