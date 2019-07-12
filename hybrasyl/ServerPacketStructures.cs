using System;
using System.Collections.Generic;
using Hybrasyl.Enums;
using Hybrasyl.Maps;
using Hybrasyl.Objects;
using log4net;

namespace Hybrasyl
{

    internal interface IPacket
    {
        byte OpCode { get; }
        ServerPacket ToPacket();
    }

    //This is a POC. Nothing to see here folks.

    internal class ServerPacketStructures
    {

        public static readonly ILog Logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        internal partial class UseSkill
        {
            private byte OpCode;

            internal UseSkill()
            {
                OpCode = OpCodes.UseSkill;
            }

            internal byte Slot { get; set; }
        }

        internal partial class StatusBar
        {
            private static byte OpCode = OpCodes.StatusBar;

            internal ushort Icon;
            internal StatusBarColor BarColor;

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteUInt16(Icon);
                packet.WriteByte((byte) BarColor);
                return packet;
            }
        }


        internal partial class Cooldown
        {
            private static byte OpCode = OpCodes.Cooldown;

            internal byte Pane;
            internal byte Slot;
            internal uint Length;

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteByte(Pane);
                packet.WriteByte(Slot);
                packet.WriteUInt32(Length);

                return packet;
            }

        }

        internal partial class PlayerAnimation
        {
            private byte OpCode;

            internal PlayerAnimation()
            {
                OpCode = OpCodes.PlayerAnimation;
            }

            internal uint UserId { get; set; }
            internal ushort Speed { get; set; }
            internal byte Animation { get; set; }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteUInt32(UserId);
                packet.WriteByte(Animation);
                packet.WriteUInt16(Speed);

                return packet;
            }

        }

        internal partial class Exchange
        {
            private static byte OpCode =
                OpCodes.Exchange;

            internal byte Action;
            internal uint Gold;
            internal bool Side;
            internal string RequestorName;
            internal uint RequestorId;
            internal byte ItemSlot;
            internal ushort ItemSprite;
            internal byte ItemColor;
            internal string ItemName;
            internal const string CancelMessage = "Exchange was cancelled.";
            internal const string ConfirmMessage = "You exchanged.";

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
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
                        packet.WriteByte((byte) (Side ? 0 : 1));
                        packet.WriteByte(ItemSlot);
                        packet.WriteUInt16((ushort) (0x8000 + ItemSprite));
                        packet.WriteByte(ItemColor);
                        packet.WriteString8(ItemName);
                    }
                        break;
                    case ExchangeActions.GoldUpdate:
                    {
                        packet.WriteByte((byte) (Side ? 0 : 1));
                        packet.WriteUInt32(Gold);
                    }
                        break;
                    case ExchangeActions.Cancel:
                    {
                        packet.WriteByte((byte) (Side ? 0 : 1));
                        packet.WriteString8(CancelMessage);

                    }
                        break;
                    case ExchangeActions.Confirm:
                    {
                        packet.WriteByte((byte) (Side ? 0 : 1));
                        packet.WriteString8(ConfirmMessage);
                    }
                        break;
                }
                return packet;
            }
        }

        internal partial class CastLine
        {
            private byte OpCode;

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

        internal partial class PlaySound
        {
            private byte OpCode;

            internal PlaySound()
            {
                OpCode = OpCodes.PlaySound;
            }

            internal byte Sound { get; set; }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteByte(Sound);
                return packet;
            }
        }

        internal partial class HealthBar
        {
            private byte OpCode;

            internal HealthBar()
            {
                OpCode = OpCodes.HealthBar;
            }

            internal uint ObjId { get; set; }

            internal byte CurrentPercent { get; set; }
            internal byte? Sound { get; set; }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteUInt32(ObjId);
                packet.WriteByte(0);
                packet.WriteByte(CurrentPercent);
                packet.WriteByte(Sound ?? 0xFF);

                return packet;
            }

        }

        internal partial class EffectAnimation
        {
            private byte OpCode;

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
                ServerPacket packet = new ServerPacket(OpCode);
                int position = packet.Position;
                packet.WriteUInt32(TargetId);
                packet.WriteUInt32(SourceId ?? 0);
                packet.WriteUInt16((ushort) TargetAnimation);
                packet.WriteUInt16((ushort) (SourceAnimation ?? 0));
                packet.WriteInt16(Speed);
                packet.WriteInt32(0);
                return packet;
            }
        }

        internal partial class DisplayUser
        {
            private byte OpCode;

            internal DisplayUser()
            {
                OpCode = OpCodes.DisplayUser;
            }

            #region Location information

            internal byte X { get; set; }
            internal byte Y { get; set; }
            internal Direction Direction { get; set; }
            internal uint Id { get; set; }

            #endregion

            #region Appearance

            internal string Name { get; set; }
            internal Sex Sex { get; set; }
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
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteUInt16(X);
                packet.WriteUInt16(Y);
                packet.WriteByte((byte) Direction);
                packet.WriteUInt32(Id);
                packet.WriteUInt16(Helmet);
                Logger.InfoFormat($"Sex is {Sex}");
                if (!DisplayAsMonster)
                {
                    packet.WriteByte((byte) (((byte) Sex*16) + BodySpriteOffset));
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
                    packet.WriteByte((byte) LanternSize);
                    packet.WriteByte((byte) RestPosition);
                    packet.WriteUInt16(Overcoat);
                    packet.WriteByte(OvercoatColor);
                    packet.WriteByte((byte) SkinColor);
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
                packet.WriteByte((byte) NameStyle);
                packet.WriteString8(Name ?? string.Empty);
                packet.WriteString8(GroupName ?? string.Empty);

                return packet;
            }


        }

        internal partial class MerchantResponse
        {
            private byte OpCode;
            internal MerchantDialogType MerchantDialogType { get; set; }
            internal MerchantDialogObjectType MerchantDialogObjectType { get; set; }
            internal uint ObjectId { get; set; }
            private byte Unknow4 = 1;
            internal ushort Tile1 { get; set; }
            internal byte Color1 { get; set; } //affect items only
            internal byte Unknow7 = 1;
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
            internal MerchantOptionsWithArgument OptionsWithArgument { get;set;}
            internal MerchantInput Input { get; set; }
            internal MerchantInputWithArgument InputWithArgument { get; set; }
            internal UserInventoryItems UserInventoryItems { get; set; }
            internal MerchantShopItems ShopItems { get; set; }
            internal MerchantSpells Spells { get; set; }
            internal MerchantSkills Skills { get; set; }
            internal UserSkillBook UserSkills { get; set; }
            internal UserSpellBook UserSpells { get; set; }


            internal MerchantResponse()
            {
                OpCode = OpCodes.NpcReply;
            }

            internal ServerPacket Packet()
            {
                var packet = new ServerPacket(OpCode);
                packet.WriteByte((byte)MerchantDialogType);
                packet.WriteByte((byte)MerchantDialogObjectType);
                packet.WriteUInt32(ObjectId);
                packet.WriteByte(Unknow4);
                packet.WriteUInt16(Tile1);
                packet.WriteByte(Color1);
                packet.WriteByte(Unknow7);
                packet.WriteUInt16(Tile2);
                packet.WriteByte(Color2);
                packet.WriteByte(PortraitType);
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
                if (MerchantDialogType == MerchantDialogType.Input)
                {
                    packet.WriteUInt16(Input.Id);
                }
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
                        packet.WriteByte(skill.Icon);
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
                if (MerchantDialogType == MerchantDialogType.UserSkillBook)
                {
                    packet.WriteUInt16(UserSkills.Id);
                }
                if (MerchantDialogType == MerchantDialogType.UserSpellBook)
                {
                    packet.WriteUInt16(UserSpells.Id);
                }
                if (MerchantDialogType == MerchantDialogType.UserInventoryItems)
                {
                    packet.WriteUInt16(UserInventoryItems.Id);
                    packet.WriteByte(UserInventoryItems.InventorySlotsCount);
                    foreach (var slot in UserInventoryItems.InventorySlots)
                    {
                        packet.WriteByte(slot);
                    }
                }

                return packet;
            }
        }

        internal partial class Turn
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
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteUInt32(Id);
                packet.WriteByte(Direction);
                return packet;
            }
        }

        internal partial class PlayerProfile
        {
            private readonly byte OpCode;

            internal PlayerProfile()
            {
                OpCode = OpCodes.SelfProfile;
            }
            internal User Player { get; set; }
            internal byte NationFlag { get; set; }
            internal string GuildRank { get; set; }
            internal byte TitleCount { get; set; }
            internal List<byte> TitleIds { get; set; }
            internal byte CurrentTitle { get; set; }
            internal UserGroup Group { get; set; }
            internal bool IsGrouped { get; set; }
            internal byte CanGroup { get; set; }
            internal byte Class { get; set; }
            internal string ClassName { get; set; }
            internal ushort PlayerDisplay { get; set; }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteByte(NationFlag);
                packet.WriteString8(GuildRank);
                packet.WriteByte(TitleCount);
                foreach (var id in TitleIds)
                {
                    packet.WriteByte(id);
                }
                packet.WriteByte(CurrentTitle);
                if (!IsGrouped) packet.WriteString8("No Group");
                else
                {
                    var ret = "Group\n";
                    foreach (var member in Group.Members)
                    {
                        ret += $"{member.Name}\n";
                    }
                    packet.WriteString8(ret);
                }
                packet.WriteByte(CanGroup);
                packet.WriteByte(0x00);
                packet.WriteByte(Class);
                packet.WriteByte(0x01);
                packet.WriteByte(0x00);
                packet.WriteString8(Player.IsMaster ? "Master" : Player.Class.ToString());
                packet.WriteString8(Player.Guild != null ? Player.Guild.Name : string.Empty);
                packet.WriteByte((byte)(Player.Legend.Count > 255 ? 255 : Player.Legend.Count));
                foreach (var mark in Player.Legend)
                {
                    packet.WriteByte((byte)mark.Icon);
                    packet.WriteByte((byte)mark.Color);
                    packet.WriteString8(mark.Prefix);
                    packet.WriteString8(mark.Text);
                }
                packet.WriteByte(0x00);
                packet.WriteUInt16(PlayerDisplay);
                packet.WriteByte(0x02);
                packet.WriteUInt32(0x00);
                packet.WriteByte(0x00);
                return packet;
            }
        }

        internal partial class RemoveWorldObject
        {
            private readonly byte OpCode;

            internal uint Id { get; set; }

            internal RemoveWorldObject()
            {
                OpCode = OpCodes.RemoveWorldObject;
            }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteUInt32(Id);

                return packet;
            }
        }

        internal partial class Location
        {
            private readonly byte OpCode;

            internal ushort X { get; set; }
            internal ushort Y { get; set; }


            internal Location()
            {
                OpCode = OpCodes.Location;
            }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteUInt16(X);
                packet.WriteUInt16(Y);
                packet.WriteUInt16(11);
                packet.WriteUInt16(11);

                return packet;
            }
        }
        internal partial class UserId
        {
            private readonly byte OpCode;

            internal User User { get; set; }

            internal UserId()
            {
                OpCode = OpCodes.UserId;
            }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteUInt32(User.Id);
                packet.WriteByte(0x01);
                packet.WriteByte(213);
                packet.WriteByte((byte)User.Class);
                packet.WriteUInt16(0);

                return packet;
            }
        }

        internal partial class MapInfo
        {
            private readonly byte OpCode;

            internal User User { get; set; }
            internal MapInfo()
            {
                OpCode = OpCodes.MapInfo;
            }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
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

        internal partial class MapData
        {
            private readonly byte OpCode;

            internal Map Map { get; set; }

            internal MapData()
            {
                OpCode = OpCodes.MapData;
            }

            internal List<ServerPacket> Packets()
            {
                var ret = new List<ServerPacket>();
                var tile = 0;
                for (var row = 0; row < Map.Y; row++)
                {
                    ServerPacket packet = new ServerPacket(OpCode);

                    packet.WriteUInt16((ushort) row);
                    for (int column = 0; column < Map.X * 6; column += 2)
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

        internal partial class LoginMessage
        {
            private readonly byte OpCode;

            internal byte Type { get; set; }
            internal string Message { get; set; }

            internal LoginMessage()
            {
                OpCode = OpCodes.LoginMessage;
            }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteByte(Type);
                packet.WriteString8(Message);

                return packet;
            }
        }

        internal partial class SystemMessage
        {
            private readonly byte OpCode;

            internal byte Type { get; set; }
            internal string Message { get; set; }

            internal SystemMessage()
            {
                OpCode = OpCodes.SystemMessage;
            }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteByte(Type);
                packet.WriteString16(Message);
                return packet;
            }
        }

        internal partial class SpellAnimation
        {
            private readonly byte OpCode;

            internal uint Id { get; set; }
            internal uint SenderId { get; set; }
            internal ushort AnimationId { get; set; }
            internal ushort SenderAnimationId { get; set; }
            internal ushort Speed { get; set; }
            internal ushort X { get; set; }
            internal ushort Y { get; set; }

            internal SpellAnimation()
            {
                OpCode = OpCodes.SpellAnimation;
            }

            internal ServerPacket Packet()
            {


                ServerPacket packet = new ServerPacket(OpCode);
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

        internal partial class RemoveSpell
        {
            private readonly byte OpCode;

            internal byte Slot { get; set; }
            internal RemoveSpell()
            {
                OpCode = OpCodes.RemoveSpell;
            }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteByte(Slot);
                packet.WriteByte(0x00);

                return packet;
            }
        }

        internal partial class RemoveSkill  
        {
            private readonly byte OpCode;

            internal byte Slot { get; set; }
            internal RemoveSkill()
            {
                OpCode = OpCodes.RemoveSkill;
            }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteByte(Slot);
                packet.WriteByte(0x00);

                return packet;
            }
        }

        internal partial class Refresh
        {
            private readonly byte OpCode;

            internal Refresh()
            {
                OpCode = OpCodes.Refresh;
            }

            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteByte(0x00);

                return packet;
            }
        }
    }

}
