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

namespace Hybrasyl.ChatCommands
{
    public struct ChatCommandResult
    {
        public bool Success;
        public string Message;
        public byte MessageType;
    }

    public abstract class ChatCommand
    {
        public string Command { get; }
        public string ArgumentText { get; }
        public string HelpText { get; }
        public bool Privileged { get; }
        public int ArgumentCount { get; }
        public static ChatCommandResult Success(string ErrorMessage = null, byte MessageType = MessageTypes.SYSTEM) => 
            new ChatCommandResult() { Success = true, Message = ErrorMessage ?? string.Empty, MessageType = MessageType };
        public static ChatCommandResult Fail(string ErrorMessage, byte MessageType = MessageTypes.SYSTEM) => new ChatCommandResult() { Success = false, Message = ErrorMessage, MessageType = MessageType };
        public static ChatCommandResult Run(User user, params string[] args) { return Success(); }
    }

}
