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
using Hybrasyl.Messaging;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Generic;

namespace Hybrasyl;

internal interface IPacket
{
    byte OpCode { get; }
    ServerPacket ToPacket();
}

public class ServerPacketStructures
{
    internal class AddSpell
    {
        private static byte OpCode;

        internal AddSpell()
        {
            OpCode = OpCodes.AddSpell;
        }

        internal byte Slot { get; set; }
        internal byte Icon { get; set; }
        internal byte UseType { get; set; }
        internal byte Lines { get; set; }
        internal string Name { get; set; }
        internal string Prompt { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Slot);
            packet.WriteUInt16(Icon);
            packet.WriteByte(UseType);
            packet.WriteString8(Name);
            packet.WriteString8(Prompt);
            packet.WriteByte(Lines);

            return packet;
        }
    }


    internal class UseSkill
    {
        private static byte OpCode;

        internal UseSkill()
        {
            OpCode = OpCodes.UseSkill;
        }

        internal byte Slot { get; set; }
    }

    internal class StatusBar
    {
        private static readonly byte OpCode = OpCodes.StatusBar;
        internal StatusBarColor BarColor;

        internal ushort Icon;

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt16(Icon);
            packet.WriteByte((byte)BarColor);
            return packet;
        }
    }


    internal class CancelCast
    {
        private static byte OpCode;

        internal CancelCast()
        {
            OpCode = OpCodes.CancelCast;
        }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(0);
            return packet;
        }
    }

    internal class Cooldown
    {
        private static readonly byte OpCode = OpCodes.Cooldown;
        internal uint Length;

        internal byte Pane;
        internal byte Slot;

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Pane);
            packet.WriteByte(Slot);
            packet.WriteUInt32(Length);

            return packet;
        }
    }

    internal class PlayerAnimation
    {
        private readonly byte OpCode = OpCodes.PlayerAnimation;

        internal uint UserId { get; set; }
        internal short Speed { get; set; }
        internal byte Animation { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt32(UserId);
            packet.WriteByte(Animation);
            packet.WriteInt16(Speed);
            packet.WriteByte(byte.MaxValue);
            return packet;
        }
    }

    internal class Exchange
    {
        internal const string CancelMessage = "Exchange was cancelled.";
        internal const string ConfirmMessage = "You exchanged.";

        private static readonly byte OpCode =
            OpCodes.Exchange;

        internal byte Action;
        internal uint Gold;
        internal byte ItemColor;
        internal string ItemName;
        internal byte ItemSlot;
        internal ushort ItemSprite;
        internal uint RequestorId;
        internal string RequestorName;
        internal bool Side;

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Action);
            switch (Action)
            {
                case ExchangeActions.Initiate:
                    {
                        packet.WriteUInt32(RequestorId);
                        packet.WriteString8(RequestorName);
                    }
                    break;
                case ExchangeActions.QuantityPrompt:
                    {
                        packet.WriteByte(ItemSlot);
                    }
                    break;
                case ExchangeActions.ItemUpdate:
                    {
                        packet.WriteByte((byte)(Side ? 0 : 1));
                        packet.WriteByte(ItemSlot);
                        packet.WriteUInt16((ushort)(0x8000 + ItemSprite));
                        packet.WriteByte(ItemColor);
                        packet.WriteString8(ItemName);
                    }
                    break;
                case ExchangeActions.GoldUpdate:
                    {
                        packet.WriteByte((byte)(Side ? 0 : 1));
                        packet.WriteUInt32(Gold);
                    }
                    break;
                case ExchangeActions.Cancel:
                    {
                        packet.WriteByte((byte)(Side ? 0 : 1));
                        packet.WriteString8(CancelMessage);
                    }
                    break;
                case ExchangeActions.Confirm:
                    {
                        packet.WriteByte((byte)(Side ? 0 : 1));
                        packet.WriteString8(ConfirmMessage);
                    }
                    break;
            }

            return packet;
        }
    }

    internal class CastLine
    {
        private readonly byte OpCode;

        internal CastLine()
        {
            OpCode = OpCodes.CastLine;
        }

        internal string LineText { get; set; }
        internal byte ChatType { get; set; }
        internal uint TargetId { get; set; }
        internal byte LineLength { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(ChatType);
            packet.WriteUInt32(TargetId);
            packet.WriteByte(LineLength);
            packet.WriteString(LineText);
            packet.WriteByte(0);
            packet.WriteByte(0);
            packet.WriteByte(0);
            return packet;
        }
    }

    internal class PlaySound
    {
        private readonly byte OpCode;

        internal PlaySound()
        {
            OpCode = OpCodes.PlaySound;
        }

        internal byte Sound { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Sound);
            return packet;
        }
    }

    internal class HealthBar
    {
        private readonly byte OpCode;

        internal HealthBar()
        {
            OpCode = OpCodes.HealthBar;
        }

        internal uint ObjId { get; set; }

        internal byte CurrentPercent { get; set; }
        internal byte? Sound { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt32(ObjId);
            packet.WriteByte(0);
            packet.WriteByte(CurrentPercent);
            packet.WriteByte(Sound ?? 0xFF);

            return packet;
        }
    }

    internal class EffectAnimation
    {
        private readonly byte OpCode;

        internal EffectAnimation()
        {
            OpCode = OpCodes.SpellAnimation;
        }

        internal uint TargetId { get; set; }
        internal uint? SourceId { get; set; }
        internal uint TargetAnimation { get; set; }
        internal uint? SourceAnimation { get; set; }
        internal short Speed { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            var position = packet.Position;
            packet.WriteUInt32(TargetId);
            packet.WriteUInt32(SourceId ?? 0);
            packet.WriteUInt16((ushort)TargetAnimation);
            packet.WriteUInt16((ushort)(SourceAnimation ?? 0));
            packet.WriteInt16(Speed);
            packet.WriteInt32(0);
            return packet;
        }
    }

    internal class DisplayUser
    {
        private readonly byte OpCode;

        internal DisplayUser()
        {
            OpCode = OpCodes.DisplayUser;
        }
        // General notes about this god awful packet:

        /* Offsets:
           00-0F: no human body + pants
           10-1F: male human body + pants
           20-2F: female human body, no pants
           30-3F: male spirit + pants
           40-4F: female spirit, no pants
           50-5F: invisible male body + pants
           60-6F: invisible female body, no pants
           70-7F: male doll body + pants
           80-8F: male mounted body + pants
           90-9F: female mounted body, no pants
           A0-FF: no human body + pants
        */

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt16(X);
            packet.WriteUInt16(Y);
            packet.WriteByte((byte)Direction);
            packet.WriteUInt32(Id);
            packet.WriteUInt16(Helmet);

            if (!DisplayAsMonster)
            {
                packet.WriteByte((byte)((byte)Gender * 16 + BodySpriteOffset));
                packet.WriteUInt16(Armor);
                packet.WriteByte(Boots);
                packet.WriteUInt16(Armor);
                packet.WriteByte(Shield);
                packet.WriteUInt16(Weapon);
                packet.WriteByte(HairColor);
                packet.WriteByte(BootsColor);
                packet.WriteByte(FirstAccColor);
                packet.WriteUInt16(FirstAcc);
                packet.WriteByte(SecondAccColor);
                packet.WriteUInt16(SecondAcc);
                packet.WriteByte(ThirdAccColor);
                packet.WriteUInt16(ThirdAcc);
                packet.WriteByte((byte)LanternSize);
                packet.WriteByte((byte)RestPosition);
                packet.WriteUInt16(Overcoat);
                packet.WriteByte(OvercoatColor);
                packet.WriteByte((byte)SkinColor);
                packet.WriteBoolean(Invisible);
                packet.WriteByte(FaceShape);
            }
            else
            {
                packet.WriteUInt16(MonsterSprite);
                packet.WriteByte(HairColor);
                packet.WriteByte(BootsColor);
                // Unknown
                packet.WriteByte(0x00);
                packet.WriteByte(0x00);
                packet.WriteByte(0x00);
                packet.WriteByte(0x00);
                packet.WriteByte(0x00);
                packet.WriteByte(0x00);
            }

            packet.WriteByte((byte)NameStyle);
            packet.WriteString8(Name ?? string.Empty);
            packet.WriteString8(GroupName ?? string.Empty);

            return packet;
        }

        #region Location information

        internal byte X { get; set; }
        internal byte Y { get; set; }
        internal Direction Direction { get; set; }
        internal uint Id { get; set; }

        #endregion

        #region Appearance

        internal string Name { get; set; }
        internal Gender Gender { get; set; }
        internal ushort Helmet { get; set; }
        internal byte BodySpriteOffset { get; set; }
        internal ushort Armor { get; set; }
        internal byte Shield { get; set; }
        internal ushort Weapon { get; set; }
        internal byte Boots { get; set; }
        internal byte HairColor { get; set; }
        internal byte BootsColor { get; set; }
        internal byte FirstAccColor { get; set; }
        internal ushort FirstAcc { get; set; }
        internal byte SecondAccColor { get; set; }
        internal ushort SecondAcc { get; set; }
        internal byte ThirdAccColor { get; set; }
        internal ushort ThirdAcc { get; set; }
        internal RestPosition RestPosition { get; set; }
        internal ushort Overcoat { get; set; }
        internal byte OvercoatColor { get; set; }
        internal SkinColor SkinColor { get; set; }
        internal bool Invisible { get; set; }
        internal byte FaceShape { get; set; }
        internal LanternSize LanternSize { get; set; }
        internal NameDisplayStyle NameStyle { get; set; }
        internal string GroupName { get; set; }
        internal bool DisplayAsMonster { get; set; }
        internal ushort MonsterSprite { get; set; }

        #endregion
    }

    internal class MerchantResponse
    {
        private readonly byte OpCode;
        private byte Unknow4 = 2;
        internal byte Unknow7 = 1;


        internal MerchantResponse()
        {
            OpCode = OpCodes.NpcReply;
        }

        internal MerchantDialogType MerchantDialogType { get; set; }
        internal MerchantDialogObjectType MerchantDialogObjectType { get; set; }
        internal uint ObjectId { get; set; }
        internal ushort Tile1 { get; set; }
        internal byte Color1 { get; set; } //affect items only
        internal ushort Tile2 { get; set; }
        internal byte Color2 { get; set; } //affect item only
        internal byte PortraitType { get; set; } //portrait style. 0 = anime 1 = sprite
        internal byte NameLength => Convert.ToByte(Name.Length);
        internal string Name { get; set; }
        internal ushort TextLength { get; set; }
        internal string Text { get; set; }
        internal byte Slot { get; set; }
        internal uint Quantity { get; set; }

        internal MerchantOptions Options { get; set; }
        internal MerchantOptionsWithArgument OptionsWithArgument { get; set; }
        internal MerchantInput Input { get; set; }
        internal MerchantInputWithArgument InputWithArgument { get; set; }
        internal UserInventoryItems UserInventoryItems { get; set; }
        internal MerchantShopItems ShopItems { get; set; }
        internal MerchantSpells Spells { get; set; }
        internal MerchantSkills Skills { get; set; }
        internal UserSkillBook UserSkills { get; set; }
        internal UserSpellBook UserSpells { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte((byte)MerchantDialogType);
            packet.WriteByte((byte)MerchantDialogObjectType);
            packet.WriteUInt32(ObjectId);
            packet.WriteByte(0);
            packet.WriteInt16((short)Tile1);
            packet.WriteByte(0);
            packet.WriteByte(1);
            packet.WriteInt16((short)Tile1);
            packet.WriteByte(0);
            packet.WriteByte(0);
            packet.WriteString8(Name);
            packet.WriteString16(Text);
            if (MerchantDialogType == MerchantDialogType.Options)
            {
                packet.WriteByte(Options.OptionsCount);
                foreach (var opt in Options.Options)
                {
                    packet.WriteString8(opt.Text);
                    packet.WriteUInt16(opt.Id);
                }
            }

            if (MerchantDialogType == MerchantDialogType.OptionsWithArgument)
            {
                packet.WriteString8(OptionsWithArgument.Argument);
                packet.WriteByte(OptionsWithArgument.OptionsCount);
                foreach (var opt in OptionsWithArgument.Options)
                {
                    packet.WriteString8(opt.Text);
                    packet.WriteUInt16(opt.Id);
                }
            }

            if (MerchantDialogType == MerchantDialogType.Input) packet.WriteUInt16(Input.Id);
            if (MerchantDialogType == MerchantDialogType.InputWithArgument)
            {
                packet.WriteString8(InputWithArgument.Argument);
                packet.WriteUInt16(InputWithArgument.Id);
            }

            if (MerchantDialogType == MerchantDialogType.MerchantShopItems)
            {
                packet.WriteUInt16(ShopItems.Id);
                packet.WriteUInt16(ShopItems.ItemsCount);
                foreach (var item in ShopItems.Items)
                {
                    packet.WriteUInt16(item.Tile);
                    packet.WriteByte(item.Color);
                    packet.WriteUInt32(item.Price);
                    packet.WriteString8(item.Name);
                    packet.WriteString8(item.Description);
                }
            }

            if (MerchantDialogType == MerchantDialogType.MerchantSkills)
            {
                packet.WriteUInt16(Skills.Id);
                packet.WriteUInt16(Skills.SkillsCount);
                foreach (var skill in Skills.Skills)
                {
                    packet.WriteByte(skill.IconType);
                    packet.WriteUInt16(skill.Icon);
                    packet.WriteByte(skill.Color);
                    packet.WriteString8(skill.Name);
                }
            }

            if (MerchantDialogType == MerchantDialogType.MerchantSpells)
            {
                packet.WriteUInt16(Spells.Id);
                packet.WriteUInt16(Spells.SpellsCount);
                foreach (var spell in Spells.Spells)
                {
                    packet.WriteByte(spell.IconType);
                    packet.WriteUInt16(spell.Icon);
                    packet.WriteByte(spell.Color);
                    packet.WriteString8(spell.Name);
                }
            }

            if (MerchantDialogType == MerchantDialogType.UserSkillBook) packet.WriteUInt16(UserSkills.Id);
            if (MerchantDialogType == MerchantDialogType.UserSpellBook) packet.WriteUInt16(UserSpells.Id);
            if (MerchantDialogType == MerchantDialogType.UserInventoryItems)
            {
                packet.WriteUInt16(UserInventoryItems.Id);
                packet.WriteByte(UserInventoryItems.InventorySlotsCount);
                foreach (var slot in UserInventoryItems.InventorySlots) packet.WriteByte(slot);
            }

            return packet;
        }
    }

    internal class Turn
    {
        private readonly byte OpCode;

        internal Turn()
        {
            OpCode = OpCodes.CreatureDirection;
        }

        internal uint Id { get; set; }
        internal byte Direction { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt32(Id);
            packet.WriteByte(Direction);
            return packet;
        }
    }

    internal class PlayerProfile
    {
        private readonly byte OpCode;

        internal PlayerProfile()
        {
            OpCode = OpCodes.SelfProfile;
        }

        internal User Player { get; set; }
        internal byte NationFlag { get; set; }
        internal string GuildRank { get; set; }
        internal string CurrentTitle { get; set; }
        internal UserGroup Group { get; set; }
        internal bool IsGrouped { get; set; }
        internal bool CanGroup { get; set; }
        internal GroupRecruit GroupRecruit { get; set; }
        internal byte Class { get; set; }
        internal string ClassName { get; set; }
        internal ushort PlayerDisplay { get; set; }
        internal string GuildName { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(NationFlag);
            packet.WriteString8(GuildRank);
            packet.WriteString8(CurrentTitle);
            if (!IsGrouped)
            {
                packet.WriteString8("Adventuring Alone");
            }
            else
            {
                var ret = "Group members\n";
                foreach (var member in Group.Members)
                    ret += member == Group.Founder ? $"* {member.Name}\n" : $"  {member.Name}\n";
                ret += $"Total {Group.Members.Count}";

                packet.WriteString8(ret);
            }

            packet.WriteBoolean(CanGroup);
            packet.WriteBoolean(GroupRecruit != null);
            GroupRecruit?.WriteInfo(packet);
            packet.WriteByte(Class);
            packet.WriteByte(0x00);
            packet.WriteByte(0x00);
            packet.WriteString8(Player.IsMaster ? "Master" : Player.Class.ToString());
            packet.WriteString8(GuildName ?? string.Empty);
            packet.WriteByte((byte)(Player.Legend.Count > 255 ? 255 : Player.Legend.Count));
            foreach (var mark in Player.Legend)
            {
                packet.WriteByte((byte)mark.Icon);
                packet.WriteByte((byte)mark.Color);
                packet.WriteString8(mark.Prefix);
                packet.WriteString8(mark.ToString());
            }

            packet.WriteByte(0x00);
            packet.WriteUInt16(PlayerDisplay);
            packet.WriteByte(0x02);
            packet.WriteUInt32(0x00);
            packet.WriteByte(0x00);
            return packet;
        }
    }

    internal class RemoveWorldObject
    {
        private readonly byte OpCode;

        internal RemoveWorldObject()
        {
            OpCode = OpCodes.RemoveWorldObject;
        }

        internal uint Id { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt32(Id);

            return packet;
        }
    }

    internal class Location
    {
        private readonly byte OpCode;


        internal Location()
        {
            OpCode = OpCodes.Location;
        }

        internal ushort X { get; set; }
        internal ushort Y { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt16(X);
            packet.WriteUInt16(Y);
            packet.WriteUInt16(11);
            packet.WriteUInt16(11);

            return packet;
        }
    }

    internal class UserId
    {
        private readonly byte OpCode;

        internal UserId()
        {
            OpCode = OpCodes.UserId;
        }

        internal User User { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt32(User.Id);
            packet.WriteByte((byte)User.Direction);
            packet.WriteByte(213);
            packet.WriteByte((byte)User.Class);
            packet.WriteUInt16(0);

            return packet;
        }
    }

    internal class MapInfo
    {
        private readonly byte OpCode;

        internal MapInfo()
        {
            OpCode = OpCodes.MapInfo;
        }

        internal User User { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt16(User.Map.Id);
            packet.WriteByte((byte)(User.Map.X % 256));
            packet.WriteByte((byte)(User.Map.Y % 256));
            byte flags = 0;
            //if ((User.Map.Flags & MapFlags.Snow) == MapFlags.Snow)
            //    flags |= 1;
            //if ((User.Map.Flags & MapFlags.Rain) == MapFlags.Rain)
            //    flags |= 2;
            //if ((User.Map.Flags & MapFlags.NoMap) == MapFlags.NoMap)
            //    flags |= 64;
            //if ((User.Map.Flags & MapFlags.Winter) == MapFlags.Winter)
            //    flags |= 128;
            packet.WriteByte(flags);
            packet.WriteByte((byte)(User.Map.X / 256));
            packet.WriteByte((byte)(User.Map.Y / 256));
            packet.WriteByte((byte)(User.Map.Checksum % 256));
            packet.WriteByte((byte)(User.Map.Checksum / 256));
            packet.WriteString8(User.Map.Name);

            return packet;
        }
    }

    internal class MapLoadComplete
    {
        private readonly byte OpCode;

        internal MapLoadComplete()
        {
            OpCode = OpCodes.MapLoadComplete;
        }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteUInt16(0);

            return packet;
        }
    }

    internal class MapData
    {
        private readonly byte OpCode;

        internal MapData()
        {
            OpCode = OpCodes.MapData;
        }

        internal MapObject Map { get; set; }

        internal List<ServerPacket> Packets()
        {
            var ret = new List<ServerPacket>();
            var tile = 0;
            for (var row = 0; row < Map.Y; row++)
            {
                var packet = new ServerPacket(OpCode);

                packet.WriteUInt16((ushort)row);
                for (var column = 0; column < Map.X * 6; column += 2)
                {
                    packet.WriteByte(Map.RawData[tile + 1]);
                    packet.WriteByte(Map.RawData[tile]);
                    tile += 2;
                }

                ret.Add(packet);
            }

            return ret;
        }
    }

    internal class LoginMessage
    {
        private readonly byte OpCode;

        internal LoginMessage()
        {
            OpCode = OpCodes.LoginMessage;
        }

        internal byte Type { get; set; }
        internal string Message { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Type);
            packet.WriteString8(Message);

            return packet;
        }
    }

    internal class SystemMessage
    {
        private readonly byte OpCode;

        internal SystemMessage()
        {
            OpCode = OpCodes.SystemMessage;
        }

        internal byte Type { get; set; }
        internal string Message { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Type);
            packet.WriteString16(Message);
            return packet;
        }
    }

    internal class SettingsMessage
    {
        private readonly byte OpCode = OpCodes.SystemMessage;
        internal byte Type = 0x07;
        internal byte Number { get; set; }
        internal string DisplayString { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Type);
            // Unusually, this message length includes the settings number above,
            // and is not just the string length...
            packet.WriteByte(00);
            packet.WriteByte((byte)(DisplayString.Length + 1));
            packet.WriteByte((byte)(Number + 0x30));
            packet.WriteString(DisplayString);
            return packet;
        }
    }

    internal class SpellAnimation
    {
        private readonly byte OpCode;

        internal SpellAnimation()
        {
            OpCode = OpCodes.SpellAnimation;
        }

        internal uint Id { get; set; }
        internal uint SenderId { get; set; }
        internal ushort AnimationId { get; set; }
        internal ushort SenderAnimationId { get; set; }
        internal ushort Speed { get; set; }
        internal ushort X { get; set; }
        internal ushort Y { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(0x00);
            if (Id != 0)
            {
                packet.WriteUInt32(Id);
                packet.WriteUInt32(SenderId == 0 ? Id : SenderId);
                packet.WriteUInt16(AnimationId);
                packet.WriteUInt16(SenderAnimationId == 0 ? ushort.MinValue : SenderAnimationId);
                packet.WriteUInt16(Speed);
                packet.WriteByte(0x00);
            }
            else
            {
                packet.WriteUInt32(uint.MinValue);
                packet.WriteUInt16(AnimationId);
                packet.WriteUInt16(Speed);
                packet.WriteUInt16(X);
                packet.WriteUInt16(Y);
            }

            return packet;
        }
    }

    internal class RemoveSpell
    {
        private readonly byte OpCode;

        internal RemoveSpell()
        {
            OpCode = OpCodes.RemoveSpell;
        }

        internal byte Slot { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Slot);
            packet.WriteByte(0x00);

            return packet;
        }
    }

    internal class RemoveSkill
    {
        private readonly byte OpCode;

        internal RemoveSkill()
        {
            OpCode = OpCodes.RemoveSkill;
        }

        internal byte Slot { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Slot);
            packet.WriteByte(0x00);

            return packet;
        }
    }

    internal class Refresh
    {
        private readonly byte OpCode;

        internal Refresh()
        {
            OpCode = OpCodes.Refresh;
        }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(0x00);

            return packet;
        }
    }

    internal class Manufacture
    {
        private readonly byte OpCode;

        internal Manufacture()
        {
            OpCode = OpCodes.Manufacture;
        }

        public bool IsInitial { get; set; }
        public byte RecipeCount { get; set; }
        public byte Index { get; set; }
        public ushort Sprite { get; set; }
        public string RecipeName { get; set; }
        public string RecipeDescription { get; set; }
        public Dictionary<string, int> RecipeIngredients { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(0x01);
            packet.WriteByte(0x3C);
            if (IsInitial)
            {
                packet.WriteByte(0x00);
                packet.WriteByte(RecipeCount);
                packet.WriteByte(0x00);
            }
            else
            {
                packet.WriteByte(0x01);
                packet.WriteByte(Index);
                packet.WriteUInt16(Sprite);

                packet.WriteString16(RecipeDescription);

                var ing = "Ingredients: \n";
                foreach (var ingredient in RecipeIngredients) ing += $"{ingredient.Value} {ingredient.Key}\n";
                packet.WriteString16(ing);
                packet.WriteByte(0x01);
                packet.WriteByte(0x00);
            }

            return packet;
        }
    }

    internal class ManufactureCursor
    {
        private readonly byte OpCode;

        internal ManufactureCursor()
        {
            OpCode = OpCodes.BlockInput;
        }

        public bool Complete { get; set; }

        internal ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteBoolean(Complete);

            return packet;
        }
    }

    internal class PlayerShop
    {
        private readonly byte OpCode;

        internal PlayerShop()
        {
            OpCode = OpCodes.PlayerShop;
        }

        public uint ShopId { get; set; }
        public uint ShopGold { get; set; }
        public string ShopName { get; set; }
        public bool NameOnly { get; set; }
        public (uint id, ItemObject item, ushort count, uint price)[] ShopItems { get; set; }

        public ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(0x01);
            packet.WriteUInt32(ShopId);
            if (NameOnly)
            {
                packet.WriteByte(0x04);
                packet.WriteString8(ShopName);
            }
            else
            {
                packet.WriteByte(0x00);
                packet.WriteUInt32(ShopGold);
                packet.WriteByte(0x64); // unknown
                packet.WriteByte((byte)ShopItems.Length);
                foreach (var listing in ShopItems)
                {
                    packet.WriteUInt32(listing.id);
                    packet.WriteUInt16(listing.item.Sprite);
                    packet.WriteByte(listing.item.Color);
                    packet.WriteString8(listing.item.Name);
                    packet.WriteUInt32(listing.item.DisplayDurability);
                    packet.WriteUInt16(listing.count);
                    packet.WriteUInt32(listing.price);
                    packet.WriteUInt32(0);
                }
            }

            return packet;
        }
    }

    internal class EditablePaper
    {
        private readonly byte OpCode;

        public EditablePaper()
        {
            OpCode = OpCodes.EditablePaper;
        }

        public PaperType Type { get; set; }
        public byte Width { get; set; }
        public byte Height { get; set; }
        public string Text { get; set; }
        public byte Slot { get; set; }

        public ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte(Slot);
            packet.WriteByte((byte)Type);
            packet.WriteByte(Width);
            packet.WriteByte(Height);
            packet.WriteString16(Text);

            return packet;
        }
    }

    internal class ReadonlyPaper
    {
        private readonly byte OpCode;

        public ReadonlyPaper()
        {
            OpCode = OpCodes.ReadonlyPaper;
        }

        public PaperType Type { get; set; }
        public byte Width { get; set; }
        public byte Height { get; set; }
        public string Text { get; set; }
        public bool Centered { get; set; }

        public ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);
            packet.WriteByte((byte)Type);
            packet.WriteByte(Width);
            packet.WriteByte(Height);
            packet.WriteBoolean(Centered);
            packet.WriteString16(Text);

            return packet;
        }
    }

    internal class MessagingResponse
    {
        private readonly byte OpCode;

        public MessagingResponse()
        {
            OpCode = OpCodes.Board;
            Boards = new List<(ushort Id, string Name)>();
            Messages = new List<MessageInfo>();
            BoardId = 0;
            BoardName = "Mail";
        }

        public BoardResponseType ResponseType { get; set; }
        public List<(ushort Id, string Name)> Boards { get; set; }
        public List<MessageInfo> Messages { get; set; }
        public bool isClick { get; set; }
        public ushort BoardId { get; set; }
        public string BoardName { get; set; }
        public string ResponseString { get; set; }
        public bool ResponseSuccess { get; set; }

        public ServerPacket Packet()
        {
            var packet = new ServerPacket(OpCode);

            if (ResponseType == BoardResponseType.EndResult ||
                ResponseType == BoardResponseType.DeleteMessage ||
                ResponseType == BoardResponseType.HighlightMessage)
            {
                packet.WriteByte((byte)ResponseType);
                packet.WriteBoolean(ResponseSuccess);
                packet.WriteString8(ResponseString);
            }
            else if (ResponseType == BoardResponseType.GetMailboxIndex ||
                     ResponseType == BoardResponseType.GetBoardIndex)
            {
                if (ResponseType == BoardResponseType.GetMailboxIndex)
                {
                    packet.WriteByte(0x04); // 0x02 - public, 0x04 - mail
                    packet.WriteByte(0x01); // ??? - needs to be odd number unless board in world has been clicked
                }
                else
                {
                    packet.WriteByte(0x02);
                    packet.WriteByte((byte)(isClick ? 0x02 : 0x01));
                }

                packet.WriteUInt16(BoardId);
                packet.WriteString8(BoardName);
                packet.WriteByte((byte)Messages.Count);
                foreach (var message in Messages)
                {
                    packet.WriteBoolean(message.Highlight);
                    packet.WriteInt16(message.Id);
                    packet.WriteString8(message.Sender);
                    packet.WriteByte(message.Month);
                    packet.WriteByte(message.Day);
                    packet.WriteString8(message.Subject);
                }
            }

            else if (ResponseType == BoardResponseType.DisplayList)
            {
                packet.WriteByte(0x01);
                packet.WriteUInt16((ushort)(Boards.Count + 1));
                packet.WriteUInt16(0);
                packet.WriteString8("Mail");
                foreach (var (Id, Name) in Boards)
                {
                    packet.WriteUInt16(Id);
                    packet.WriteString8(Name);
                }

                // This is required to correctly display the messaging pane
                packet.TransmitDelay = 600;
            }

            else if (ResponseType == BoardResponseType.GetBoardMessage ||
                     ResponseType == BoardResponseType.GetMailMessage)
            {
                // Functionality unknown but necessary
                var message = Messages[0];
                if (ResponseType == BoardResponseType.GetMailMessage)
                {
                    packet.WriteByte(0x05);
                    packet.WriteByte(0x03);
                    packet.WriteBoolean(true); // Mailbox messages are always "read"
                }
                else
                {
                    packet.WriteByte(0x03);
                    packet.WriteByte(0x00);
                    packet.WriteBoolean(message.Highlight);
                }

                packet.WriteUInt16((ushort)message.Id);
                packet.WriteString8(message.Sender);
                packet.WriteByte(message.Month);
                packet.WriteByte(message.Day);
                packet.WriteString8(message.Subject);
                packet.WriteString16(message.Body);
            }

            return packet;
        }
    }
}