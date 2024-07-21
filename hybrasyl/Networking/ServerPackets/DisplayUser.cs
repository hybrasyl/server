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
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Networking.ServerPackets;

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
        packet.WriteByte((byte) Direction);
        packet.WriteUInt32(Id);
        packet.WriteUInt16(Helmet);

        if (!DisplayAsMonster)
        {
            packet.WriteByte((byte) ((byte) Gender * 16 + BodySpriteOffset));
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