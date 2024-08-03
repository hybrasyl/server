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
using System.Linq;
using Hybrasyl.Casting;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class ClevelCommand : ChatCommand
{
    public new static string Command = "clevel";
    public new static string ArgumentText = "<string skillName> <int level>";
    public new static string HelpText = "Change the specified skill or spell to a given mastery level.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        BookSlot slot;
        if (Game.World.WorldData.TryGetValueByIndex(args[0], out Castable castable))
        {
            if (!uint.TryParse(args[1], out var i))
                return Fail("What kind of level is that?");

            if (user.SpellBook.Contains(castable.Id))
            {
                slot = user.SpellBook.Single(predicate: x => x.Castable.Name == castable.Name);
                var uses = castable.Mastery.Uses * ((double) i / 100);
                slot.UseCount = Convert.ToUInt32(uses);
                user.SendSpellUpdate(slot, user.SpellBook.SlotOf(castable.Id));
            }
            else if (user.SkillBook.Contains(castable.Id))
            {
                slot = user.SkillBook.Single(predicate: x => x.Castable.Name == castable.Name);
                var uses = castable.Mastery.Uses * ((double) i / 100);
                slot.UseCount = Convert.ToUInt32(uses);
                user.SendSkillUpdate(slot, user.SkillBook.SlotOf(castable.Id));
            }
            else
            {
                return Fail("You don't know that spell or skill.");
            }

            return Success($"Castable {slot.Castable.Name} set to level {i}.");
        }

        return Fail("Castable not found");
    }
}