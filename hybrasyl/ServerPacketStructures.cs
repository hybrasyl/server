using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Enums;

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
                Console.WriteLine(String.Format("uid: {0}, Animation: {1}, speed {2}", UserId, Animation, Speed));
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
                Console.WriteLine(String.Format("sound: {0}", Sound));
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
                packet.WriteUInt16((ushort)TargetAnimation);
                packet.WriteUInt16((ushort)(SourceAnimation ?? 0));
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
            internal byte Direction { get; set; }
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

            // 0x33 <X> <Y> <Direction> <Player ID> <hat/hairstyle> <Offset for sex/status (includes dead/etc)>
            // <armor sprite> <boots> <armor sprite> <shield> <weapon> <hair color> <boot color> <acc1 color> <acc1>
            // <acc2 color> <acc2> <acc3 color> <acc3> <nfi> <nfi> <overcoat> <overcoat color> <skin color> <transparency>
            // <face> <name style (see Enums.NameDisplayStyles)> <name length> <name> <group name length> <group name> (shows up as hovering clickable bar)
            internal ServerPacket Packet()
            {
                ServerPacket packet = new ServerPacket(OpCode);
                packet.WriteUInt16(X);
                packet.WriteByte(Y);
                packet.WriteByte(Direction);
                packet.WriteUInt32(Id);
                packet.WriteUInt16(Helmet);
                if (DisplayAsMonster)
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
                    packet.WriteUInt16(SecondAcc););
                    packet.WriteByte(ThirdAccColor);
                    packet.WriteUInt16(ThirdAcc);
                    packet.WriteByte((byte)LanternSize);
                    packet.WriteByte((byte)RestPosition);
                    packet.WriteUInt16(Overcoat);
                    packet.WriteUInt16(OvercoatColor);
                    packet.WriteByte((byte)SkinColor);
                    packet.WriteBoolean(Invisible);
                    packet.WriteByte(FaceShape);
                }
                else
                {
                    packet.WriteUInt16(MonsterSprite);
                    packet.WriteUInt16(HairColor);
                    packet.WriteUInt16(BootsColor);
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


        }
    }

}
