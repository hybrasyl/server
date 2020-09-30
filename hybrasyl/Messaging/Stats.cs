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
 
using Hybrasyl.Enums;
using Hybrasyl.Xml;
using Hybrasyl.Objects;
using System;
using System.Linq;

namespace Hybrasyl.Messaging
{
    class HpCommand : ChatCommand
    {
        public new static string Command = "hp";
        public new static string ArgumentText = "<uint hp>";
        public new static string HelpText = "Set current HP to the specified uint value.";
        public new static bool Privileged = true;

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
        public new static bool Privileged = true;

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
        public new static bool Privileged = true;

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
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (uint.TryParse(args[0], out uint mp))
            {
                user.Stats.BaseMp = mp;
                user.UpdateAttributes(StatUpdateFlags.Full);
                return Success($"Base MP now {mp}");
            }
            return Fail("The value you specified could not be parsed (uint)");
        }
    }

    class AttrCommand : ChatCommand
    {
        public new static string Command = "attr";
        public new static string ArgumentText = "<string attribute> <byte value>";
        public new static string HelpText = "Set a specified attribute (str/con etc) to the given byte value.";
        public new static bool Privileged = true;

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
        public new static bool Privileged = true;

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
        public new static bool Privileged = true;
        
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
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!uint.TryParse(args[0], out uint amount))
                return Fail("The value you specified could not be parsed (uint)");
            user.ShareExperience(amount, user.Stats.Level);
            return Success($"{user.Name} - awarded {amount} XP.");

        }

    }

    class ExpResetCommand : ChatCommand
    {
        public new static string Command = "expreset";
        public new static string ArgumentText = "<none>";
        public new static string HelpText = "Reset level, experience, and level points (level 1, 0 XP, 0 points).";
        public new static bool Privileged = true;

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
        public new static bool Privileged = true;

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
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            var cls = args[0].ToLower();
            if (Constants.CLASSES.TryGetValue(args[0].ToLower(), out int classValue))
            {
                user.Class = (Class)classValue;
                return Success($"Class changed to {args[0]}.");
            }
            return Fail("I know nothing about that class");

        }
    }

    class LevelCommand : ChatCommand
    {
        public new static string Command = "level";
        public new static string ArgumentText = "<byte level>";
        public new static string HelpText = "Change your level to the one specified.";
        public new static bool Privileged = true;

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

    class ForgetCommand : ChatCommand
    {
        public new static string Command = "forget";
        public new static string ArgumentText = "<string castable>";
        public new static string HelpText = "Forget the specified castable (remove from skill or spell book)";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValueByIndex(args[0], out Castable castable))
            {
                if (user.SpellBook.Contains(castable.Id))
                {
                    var i = user.SpellBook.SlotOf(castable.Id);
                    user.SpellBook.Remove(i);
                    user.SendClearSpell(i);
                }
                else if (user.SkillBook.Contains(castable.Id))
                {
                    var i = user.SkillBook.SlotOf(castable.Id);
                    user.SkillBook.Remove(i);
                    user.SendClearSkill(i);
                }
                else
                    return Fail("You don't know that skill or spell.");
                return Success($"{args[0]} removed.");
            }
            return Fail("That castable does not exist.");
        }
    }

    class ClevelCommand : ChatCommand
    {
        public new static string Command = "clevel";
        public new static string ArgumentText = "<string skillName> <int level>";
        public new static string HelpText = "Change the specified skill or spell to a given mastery level.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            BookSlot slot;
            if (Game.World.WorldData.TryGetValueByIndex(args[0], out Castable castable))
            {
                if (!uint.TryParse(args[1], out uint i))
                    return Fail("What kind of level is that?");

                if (user.SpellBook.Contains(castable.Id))
                {
                    slot = user.SpellBook.Single(x => x.Castable.Name == castable.Name);
                    var uses = ((double)castable.Mastery.Uses) * ((double)i / 100);
                    slot.UseCount = Convert.ToUInt32(uses);
                    user.SendSpellUpdate(slot, user.SpellBook.SlotOf(castable.Id));
                }
                else if (user.SkillBook.Contains(castable.Id))
                {
                    slot = user.SkillBook.Single(x => x.Castable.Name == castable.Name);
                    var uses = ((double)castable.Mastery.Uses) * ((double)i / 100);
                    slot.UseCount = Convert.ToUInt32(uses);
                    user.SendSkillUpdate(slot, user.SkillBook.SlotOf(castable.Id));
                }
                else
                    return Fail("You don't know that spell or skill.");
                return Success($"Castable {slot.Castable.Name} set to level {i}.");
            }
            return Fail("Castable not found");
        }
    }

    class SkillCommand : ChatCommand
    {
        public new static string Command = "skill";
        public new static string ArgumentText = "<string skillName>";
        public new static string HelpText = "Add the specified skill to your skillbook.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValueByIndex(args[0], out Castable castable))
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
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (Game.World.WorldData.TryGetValueByIndex(args[0], out Castable castable))
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
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            user.IsMaster = !user.IsMaster;
            return Success(user.IsMaster ? "Mastership granted" : "Mastership removed");

        }

    }

    class EquipmentDurabilityCommand : ChatCommand
    {
        public new static string Command = "dura";
        public new static string ArgumentText = "<uint value>";
        public new static string HelpText = "Set durability of all inventory and equipment to the specified uint value.";
        public new static bool Privileged = true;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (uint.TryParse(args[0], out uint dura))
            {
                for (byte i = 0; i < user.Equipment.Size; i++)
                {
                    if (user.Equipment[i] == null) continue;
                    if (user.Equipment[i].MaximumDurability < dura) continue;
                    user.Equipment[i].Durability = dura;
                    user.AddEquipment(user.Equipment[i], i);
                }

                for (byte i = 0; i < user.Inventory.Size; i++)
                {
                    if (user.Inventory[i] == null) continue;
                    if (user.Inventory[i].MaximumDurability < dura) continue;
                    user.Inventory[i].Durability = dura;
                    user.SendItemUpdate(user.Inventory[i], i);
                }
                user.UpdateAttributes(StatUpdateFlags.Full);
                return Success($"Durability is now {dura} for all items.");
            }
            return Fail("The value you specified could not be parsed (uint)");
        }
    }
}
