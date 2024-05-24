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

using Hybrasyl.Internals.Enums;

namespace Hybrasyl.Networking.ServerPackets;

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