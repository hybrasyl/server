﻿using Hybrasyl.Creatures;
using Hybrasyl.Enums;
using Hybrasyl.Nations;
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
        public new static string Command = "hp";
        public new static string ArgumentText = "<uint hp>";
        public new static string HelpText = "Set current HP to the specified uint value.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (uint.TryParse(args[0], out uint hp))
            {
                user.Stats.Hp = hp;
                user.UpdateAttributes(StatUpdateFlags.Full);
                return Success($"HP now {hp}");
            }
            return Fail("The value you specified could not be parsed (uint).");
        }
    }

    class BaseHpCommand : ChatCommand
    {
        public new static string Command = "basehp";
        public new static string ArgumentText = "<uint hp>";
        public new static string HelpText = "Set base HP to the specified uint value.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (uint.TryParse(args[0], out uint hp))
            {
                user.Stats.BaseHp = hp;
                user.UpdateAttributes(StatUpdateFlags.Full);
                return Success($"Base HP now {hp}");
            }
            return Fail("The value you specified could not be parsed (uint)");
        }
    }

    class MpCommand : ChatCommand
    {
        public new static string Command = "mp";
        public new static string ArgumentText = "<uint mp>";
        public new static string HelpText = "Set current HP to the specified uint value.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (uint.TryParse(args[0], out uint mp))
            {
                user.Stats.Mp = mp;
                user.UpdateAttributes(StatUpdateFlags.Full);
                return Success($"MP now {mp}");
            }
            return Fail("The value you specified could not be parsed (uint).");
        }
    }

    class BaseMpCommand : ChatCommand
    {
        public new static string Command = "basemp";
        public new static string ArgumentText = "<uint mp>";
        public new static string HelpText = "Set base HP to the specified uint value.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (uint.TryParse(args[0], out uint mp))
            {
                user.Stats.BaseMp = mp;
                user.UpdateAttributes(StatUpdateFlags.Full);
                return Success($"Base HP now {mp}");
            }
            return Fail("The value you specified could not be parsed (uint)");
        }
    }

    class AttrCommand : ChatCommand
    {
        public new static string Command = "attr";
        public new static string ArgumentText = "<string attribute (str|wis|int|con|dex)> <byte value>";
        public new static string HelpText = "Set a specified attribute to the given byte value.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {

            if (!Byte.TryParse(args[1], out byte newStat))
            {
                return Fail($"The value you specified for attribute {args[0]} could not be parsed (byte)");
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
                    return Fail($"Unknown attribute {args[0].ToLower()}");
            }
            user.UpdateAttributes(StatUpdateFlags.Stats);
            return Success($"{args[0].ToLower()} now {newStat}.");
        }
    }

    class GoldCommand : ChatCommand
    {
        public new static string Command = "gold";
        public new static string ArgumentText = "<uint gold>";
        public new static string HelpText = "Give yourself the specified amount of gold.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!uint.TryParse(args[0], out uint amount))
                return Fail("The value you specified could not be parsed (uint)");

            user.Gold = amount;
            user.UpdateAttributes(StatUpdateFlags.Experience);
            return Success($"{user.Name} - Gold increased by {amount}.");
        }
    }

    class DamageCommand : ChatCommand
    {
        public new static string Command = "damage";
        public new static string ArgumentText = "<double damage>";
        public new static string HelpText = "Damage yourself for the specified amount. Careful...";
        public new static bool Privileged = false;
        
        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!double.TryParse(args[0], out double amount))
                return Fail("The value you specified could not be parsed (double)");
            user.Damage(amount);
            return Success($"{user.Name} - damaged by {amount}.");

        }
    }

    class ExpCommand : ChatCommand
    {
        public new static string Command = "exp";
        public new static string ArgumentText = "<uint experience>";
        public new static string HelpText = "Award yourself a given amount of experience.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!uint.TryParse(args[0], out uint amount))
                return Fail("The value you specified could not be parsed (uint)");
            user.ShareExperience(amount);
            return Success($"{user.Name} - awarded {amount} XP.");

        }

    }

    class ExpResetCommand : ChatCommand
    {
        public new static string Command = "expreset";
        public new static string ArgumentText = "<none>";
        public new static string HelpText = "Reset level, experience, and level points (level 1, 0 XP, 0 points).";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            user.LevelPoints = 0;
            user.Stats.Level = 1;
            user.Stats.Experience = 0;
            user.UpdateAttributes(StatUpdateFlags.Full);
            return Success($"{user.Name} - XP reset.");

        }
    }

    class NationCommand : ChatCommand
    {
        public new static string Command = "nation";
        public new static string ArgumentText = "<string nation>";
        public new static string HelpText = "Make yourself a citizen of the specified nation.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValue(args[0], out Nation nation))
            {
                user.Nation = nation;
                return Success($"Citizenship set to {args[0]}");
            }
            else return Fail("Nation not found");

        }
    }

    class ClassCommand : ChatCommand
    {
        public new static string Command = "class";
        public new static string ArgumentText = "<string class>";
        public new static string HelpText = "Change your class to the one specified.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            var cls = args[0].ToLower();
            if (Constants.CLASSES.TryGetValue(args[0], out int classValue))
            {
                user.Class = (Enums.Class)classValue;
                return Success("Class changed to {args[0]}.");
            }
            return Fail("I know nothing about that class");

        }
    }

    class LevelCommand : ChatCommand
    {
        public new static string Command = "level";
        public new static string ArgumentText = "<byte level>";
        public new static string HelpText = "Change your level to the one specified.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Byte.TryParse(args[0], out byte newLevel))
            {
                user.Stats.Level = newLevel > Constants.MAX_LEVEL ? (byte)Constants.MAX_LEVEL : newLevel;
                user.UpdateAttributes(StatUpdateFlags.Full);
                return Success($"Level changed to {newLevel}");
            }
            else return Fail("The value you specified could not be parsed (byte)");

        }
    }

    class SkillCommand : ChatCommand
    {
        public new static string Command = "skill";
        public new static string ArgumentText = "<string skillName>";
        public new static string HelpText = "Add the specified skill to your skillbook.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValueByIndex(args[0], out Castables.Castable castable))
            {
                user.AddSkill(castable);
                return Success($"{castable.Name} added to skillbook.");
            }
            else return Fail($"The castable {args[0]} could not be found");

        }
    }

    class SpellCommand : ChatCommand
    {
        public new static string Command = "spell";
        public new static string ArgumentText = "<string spellName>";
        public new static string HelpText = "Add the specified spell to your spellbook.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValueByIndex(args[0], out Castables.Castable castable))
            {
                user.AddSpell(castable);
                return Success($"{castable.Name} added to spellbook.");
            }
            else return Fail($"The castable {args[0]} could not be found");

        }
    }

    class MasterCommand : ChatCommand
    {
        public new static string Command = "master";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Toggle mastership.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            user.IsMaster = !user.IsMaster;
            return Success(user.IsMaster ? "Mastership granted" : "Mastership removed");

        }

    }
}
