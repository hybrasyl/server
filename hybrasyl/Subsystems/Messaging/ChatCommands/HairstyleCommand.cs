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

internal class HairstyleCommand : ChatCommand
{
    public new static string Command = "hairstyle";
    public new static string ArgumentText = "<ushort hairstyle> [<byte haircolor>]";
    public new static string HelpText = "Change your hairstyle and hair color.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (ushort.TryParse(args[0], out var hairstyle))
        {
            byte haircolor = 0;
            if (args.Length > 1 && byte.TryParse(args[1], out haircolor))
                user.HairColor = haircolor;
            user.HairStyle = hairstyle;
            user.SendUpdateToUser();
            return Success($"Hair color and/or style updated to style:{user.HairStyle} color:{user.HairColor}.");
        }

        return Fail("The value you specified could not be parsed (byte)");
    }
}