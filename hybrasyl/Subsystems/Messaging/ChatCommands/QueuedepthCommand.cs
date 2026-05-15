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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Servers;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class QueuedepthCommand : ChatCommand
{
    public new static string Command = "queuedepth";
    public new static string ArgumentText = "None";
    public new static string HelpText = "Display current queue depths.";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args) =>
        Success(
            $"Packet Queue Depth: {World.MessageQueue.Count}\n\nControl Message Queue Depth: {World.ControlMessageQueue.Count}",
            MessageTypes.SLATE_WITH_SCROLLBAR);
}