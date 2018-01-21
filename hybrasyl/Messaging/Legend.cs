using Hybrasyl.Enums;
using Hybrasyl.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Messaging
{
    class LegendCommand : ChatCommand
    {
        public new static string Command = "legend";
        public new static string ArgumentText = "<string legendText> <byte icon> <byte color> [<int quantity> <datetime date>]";
        public new static string HelpText = "Add a legend mark with the specified text, icon and color, and optionally with the given quantity and date.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Enum.TryParse(args[1], out LegendIcon icon) && Enum.TryParse(args[2], out LegendColor color))
            {
                DateTime time = DateTime.Now;
                int qty = 1;
                if (args.Length > 3)
                    int.TryParse(args[3], out qty);
                if (args.Length == 5)
                    DateTime.TryParse(args[4], out time);

                user.Legend.AddMark(icon, color, args[0]);
            }
            else return Fail("The value you specified could not be parsed (LegendIcon/Color)");
            return Success("Legend added.");

        }
    }

    class LegendclearCommand : ChatCommand
    {
        public new static string Command = "legend";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Clear your legend. WARNING: Not reversible.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            user.Legend.Clear();
            return Success("Legend cleared.");
        }
    }
}
