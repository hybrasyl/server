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
using Hybrasyl.Enums;
using Hybrasyl.Objects;

namespace Hybrasyl.ChatCommands;

// You gotta start somewhere, so we're starting as slash commands.
internal class AnnounceMass : ChatCommand
{
    public new static string Command = "announcemass";
    public new static string ArgumentText = "<string deity>";
    public new static string HelpText = "Announce a mass for the specified deity";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.GlobalMessage,
            $"{user.Name} will be giving a mass at the temple of {char.ToUpper(args[0][0])}{args[0][1..]}"));
        return Success();
    }
}

internal class AnnounceClass : ChatCommand
{
    public new static string Command = "announceclass";
    public new static string ArgumentText = "<string subject>";
    public new static string HelpText = "Announce a class for the specified subject";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.GlobalMessage,
            $"{user.Name} will be giving a {char.ToUpper(args[0][0])}{args[0][1..]} class at Loures College."));
        return Success();
    }
}

internal class BeginMass : ChatCommand
{
    public new static string Command = "beginmass";
    public new static string ArgumentText = "<string deity>";
    public new static string HelpText = "Begin a mass.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.GlobalMessage,
            $"{user.Name}'s {char.ToUpper(args[0][0])}{args[0][1..]} mass is starting."));
        if (Game.World.WorldState.TryGetSocialEvent(user, out var _))
            return Fail("An event is already occurring here.");
        var e = new SocialEvent(user, SocialEventType.Mass, args[0]);
        Game.World.WorldState.SetWithIndex(user.Name, e, user.Map.Id);
        user.SendSystemMessage("Bring the light of creativity into this world.");
        return Success();
    }
}

internal class BeginClass : ChatCommand
{
    public new static string Command = "beginclass";
    public new static string ArgumentText = "<string subject>";
    public new static string HelpText = "Begin a class.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.GlobalMessage,
            $"{user.Name}'s {char.ToUpper(args[0][0])}{args[0][1..]} class is starting."));
        user.SendSystemMessage("Use your spark.");
        if (Game.World.WorldState.TryGetSocialEvent(user, out var _))
            return Fail("An event is already occurring here.");
        var e = new SocialEvent(user, SocialEventType.Class, args[0]);
        Game.World.WorldState.SetWithIndex(user.Name, e, user.Map.Id);
        user.Map.MapMute();
        return Success();
    }
}

internal class Voice : ChatCommand
{
    public new static string Command = "voice";
    public new static string ArgumentText = "<string username>";
    public new static string HelpText = "Allow a participant to speak at a class.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetSocialEvent(user, out var e))
        {
            if (e.MapId != user.Map.Id)
                return Fail("You are not at the event...?");
            // TODO: this is case sensitive which has the potential to be ungodly annoying
            if (!user.Map.Users.ContainsKey(args[0]))
                return Fail("They are not at this event.");
            e.Speakers.Add(args[0]);
            return Success($"{args[0]}: speaking privileges removed");
        }

        return Fail("You are not currently running an event.");
    }
}

internal class UnVoice : ChatCommand
{
    public new static string Command = "unvoice";
    public new static string ArgumentText = "<string username>";
    public new static string HelpText = "Remove a participant's ability to speak at a class.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetSocialEvent(user, out var e))
        {
            if (e.MapId != user.Map.Id)
                return Fail("You are not at the event...?");
            // TODO: this is case sensitive which has the potential to be ungodly annoying
            if (!user.Map.Users.ContainsKey(args[0]))
                return Fail("They are not at this event.");
            e.Speakers.Remove(args[0]);
            return Success($"{args[0]}: speaking privileges removed");
        }

        return Fail("You are not currently running an event.");
    }
}

internal class EndMass : ChatCommand
{
    public new static string Command = "endmass";
    public new static string ArgumentText = "none";
    public new static string HelpText = "End an active mass.";
    public new static bool Privileged = true;


    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetSocialEvent(user, out var e))
        {
            if (e.Type != SocialEventType.Mass)
                return Fail("You are not giving a mass here.");
            foreach (var participant in user.Map.Users.Values.Where(predicate: x => x.Distance(user) < 20))
            {
                var reward = Random.Shared.Next(1, 100);
                if (reward <= 80)
                {
                    participant.GiveExperience(Math.Max(Convert.ToUInt32(participant.ExpToLevel * 0.01), 2500));
                    participant.Effect(5, 100);
                    participant.SendSystemMessage($"Praise be to {e.Subtype}.");
                }
                else if (reward <= 90)
                {
                    participant.GiveExperience(Math.Max(Convert.ToUInt32(participant.ExpToLevel * 0.025), 5000));
                    participant.Effect(21, 100);
                    participant.SendSystemMessage($"You are touched by {e.Subtype}.");
                }
                else
                {
                    participant.GiveExperience(Math.Max(Convert.ToUInt32(participant.ExpToLevel * 0.05), 10000));
                    participant.Effect(16, 100);
                    participant.SendSystemMessage($"You are in awe of the power of {e.Subtype}!");
                }
            }

            e.End();
            Game.World.WorldState.Remove<SocialEvent>(user);
            Game.World.WorldState.RemoveIndex<SocialEvent>(user.Map.Id);
            user.Map.MapUnmute();
            return Success("Your mass has concluded.");
        }

        return Fail("You are not giving a mass here.");
    }
}

internal class EndClass : ChatCommand
{
    public new static string Command = "endclass";
    public new static string ArgumentText = "none";
    public new static string HelpText = "End an active class.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetSocialEvent(user, out var e))
        {
            if (e.Type != SocialEventType.Class)
                return Fail("You are not giving a class here.");
            foreach (var participant in user.Map.Users.Values.Where(predicate: x => x.Distance(user) < 20))
            {
                var reward = Random.Shared.Next(1, 100);
                if (reward <= 80)
                {
                    participant.GiveExperience(Math.Max(Convert.ToUInt32(participant.ExpToLevel * 0.01), 2500));
                    participant.Effect(5, 100);
                    participant.SendSystemMessage($"A good {e.Subtype} lecture.");
                }
                else if (reward <= 90)
                {
                    participant.GiveExperience(Math.Max(Convert.ToUInt32(participant.ExpToLevel * 0.025), 5000));
                    participant.Effect(46, 100);
                    participant.SendSystemMessage($"You are well learned in {e.Subtype}.");
                }
                else
                {
                    participant.GiveExperience(Math.Max(Convert.ToUInt32(participant.ExpToLevel * 0.05), 10000));
                    participant.Effect(50, 100);
                    participant.SendSystemMessage($"You gasp at the revelation you just had about {e.Subtype}!");
                }
            }

            e.End();
            Game.World.WorldState.Remove<SocialEvent>(user);
            Game.World.WorldState.RemoveIndex<SocialEvent>(user.Map.Id);
            user.Map.MapUnmute();
            return Success("Your class has concluded.");
        }

        return Fail("You are not giving a class here.");
    }
}