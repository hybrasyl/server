/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */
 
using Hybrasyl.Objects;

namespace Hybrasyl.Messaging
{

    class MotionCommand : ChatCommand
    {
        public new static string Command = "motion";
        public new static string ArgumentText = "<byte motion> [<short speed>]";
        public new static string HelpText = "Displays the specified motion (player animation) with an optional speed.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            short speed = 20;
            if (byte.TryParse(args[0], out byte motion))
            {
                if (args.Length > 1)
                    short.TryParse(args[1], out speed);
                user.Motion(motion, speed);
                return Success($"Displayed motion {motion}.");
            }
            return Fail("The value you specified could not be parsed (byte)");
        }
    }

    class HairstyleCommand : ChatCommand
    {
        public new static string Command = "hairstyle";
        public new static string ArgumentText = "<byte hairstyle> [<byte haircolor>]";
        public new static string HelpText = "Change your hairstyle and hair color.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (byte.TryParse(args[0], out byte hairstyle))
            {
                if (args.Length > 1 && byte.TryParse(args[1], out byte haircolor))
                    user.HairColor = haircolor;
                user.HairStyle = hairstyle;
                user.SendUpdateToUser();
                return Success($"Hair color and/or style updated.");
            }
            return Fail("The value you specified could not be parsed (byte)");
        }
    }


    class EffectCommand : ChatCommand
    {
        public new static string Command = "effect";
        public new static string ArgumentText = "<byte effect>";
        public new static string HelpText = "Displays the specified effect (animation).";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            short speed = 20;
            if (byte.TryParse(args[0], out byte effect))
            {
                if (args.Length > 1)
                    short.TryParse(args[1], out speed);
                user.Effect(effect, speed);
                return Success($"Displayed effect {effect}.");
            }
            return Fail("The value you specified could not be parsed (byte)");
        }
    }

    class SoundCommand : ChatCommand
    {
        public new static string Command = "sound";
        public new static string ArgumentText = "<byte sound>";
        public new static string HelpText = "Plays the specified sound effect.";
        public new static bool Privileged = false;
        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (byte.TryParse(args[0], out byte sound))
            {
                user.SendSound(sound);
                return Success($"Played sound {sound}.");
            }

            return Fail("The value you specified could not be parsed (byte)");
        }
    }

    class MusicCommand : ChatCommand

    {
        public new static string Command = "music";
        public new static string ArgumentText = "<byte music>";
        public new static string HelpText = "Plays the specified background music.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (byte.TryParse(args[0], out byte track))
            {
                user.Map.Music = track;
                foreach (var mapuser in user.Map.Users.Values)
                {
                    mapuser.SendMusic(track);
                }
                return Success($"Music track changed to {track} for all users on {user.Map.Name}.");
            }
            return Fail("The value you specified could not be parsed (byte)");
        }
    }

    class ItemCommand : ChatCommand
    {
        public new static string Command = "item";
        public new static string ArgumentText = "<string itemName> [<uint quantity>]";
        public new static string HelpText = "Give yourself the specified item, with optional quantity.";
        public new static bool Privileged = false;


        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValueByIndex(args[0], out Xml.Item template))
            {
                var item = Game.World.CreateItem(template.Id);
                if (args.Length == 2 && int.TryParse(args[1], out int count) && count <= item.MaximumStack)
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

    class MapmsgCommand : ChatCommand
    {
        public new static string Command = "mapmsg";
        public new static string ArgumentText = "<string message>";
        public new static string HelpText = "Send a map message to everyone on the current map.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            user.Map.Message = args[0];
            foreach (var mapuser in user.Map.Users.Values)
            {
                mapuser.SendMessage(args[0], 18);
            }
            return Success("Map message sent.");
        }
    }

    class WorldmsgCommand : ChatCommand
    {
        public new static string Command = "worldmsg";
        public new static string ArgumentText = "<string message>";
        public new static string HelpText = "Send a map message to everyone on the current map.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            user.Map.Message = args[0];
            foreach (var connectedUser in World.ActiveUsers)
            {
                connectedUser.Value.SendWorldMessage(user.Name, args[0]);
            }
            return Success("World message sent.");
        }
    }
}
