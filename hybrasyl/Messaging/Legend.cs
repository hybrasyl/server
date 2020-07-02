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
 
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using System;

namespace Hybrasyl.Messaging
{

    class LegendCommand : ChatCommand
    {
        public new static string Command = "legend";
        public new static string ArgumentText = "<string legendText> <byte icon> <byte color> | <int prefix> <int quantity> [<datetime date>]";
        public new static string HelpText = "Add a legend mark with the specified text, icon and color, and optionally with the given quantity and date.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Enum.TryParse(args[1], out LegendIcon icon) && Enum.TryParse(args[2], out LegendColor color))
            {
                DateTime time = DateTime.Now;
                int qty = -1;
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

    class TitleCommand : ChatCommand
    {
        public new static string Command = "title";
        public new static string ArgumentText = "<string title>";
        public new static string HelpText = "Change your displayed title.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            user.Title = args[0];
            return Success("Title updated.");
        }
    }

    class LegendclearCommand : ChatCommand
    {
        public new static string Command = "legendclear";
        public new static string ArgumentText = "[<int marks>]";
        public new static string HelpText = "Clear your legend of the specified number of marks, starting at the end. If no argument given, CLEARS ALL MARKS. WARNING: Not reversible.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (args.Length == 0)
            {
                user.Legend.Clear();
                return Success("Legend cleared.");
            }
            if (int.TryParse(args[0], out int numToRemove))
            {
                user.Legend.RemoveMark(numToRemove);
                return Success($"Last {numToRemove} legend mark(s) removed.");
            }
            return Fail("Couldn't parse number of marks");
        }
    }
}
