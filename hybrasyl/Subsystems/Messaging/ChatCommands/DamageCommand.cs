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
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class DamageCommand : ChatCommand
{
    public new static string Command = "damage";
    public new static string ArgumentText = "<double damage> <string element>";

    public new static string HelpText =
        "Damage yourself for the specified amount, with the specified element. Careful...";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var element = ElementType.None;
        if (!double.TryParse(args[0], out var amount))
            return Fail("The value you specified could not be parsed (double)");
        if (!Enum.TryParse(args[1], true, out element))
            return Fail("I don't know what element that is. Sorry.");
        user.Damage(amount, element);
        return Success($"{user.Name} - damaged by {element}:{amount}");
    }
}