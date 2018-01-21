using Hybrasyl.Objects;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Messaging
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
