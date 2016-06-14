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
