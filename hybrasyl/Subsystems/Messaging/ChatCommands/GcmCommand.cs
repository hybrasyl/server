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

using System.Net;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Networking;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class GcmCommand : ChatCommand
{
    public new static string Command = "gcm";
    public new static string ArgumentText = "none";
    public new static string HelpText = "Dump a bunch of debugging information about server connections.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var gcmContents = "Contents of Global Connection Manifest\n";
        var userContents = "Active Users\n";

        foreach (var pair in GlobalConnectionManifest.ConnectedClients)
        {
            var serverType = string.Empty;
            switch (pair.Value.ServerType)
            {
                case ServerTypes.Lobby:
                    serverType = "Lobby";
                    break;

                case ServerTypes.Login:
                    serverType = "Login";
                    break;

                default:
                    serverType = "World";
                    break;
            }

            try
            {
                gcmContents = gcmContents + string.Format("{0}:{1} - {2}:{3}\n", pair.Key,
                    ((IPEndPoint) pair.Value.Socket.RemoteEndPoint).Address,
                    ((IPEndPoint) pair.Value.Socket.RemoteEndPoint).Port, serverType);
            }
            catch
            {
                gcmContents = gcmContents + string.Format("{0}:{1} disposed\n", pair.Key, serverType);
            }
        }

        foreach (var tehuser in Game.World.WorldState.Values<User>()) userContents = userContents + tehuser.Name + "\n";

        // Report to the end user
        return Success($"{gcmContents}\n\n{userContents}",
            MessageTypes.SLATE_WITH_SCROLLBAR);
    }
}