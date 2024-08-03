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
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class SpawnCommand : ChatCommand
{
    public new static string Command = "spawn";
    public new static string ArgumentText = "<string creature> <string behaviorSet> <level>";
    public new static string HelpText = "Spawn a monster with the specified name, behavior set, and level.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldData.TryGetValue(args[0], out Creature creature) &&
            Game.World.WorldData.TryGetValue(args[1], out CreatureBehaviorSet cbs))
        {
            if (byte.TryParse(args[2], out var x))
            {
                var newMob = new Monster(creature, SpawnFlags.Active, x, null, cbs);
                user.World.Insert(newMob);
                user.Map.Insert(newMob, user.X, user.Y);
                return Success($"{creature.Name} spawned.");
            }

            return Fail("Level must be a byte between 1 and 255");
        }

        return Fail($"Creature {args[0]} not found");
    }
}