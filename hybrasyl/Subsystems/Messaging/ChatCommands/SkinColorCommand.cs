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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class SkinColorCommand : ChatCommand
{
    public new static string Command = "skincolor";
    public new static string ArgumentText = "<byte 0-255>";
    public new static string HelpText =
        "Set your character's skin color sprite — the mm##.epf / wm##.epf variant from khan{m,w}im. "
        + "Enum names cover 0-9 (Basic..Purple); the wire byte addresses 0-255 for arbitrary client-side variants.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (!byte.TryParse(args[0], out var value))
            return Fail($"Could not parse '{args[0]}' as byte (0-255).");

        user.SkinColor = (SkinColor)value;
        user.SendUpdateToUser();
        user.Show();
        return Success($"Skin color set to {value} ({user.SkinColor}).");
    }
}
