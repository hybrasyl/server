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

internal class ClearBookCommand : ChatCommand
{
    public new static string Command = "clearbook";
    public new static string ArgumentText = "";
    public new static string HelpText = "Clear all skills and spells from your books.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        // of course because of the client we need to do this in a teeth-grindingly stupid way
        foreach (var x in user.SpellBook)
            user.SendClearSpell(x.Slot);

        foreach (var y in user.SkillBook)
            user.SendClearSkill(y.Slot);

        user.SpellBook.Clear();
        user.SkillBook.Clear();

        return Success();
    }
}