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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

public class ChatCommandHandler
{
    public const string UnknownCommandMessage = "No such command, try /help.";

    private static readonly Regex QuotesRegex = new(" (?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
    private static readonly Regex ArgsRegex = new("(\\[[a-zA-Z\\<\\> ]*\\])");
    private readonly Dictionary<string, (Type Type, List<int> argCount)> _associates = new();

    public ChatCommandHandler()
    {
        var err = 0;
        var numCommands = 0;
        foreach (var x in typeof(ChatCommand).Assembly.GetTypes()
                     .Where(predicate: type => type.IsSubclassOf(typeof(ChatCommand))))
            try
            {
                // ArgumentText example
                // <string foo> <int bar> | <int baz> [<string quux> <int bazbar>] 
                var command =
                    (string) x.GetField("Command", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
                var argtext = (string) x.GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static)
                    !.GetValue(null)!;
                var options = argtext.Split('|');
                var allowedArgcounts = new List<int>();

                foreach (var option in options)
                {
                    // Count number of required / optional arguments
                    var split = ArgsRegex.Split(option);
                    if (split.Count() == 1)
                        allowedArgcounts.Add(option.Count(predicate: e => e == '<'));
                    else
                        // Now get the bracket contents

                        foreach (Group group in ArgsRegex.Match(argtext).Groups)
                        {
                            var baseopt = argtext.Remove(argtext.IndexOf(group.Value)).Count(predicate: e => e == '<');
                            allowedArgcounts.Add(baseopt + group.Value.Count(predicate: e => e == '<'));
                            allowedArgcounts.Add(baseopt);
                        }
                }

                // int argcount = ((string)x.GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!).Count(e => e == '<');
                _associates.Add(command, (x, allowedArgcounts));
                numCommands++;
            }
            catch (Exception e)
            {
                GameLog.Warning(e, "Command module {Module}: could not be loaded", x.Name);
                err++;
            }

        GameLog.Info("Commands: {CommandCount} ({ErrorCount} error(s))", numCommands, err);
    }

    public void IsHandler(string command)
    {
        _associates.ContainsKey(command);
    }

    public bool TryGetHandler(string command, [MaybeNullWhen(false)] out Type handler)
    {
        handler = null;
        if (_associates.ContainsKey(command))
        {
            handler = _associates[command].Type;
            return true;
        }

        return false;
    }

    public void Handle(User user, string command, string args)
    {
        if (_associates.ContainsKey(command))
        {
            var strings = QuotesRegex.Matches(args).Select(selector: m => m.Value).ToArray();

            var handler = _associates[command];
            var priv = (bool) handler.Type.GetField("Privileged", BindingFlags.Public | BindingFlags.Static)
                !.GetValue(null)!;

            if (priv && !user.AuthInfo.IsPrivileged)
            {
                user.SendSystemMessage("Failed: Access denied (command is privileged)");
                GameLog.UserActivityError("{User}: denied attempt to use privileged command {Command}", user.Name, command);
                return;
            }

            var splitArgs = QuotesRegex.Split(args).Select(selector: e => e.Replace("\"", "")).ToArray();

            if (splitArgs.Length == 1 && string.IsNullOrEmpty(splitArgs[0]))
                splitArgs = new string[0];

            if (!handler.argCount.Contains(splitArgs.Length))
            {
                var argText = (string) handler.Type
                    .GetField("ArgumentText", BindingFlags.Public | BindingFlags.Static)
                    !.GetValue(null)!;
                if (argText.Length <= 50)
                    user.SendSystemMessage($"Usage: {command} {argText}");
                else
                    user.SendSystemMessage($"{command}: invalid arguments");
                return;
            }

            if (user.AuthInfo.IsPrivileged)
            {
                var type = priv ? "privileged" : "unprivileged";
                GameLog.Warning("{User}: executing {CommandType} command {Command} {Args}", user.Name, type, command, args);
            }
            else
            {
                GameLog.Info("{User}: executing command {Command} {Args}", user.Name, command, args);
            }


            var wtf = handler.Type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

            // ChatCommand contract: every registered command exposes a static Run method.
            var result = (ChatCommandResult) wtf!.Invoke(null, new object[] { user, splitArgs })!;
            user.SendMessage($"[Cmd] /{command} {args}", MessageType.Guild);
            user.SendMessage(result.Message, result.MessageType);
        }
        else
        {
            user.SendSystemMessage(UnknownCommandMessage);
        }
    }
}