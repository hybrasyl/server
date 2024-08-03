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
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class LogLevelCommand : ChatCommand
{
    public new static string Command = "loglevel";
    public new static string ArgumentText = "<string type> <string loglevel>";

    public new static string HelpText =
        "Set the log level for a specific logging type. Use /loginfo to get a list of types and levels.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Enum.TryParse<LogType>(args[0], out var logType) && Enum.TryParse<LogLevel>(args[1], out var logLevel))
        {
            if (!GameLog.HasLogger(logType)) return Fail("There is not a separate logger for {logType}.");
            GameLog.SetLevel(logType, logLevel);
            return Success($"{logType} set to {logLevel}");
        }

        return Fail("Log type or log level was invalid. Use /loginfo to get a valid list.");
    }
}