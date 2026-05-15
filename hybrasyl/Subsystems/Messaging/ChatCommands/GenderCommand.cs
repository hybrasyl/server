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

using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class GenderCommand : ChatCommand
{
    public new static string Command = "gender";
    public new static string ArgumentText = "<m|f>";
    public new static string HelpText = "Set your character's gender to Male (m) or Female (f). Player characters cannot be Neutral.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var input = args[0].ToLowerInvariant();
        var gender = input switch
        {
            "m" or "male" => Gender.Male,
            "f" or "female" => Gender.Female,
            _ => (Gender?)null
        };

        if (gender is null)
            return Fail($"Unknown gender '{args[0]}'. Use 'm' / 'male' or 'f' / 'female'.");

        user.Gender = gender.Value;
        user.SendUpdateToUser();
        user.Show();
        return Success($"Gender set to {gender.Value}.");
    }
}
