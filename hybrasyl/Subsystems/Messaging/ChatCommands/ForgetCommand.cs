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

internal class ForgetCommand : ChatCommand
{
    public new static string Command = "forget";
    public new static string ArgumentText = "<string castable>";
    public new static string HelpText = "Forget the specified castable (remove from skill or spell book)";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldData.TryGetValueByIndex(args[0], out Castable castable))
        {
            if (user.SpellBook.Contains(castable.Id))
            {
                var i = user.SpellBook.SlotOf(castable.Id);
                user.SpellBook.Remove(i);
                user.SendClearSpell(i);
            }
            else if (user.SkillBook.Contains(castable.Id))
            {
                var i = user.SkillBook.SlotOf(castable.Id);
                user.SkillBook.Remove(i);
                user.SendClearSkill(i);
            }
            else
            {
                return Fail("You don't know that skill or spell.");
            }

            return Success($"{args[0]} removed.");
        }

        return Fail("That castable does not exist.");
    }
}