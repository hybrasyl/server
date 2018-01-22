﻿using Hybrasyl.Objects;
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Hybrasyl.Messaging
{

    class MaplistCommand : ChatCommand
    {
        public new static string Command = "maplist";
        public new static string ArgumentText = "<string searchTerm>";
        public new static string HelpText = "Searches for maps with the specified search term.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            var searchstring = args[0];
            if (args.Length > 1)
                searchstring = string.Join(" ", args);

            try
            {
                var term = new Regex($"{searchstring}");
                var queryMaps = from amap in Game.World.WorldData.Values<Map>()
                                where term.IsMatch(amap.Name)
                                select amap;

                var result = queryMaps.Aggregate("", (current, map) => current + $"{map.Id} - {map.Name}\n");
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

    class HelpCommand : ChatCommand
    {
        public new static string Command = "help";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Displays a list of commands.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            string helpString = "Command Help\n------------\n\n";
            helpString = $"{helpString}Note: String arguments containing spaces should be quoted when used, \"like this\".\n\n";

            if (args.Length == 0)
            {
                foreach (var x in typeof(ChatCommand).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ChatCommand))))
                {
                    string command = (string)x.GetField("Command", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    string argtext = (string)x.GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    bool priv = (bool)x.GetField("Privileged", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    string helptext = (string)x.GetField("HelpText", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    if (priv && !user.IsPrivileged) continue;
                    helpString = $"{helpString}/{command} - {argtext}\n  {helptext}\n\n";
                }
            }
            else
            {
                if (World.CommandHandler.TryGetHandler(args[0], out Type handler))
                {
                    string command = (string)handler.GetField("Command", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    string argtext = (string)handler.GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    bool priv = (bool)handler.GetField("Privileged", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    string helptext = (string)handler.GetField("HelpText", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    if (priv && !user.IsPrivileged) return Fail("Access denied");
                    helpString = $"{helpString}/{command} - {argtext}\n  {helptext}\n\n";
                }
                else
                    return Fail("Command not found");
            }

            return Success(helpString, MessageTypes.SLATE_WITH_SCROLLBAR);
        }

    }

}
