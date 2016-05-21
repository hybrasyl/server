using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Enums;

namespace Hybrasyl
{
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
    }
}
