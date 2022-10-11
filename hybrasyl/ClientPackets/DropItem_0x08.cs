using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using App.Metrics.Timer;
using Grpc.Core;
using iTextSharp.xmp.impl;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Org.BouncyCastle.Asn1.Mozilla;

namespace Hybrasyl.ClientPackets
{
    // These classes are used for unit tests that need to simulate client packet interaction, and are
    // a stripped down reimplementation of the original Packet/ClientPacket/etc classes.

    public abstract class PacketBase
    {
        public virtual byte Opcode { get; set; }
        public byte Ordinal { get; set; } = 0xFF;
        public bool ShouldEncrypt => Opcode != 0x00 && Opcode != 0x10;

        public byte Header => 0xAA;
        public byte[] Footer => new byte[] {0x68, 0x79, 0x62};
        public MemoryStream Data = new MemoryStream();

        public static explicit operator ClientPacket(PacketBase pb)
        {
            var stream = new MemoryStream();
            var shouldEncrypt = pb.ShouldEncrypt ? 5 : 4;
            var totalLength = pb.Data.Length + shouldEncrypt;
            stream.WriteByte(pb.Header);
            stream.WriteByte((byte)(totalLength - 3 / 256));
            stream.WriteByte((byte)(totalLength - 3));
            stream.WriteByte(pb.Opcode);
            if (pb.ShouldEncrypt)
                stream.WriteByte(pb.Ordinal);
            stream.Write(pb.Data.ToArray());
            stream.Write(pb.Footer);

            return new ClientPacket(stream.ToArray());
        }

        public void WriteByte(byte value) => Data.WriteByte(value);

        public void WriteInt16(short value)
        {
            Data.WriteByte((byte)(value >> 8));
            Data.WriteByte((byte) value);
        }

        public void WriteUInt16(ushort value)
        {
            Data.WriteByte((byte)(value >> 8));
            Data.WriteByte((byte)value);
        }

        public void WriteInt32(int value)
        {
            Data.WriteByte((byte)(value >> 24));
            Data.WriteByte((byte)(value >> 16));
            Data.WriteByte((byte)(value >> 8));
            Data.WriteByte((byte)value);
        }

        public void WriteUInt32(uint value)
        {
            Data.WriteByte((byte)(value >> 24));
            Data.WriteByte((byte)(value >> 16));
            Data.WriteByte((byte)(value >> 8));
            Data.WriteByte((byte)value);
        }
    }

    public class DropItem : PacketBase
    {
        public override byte Opcode => 0x08;

        public DropItem(byte slot, byte x, byte y, uint count)
        {
            WriteByte(slot);
            WriteInt16(x);
            WriteInt16(y);
            WriteUInt32(count);
        }
    }
}
