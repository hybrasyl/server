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

internal class BaseHpCommand : ChatCommand
{
    public new static string Command = "basehp";
    public new static string ArgumentText = "<uint hp>";
    public new static string HelpText = "Set base HP to the specified uint value.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (uint.TryParse(args[0], out var hp))
        {
            user.Stats.BaseHp = hp;
            user.UpdateAttributes(StatUpdateFlags.Full);
            return Success($"Base HP now {hp}");
        }

        return Fail("The value you specified could not be parsed (uint)");
    }
}