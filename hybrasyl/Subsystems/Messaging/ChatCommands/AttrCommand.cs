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

internal class AttrCommand : ChatCommand
{
    public new static string Command = "attr";
    public new static string ArgumentText = "<string attribute> <byte value>";
    public new static string HelpText = "Set a specified attribute (str/con etc) to the given byte value.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (!byte.TryParse(args[1], out var newStat))
            return Fail($"The value you specified for attribute {args[0]} could not be parsed (byte)");

        switch (args[0].ToLower())
        {
            case "str":
                user.Stats.BaseStr = newStat;
                break;
            case "con":
                user.Stats.BaseCon = newStat;
                break;
            case "dex":
                user.Stats.BaseDex = newStat;
                break;
            case "wis":
                user.Stats.BaseWis = newStat;
                break;
            case "int":
                user.Stats.BaseInt = newStat;
                break;
            default:
                return Fail($"Unknown attribute {args[0].ToLower()}");
        }

        user.UpdateAttributes(StatUpdateFlags.Stats);
        return Success($"{args[0].ToLower()} now {newStat}.");
    }
}