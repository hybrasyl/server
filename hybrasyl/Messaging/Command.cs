using Hybrasyl.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Messaging
{
    public interface IChatCommand
    {
        string Command { get; }
        string ArgumentText { get; }
        string HelpText { get; }
        bool Privileged { get; }
        (bool Success, string Message) Run(User user, params string[] args);

    }

    public abstract class ChatCommand : IChatCommand
    {
        public string Command { get; }
        public string ArgumentText { get; }
        public string HelpText { get; }
        public bool Privileged { get; }
        public static (bool Success, string Message) Success = (true, "Success");
        public abstract (bool Success, string Message) Run(User user, params string[] args);
    }

}
