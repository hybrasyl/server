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
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class ItemCommand : ChatCommand
{
    public new static string Command = "item";
    public new static string ArgumentText = "<string itemName> [<uint quantity>]";
    public new static string HelpText = "Give yourself the specified item, with optional quantity.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldData.TryGetValueByIndex(args[0], out Item template))
        {
            var item = Game.World.CreateItem(template.Id);
            if (args.Length == 2 && int.TryParse(args[1], out var count) && count <= item.MaximumStack)
                item.Count = count;
            else
                item.Count = item.MaximumStack;
            Game.World.Insert(item);
            user.AddItem(item);
            return Success($"Item {args[0]} generated.");
        }

        return Fail($"Item {args[0]} not found");
    }
}