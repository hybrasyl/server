using Hybrasyl.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Messaging
{
    class TimeCommand : ChatCommand
    {
        public new static string Command = "time";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Display the current server time.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args) => Success($"{HybrasylTime.Now().ToString()}");
    }

    class TimeconvertCommand : ChatCommand
    {
        public new static string Command = "timeconvert";
        public new static string ArgumentText = "<string timeformat> <string time>";
        public new static string HelpText = "Convert a time between aisling/terran formats.";
        public new static bool Privileged = false;
        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (args[0].ToLower() == "aisling")
            {
                var hybrasylTime = HybrasylTime.FromString(args[1]);
                return Success($"{args[1]} is {hybrasylTime.ToString()}.");
            }
            else if (args[1].ToLower() == "terran")
            {
                if (DateTime.TryParse(args[1], out DateTime time))
                {
                    var hybrasylTime = HybrasylTime.ConvertToHybrasyl(time);
                    return Success($"{args[1]} is {hybrasylTime.ToString()}.");
                }
                return Fail("Couldn't parse passed value (datetime)");
            }
            else return Fail("Unsupported time format. Try 'aisling' or 'terran'");
            
        }
    }
}
