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
using System.Linq;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using MessageType = Hybrasyl.Internals.Enums.MessageType;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class LogInfoCommand : ChatCommand
{
    public new static string Command = "loginfo";
    public new static string ArgumentText = "None";
    public new static string HelpText = "List all mobs on the current map.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var txt = "Current Logging Configuration\n-----------------------------\n";
        foreach (var (type, logger) in GameLog.Loggers)
            txt = $"{txt}\n{type}: {logger.Level.MinimumLevel} ->\n {logger.Path.Replace("\\", "/")}\n";

        txt = $"{txt}\nAvailable Log Types:\n\n";
        txt = Enum.GetValues<LogType>().Aggregate(txt, func: (current, strEnum) => $"{current} {strEnum}");
        txt = $"{txt}\nAvailable Log Levels:\n\n";
        txt = Enum.GetValues<LogLevel>().Aggregate(txt, func: (current, strEnum) => $"{current} {strEnum}");
        return Success(txt, (byte) MessageType.SlateScrollbar);
    }
}