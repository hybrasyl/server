using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Internals;

[AttributeUsage(AttributeTargets.Method)]
public class PacketHandler : Attribute
{
    public byte Opcode { get; set; }

    public PacketHandler(byte opcode)
    {
        Opcode = opcode;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class HybrasylMessageHandler : Attribute
{
    public ControlOpcode Opcode { get; set; }

    public HybrasylMessageHandler(ControlOpcode opcode)
    {
        Opcode = opcode;
    }
}

