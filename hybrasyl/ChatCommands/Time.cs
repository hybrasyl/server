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

using System;
using Hybrasyl.Objects;

namespace Hybrasyl.ChatCommands;

internal class TimeCommand : ChatCommand
{
    public new static string Command = "time";
    public new static string ArgumentText = "none";
    public new static string HelpText = "Display the current server time.";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args) => Success($"{HybrasylTime.Now}");
}

internal class TimeconvertCommand : ChatCommand
{
    public new static string Command = "timeconvert";
    public new static string ArgumentText = "<string timeformat> <string time>";
    public new static string HelpText = "Convert a time between aisling/terran formats.";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args[0].ToLower() == "aisling")
        {
            var hybrasylTime = HybrasylTime.FromString(args[1]);
            return Success($"{args[1]} is {HybrasylTime.ConvertToTerran(hybrasylTime)} .");
        }

        if (args[0].ToLower() == "terran")
        {
            if (DateTime.TryParse(args[1], out var time))
            {
                var hybrasylTime = HybrasylTime.ConvertToHybrasyl(time);
                return Success($"{args[1]} is {hybrasylTime} .");
            }

            return Fail("Couldn't parse passed value (datetime)");
        }

        return Fail("Unsupported time format. Try 'aisling' or 'terran'");
    }
}