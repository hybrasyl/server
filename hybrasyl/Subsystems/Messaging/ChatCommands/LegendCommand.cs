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
using System;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class LegendCommand : ChatCommand
{
    public new static string Command = "legend";

    public new static string ArgumentText =
        "<string legendText> <byte icon> <byte color> | <int prefix> <int quantity> [<datetime date>]";

    public new static string HelpText =
        "Add a legend mark with the specified text, icon and color, and optionally with the given quantity and date.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Enum.TryParse(args[1], out LegendIcon icon) && Enum.TryParse(args[2], out LegendColor color))
        {
            var time = DateTime.Now;
            var qty = -1;
            string prefix = null;

            if (args.Length > 3)
            {
                prefix = args[3];
                if (args.Length >= 5)
                    int.TryParse(args[4], out qty);
                if (args.Length == 6)
                    DateTime.TryParse(args[5], out time);
            }

            user.Legend.AddMark(icon, color, args[0], time, prefix, true, qty);
            return Success("Legend added.");
        }

        return Fail("The arguments you specified could not be parsed.");
    }
}