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

internal class ReloadnpcCommand : ChatCommand
{
    public new static string Command = "reloadnpc";
    public new static string ArgumentText = "<string npcname>";
    public new static string HelpText = "Reload the given NPC (dump the script and reload from disk)";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out Merchant merchant))
        {
            if (Game.World.ScriptProcessor.TryGetScript(merchant.Name, out var script))
            {
                script.Reload();
                merchant.Ready = false; // Force reload next time NPC processes an interaction
                merchant.Say("...What? What happened?");
                return Success($"NPC {args[0]} - script {script.Name} reloaded. Clicking should re-run OnSpawn.");
            }

            return Fail("NPC found but script not found...?");
        }

        return Fail($"NPC {args[0]} not found.");
    }
}