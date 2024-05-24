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
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Players.Grouping;

namespace Hybrasyl.Networking.ServerPackets;

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
        packet.WriteByte((byte) (Player.Legend.Count > 255 ? 255 : Player.Legend.Count));
        foreach (var mark in Player.Legend)
        {
            packet.WriteByte((byte) mark.Icon);
            packet.WriteByte((byte) mark.Color);
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