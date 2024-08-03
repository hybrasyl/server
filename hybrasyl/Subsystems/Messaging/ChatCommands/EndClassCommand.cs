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
using Hybrasyl.Subsystems.Players;
using System;
using System.Linq;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class EndClassCommand : ChatCommand
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