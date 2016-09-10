using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Enums;

namespace Hybrasyl
{
    //This is a POC. Nothing to see here folks.

    internal class ServerPacketStructures
    {
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
}
}
