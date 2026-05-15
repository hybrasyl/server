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
using MessageType = Hybrasyl.Internals.Enums.MessageType;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class ResistancesCommand : ChatCommand
{
    public new static string Command = "resistances";
    public new static string ArgumentText = "";
    public new static string HelpText = "Display current elemental resistances.";
    public new static bool Privileged = false;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var str = "Resistances\n-----------\n";
        foreach (var element in Enum.GetValues<ElementType>())
            str += $"{element} {user.Stats.ElementalModifiers.GetResistance(element)}\n";

        user.SendMessage(str, MessageType.SlateScrollbar);
        return Success();
    }
}