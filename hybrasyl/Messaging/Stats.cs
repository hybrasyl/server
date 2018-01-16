using Hybrasyl.Creatures;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Messaging
{
    class HpCommand : ChatCommand
    {
        public new static string Command = "/hp";
        public new static string ArgumentText = "<uint hp>";
        public new static string HelpText = "Set current HP to the specified uint value.";
        public new static bool Privileged = false;

        public override (bool Success, string Message) Run(User user, params string[] args)
        {
            if (uint.TryParse(args[0], out uint hp))
            {
                user.Stats.Hp = hp;
                user.UpdateAttributes(StatUpdateFlags.Full);
                return Success;
            }
            return (false, "The value you specified could not be parsed.");
        }
    }

    class BaseHpCommand : ChatCommand
    {
        public new static string Command = "/basehp";
        public new static string ArgumentText = "<uint hp>";
        public new static string HelpText = "Set base HP to the specified uint value.";
        public new static bool Privileged = false;

        public override (bool Success, string Message) Run(User user, params string[] args)
        {
            if (uint.TryParse(args[0], out uint hp))
            {
                user.Stats.BaseHp = hp;
                user.UpdateAttributes(StatUpdateFlags.Full);
                return Success;
            }
            return (false, "The value you specified could not be parsed.");
        }
    }

    class StatCommand : ChatCommand
    {
        public new static string Command = "/stat";
        public new static string ArgumentText = "/stat (str|dex|int|con|wis) <byte value>";
        public new static string HelpText = "Set a given attribute to the specified byte value.";
        public new static bool Privileged = false;

        public override (bool Success, string Message) Run(User user, params string[] args)
        {

            if (!Byte.TryParse(args[1], out byte newStat))
            {
                return (false, $"The value you specified for attribute {args[0]} could not be parsed.");
            }

            switch (args[0].ToLower())
            {
                case "str":
                    user.Stats.BaseStr = newStat;
                    break;

                case "con":
                    user.Stats.BaseCon = newStat;
                    break;

                case "dex":
                    user.Stats.BaseDex = newStat;
                    break;

                case "wis":
                    user.Stats.BaseWis = newStat;
                    break;
                case "int":
                    user.Stats.BaseInt = newStat;
                    break;
                default:
                    return (false, $"Unknown attribute {args[0].ToLower()} ");
            }
            user.UpdateAttributes(StatUpdateFlags.Stats);
            return Success;
        }
    }

    class GoldCommand : ChatCommand
    {
        public new static string Command = "/gold";
        public new static string ArgumentText = "<uint gold>";
        public new static string HelpText = "Give yourself the specified amount of gold.";
        public new static bool Privileged = false;

        public override (bool Success, string Message) Run(User user, params string[] args)
        {
            if (!uint.TryParse(args[0], out uint amount))
                return (false, "The value you specified is not a uint.");

            user.Gold = amount;
            user.UpdateAttributes(StatUpdateFlags.Experience);
            return Success;
        }
    }
}
