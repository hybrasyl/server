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

internal class DamageInvCommand : ChatCommand
{
    public new static string Command = "damageinv";
    public new static string ArgumentText = "";
    public new static string HelpText = "Damage the items in your inventory (useful for testing repair)";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var dura = string.Empty;
        foreach (var item in user.Inventory)
            if (item.Durability != 0)
            {
                item.Durability /= 2;
                dura += $"{item.Name} -> {item.Durability} / {item.MaximumDurability}\n";
            }

        user.SendInventory();

        return Success(dura, (byte) MessageType.SlateScrollbar);
    }
}