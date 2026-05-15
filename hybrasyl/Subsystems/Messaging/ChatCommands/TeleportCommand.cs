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

internal class TeleportCommand : ChatCommand
{
    public new static string Command = "teleport";

    public new static string ArgumentText =
        "<string playername> | <string npcname> | <string mapname> <byte x> <byte y> | <uint mapnumber> <byte x> <byte y>";

    public new static string HelpText = "Teleport to the specified player, or the given map number and coordinates.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length == 1)
        {
            // Either user or npc
            if (Game.World.WorldState.ContainsKey<User>(args[0]))
            {
                var target = Game.World.WorldState.Get<User>(args[0]);
                user.Teleport(target.Location.MapId, target.Location.X, target.Location.Y);
                return Success(
                    $"Teleported to {target.Name} - {target.Map.Name} ({target.Location.X},{target.Location.Y})");
            }

            if (Game.World.WorldState.TryGetValue(args[0], out Merchant merchant))
            {
                var (x, y) = merchant.Map.FindEmptyTile((byte) (merchant.Map.X / 2), (byte) (merchant.Map.Y / 2));
                if (x > 0 && y > 0)
                {
                    user.Teleport(merchant.Map.Id, x, y);
                    return Success($"Teleported to {merchant.Name} - {merchant.Map.Name} ({x}, {y})");
                }

                return Fail("Sorry, something went wrong (empty tile could not be found..?)");
            }

            return Fail($"Sorry, user or npc {args[0]} not found.");
        }

        ushort? mapnum = null;
        if (ushort.TryParse(args[0], out var num))
            mapnum = num;
        else if (Game.World.WorldState.TryGetValueByIndex(args[0], out MapObject targetMap))
            mapnum = targetMap.Id;
        else
            return Fail("Unknown map id or map name");

        if (Game.World.WorldState.TryGetValue(mapnum, out MapObject map))
        {
            if (byte.TryParse(args[1], out var x) && byte.TryParse(args[2], out var y))
            {
                if (x < map.X && y < map.Y)
                {
                    user.Teleport(map.Id, x, y);
                    return Success($"Teleported to {map.Name} ({x},{y}).");
                }

                return Fail("Invalid x/y specified (hint: mapsize is {map.X}x{map.Y})");
            }

            return Fail("Couldn't parse map number or coordinates (uint / byte / byte)");
        }

        return Fail("Map number {mapnum} not found");
    }
}