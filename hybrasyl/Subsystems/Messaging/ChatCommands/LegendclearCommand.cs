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

using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class LegendclearCommand : ChatCommand
{
    public new static string Command = "legendclear";
    public new static string ArgumentText = "[<int marks>]";

    public new static string HelpText =
        "Clear your legend of the specified number of marks, starting at the end. If no argument given, CLEARS ALL MARKS. WARNING: Not reversible.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length == 0)
        {
            user.Legend.Clear();
            return Success("Legend cleared.");
        }

        if (int.TryParse(args[0], out var numToRemove))
        {
            user.Legend.RemoveMark(numToRemove);
            return Success($"Last {numToRemove} legend mark(s) removed.");
        }

        return Fail("Couldn't parse number of marks");
    }
}