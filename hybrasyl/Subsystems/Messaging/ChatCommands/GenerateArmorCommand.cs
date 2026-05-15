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

using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using System;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class GenerateArmorCommand : ChatCommand
{
    public static int GeneratedId;
    public new static string Command = "generate";
    public new static string ArgumentText = "<string> type <string> gender <ushort> sprite <ushort> sprite";
    public new static string HelpText = "Used for testing sprite vs display sprite. armor Female 1 1";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length < 4) return Fail("Wrong number of arguments supplied.");
        ushort sprite;
        ushort displaysprite;
        if (!ushort.TryParse(args[2], out sprite)) return Fail("Sprite must be a number.");
        if (!ushort.TryParse(args[3], out displaysprite)) return Fail("Displaysprite must be a number.");
        switch (args[0].ToLower())
        {
            case "armor":
            {
                var item = new Item
                {
                    Name = "GeneratedArmor" + GeneratedId,
                    Properties = new ItemProperties
                    {
                        Stackable = new Stackable { Max = 1 },
                        Physical = new Physical
                        {
                            Durability = 1000,
                            Value = 1,
                            Weight = 1
                        },
                        Restrictions = new ItemRestrictions
                        {
                            Gender = (Gender) Enum.Parse(typeof(Gender), args[1]),
                            Level = new RestrictionsLevel
                            {
                                Min = 1
                            }
                        },
                        Appearance = new Appearance
                        {
                            BodyStyle = (ItemBodyStyle) Enum.Parse(typeof(ItemBodyStyle), args[1]),
                            Sprite = sprite,
                            DisplaySprite = displaysprite
                        },
                        Equipment = new Equipment
                        {
                            Slot = EquipmentSlot.Armor
                        }
                    }
                };
                Game.World.WorldData.AddWithIndex(item, item.Id, item.Name);
                user.AddItem(item.Name);
            }
                break;
            case "coat":
            {
                var item = new Item
                {
                    Name = "GeneratedArmor" + GeneratedId,
                    Properties = new ItemProperties
                    {
                        Stackable = new Stackable { Max = 1 },
                        Physical = new Physical
                        {
                            Durability = 1000,
                            Value = 1,
                            Weight = 1
                        },
                        Restrictions = new ItemRestrictions
                        {
                            Gender = (Gender) Enum.Parse(typeof(Gender), args[1]),
                            Level = new RestrictionsLevel
                            {
                                Min = 1
                            }
                        },
                        Appearance = new Appearance
                        {
                            BodyStyle = (ItemBodyStyle) Enum.Parse(typeof(ItemBodyStyle), args[1]),
                            Sprite = sprite,
                            DisplaySprite = displaysprite
                        },
                        Equipment = new Equipment
                        {
                            Slot = EquipmentSlot.Trousers
                        }
                    }
                };
                Game.World.WorldData.AddWithIndex(item, item.Id, item.Name);
                user.AddItem(item.Name);
            }
                break;
        }

        GeneratedId++;
        return Success($"GeneratedArmor{GeneratedId - 1} added to World Data.");
    }
}