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

internal class MusicCommand : ChatCommand

{
    public new static string Command = "music";
    public new static string ArgumentText = "<byte music>";
    public new static string HelpText = "Plays the specified background music.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (byte.TryParse(args[0], out var track))
        {
            user.Map.Music = track;
            foreach (var mapuser in user.Map.Users.Values) mapuser.SendMusic(track);
            return Success($"Music track changed to {track} for all users on {user.Map.Name}.");
        }

        return Fail("The value you specified could not be parsed (byte)");
    }
}