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
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System.Collections.Generic;
using System.Linq;
using DALib.Networking.Packets.Server;
using DALib.Networking.Wire;
using Xunit;
using LegacyServerPacket = Hybrasyl.Tests.Wire.LegacyBodyWriter;
// The test project already has its own ProfilePacket / LegendMark; alias the DALib records.
using DalibProfilePacket = DALib.Networking.Packets.Server.ProfilePacket;
using DalibLegendMark = DALib.Networking.Packets.Server.LegendMark;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Social, profile and board packet-compatibility coverage. MATCH opcodes are pinned
///     byte-identical against the verbatim pre-conversion emit; the 0x63 Ask trailing NULs and
///     the 0x39 trailing 9 bytes assert the typed body equals the legacy emit minus exactly the
///     dropped slack; the 0x31 board list asserts the retail-true layout produces the same bytes
///     the legacy coincidence produced.
/// </summary>
/// <remarks>
///     <strong>These are compatibility tests, not send-path coverage.</strong> Nothing here
///     invokes a production send path: each case constructs a DALib record, writes its body and
///     compares it against a hand-reconstructed copy of the encoder the conversion deleted. That
///     is legitimate migration evidence — it catches a record whose bytes drifted from what
///     Hybrasyl always sent — but it says nothing about whether anything calls it. <strong>No test
///     covers these particular call sites.</strong> <see cref="ReceiveWiring" />,
///     <see cref="MerchantDispatchWiring" /> and <see cref="CryptoPipeline" /> cover inbound
///     dispatch and the generic outbound pipeline respectively; none of them reaches the individual
///     record-construction sites these cases exercise. Named <c>*SendPath</c> until 2026-08-06,
///     which read as integration coverage it never had.
/// </remarks>
public class SocialAndBoardsPacketCompatibility
{
    private static byte[] Body(DALib.Networking.Wire.ServerPacket record)
    {
        var writer = new PacketWriter();
        record.WriteBody(writer);
        return writer.WrittenSpan.ToArray();
    }

    // --- MATCH: byte-identical conversions ---

    [Fact]
    public void RemoveEquipment_MatchesLegacyBody()
    {
        // 0x38: Slot only.
        var legacy = new LegacyServerPacket(0x38);
        legacy.WriteByte(3);

        var typed = Body(new RemoveEquipmentPacket { Slot = (DALib.Enums.EquipmentSlot)3 });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void RequestPortrait_MatchesLegacyBody()
    {
        // 0x49: two inert zero bytes; DALib's default Padding is the same pair.
        var legacy = new LegacyServerPacket(0x49);
        legacy.WriteByte(0x00);
        legacy.WriteByte(0x00);

        var typed = Body(new RequestPortraitPacket());

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void UserList_MatchesLegacyBody()
    {
        // 0x36: [u16 totalAllShards][u16 count] then per row
        // [u8 Class][u8 Color][u8 SocialStatus][string8 Title][bool IsMaster][string8 Name].
        // Hybrasyl is single-shard, so the legacy site wrote the row count twice; leaving
        // TotalUserCount null makes DALib mirror it the same way.
        var legacy = new LegacyServerPacket(0x36);
        legacy.WriteUInt16(2);
        legacy.WriteUInt16(2);
        legacy.WriteByte(1);
        legacy.WriteByte(84); // guild-mate relationship color
        legacy.WriteByte(4);  // Grouped
        legacy.WriteString8("Champion");
        legacy.WriteBoolean(true);
        legacy.WriteString8("Kedian");
        legacy.WriteByte(2);
        legacy.WriteByte(255); // "other" relationship color
        legacy.WriteByte(0);   // Awake
        legacy.WriteString8("");
        legacy.WriteBoolean(false);
        legacy.WriteString8("Brigid");

        var typed = Body(new UserListPacket
        {
            Users =
            [
                new UserListEntry(1, 84, SocialStatus.Grouped, "Champion", true, "Kedian"),
                new UserListEntry(2, 255, SocialStatus.Awake, "", false, "Brigid")
            ]
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void GroupRecruitInfo_MatchesLegacyBody()
    {
        // 0x63 type 4: [04][string8 leader][string8 group][string8 note][u8 min][u8 max]
        // then five (wanted, current) pairs in Warrior, Wizard, Rogue, Priest, Monk order.
        var legacy = new LegacyServerPacket(0x63);
        legacy.WriteByte(0x04);
        legacy.WriteString8("Kedian");
        legacy.WriteString8("Dubhaim Delvers");
        legacy.WriteString8("Bring torches");
        legacy.WriteByte(11);
        legacy.WriteByte(50);
        legacy.WriteByte(2); legacy.WriteByte(1); // warriors
        legacy.WriteByte(1); legacy.WriteByte(0); // wizards
        legacy.WriteByte(3); legacy.WriteByte(2); // rogues
        legacy.WriteByte(1); legacy.WriteByte(1); // priests
        legacy.WriteByte(0); legacy.WriteByte(0); // monks

        var typed = Body(new GroupRecruitInfoPacket
        {
            ResponseType = GroupResponseType.RecruitInfo,
            Info = new GroupRecruitInfo
            {
                RecruiterName = "Kedian",
                GroupName = "Dubhaim Delvers",
                Note = "Bring torches",
                StartingLevel = 11,
                EndingLevel = 50,
                WarriorsWanted = 2, CurrentWarriors = 1,
                WizardsWanted = 1, CurrentWizards = 0,
                RoguesWanted = 3, CurrentRogues = 2,
                PriestsWanted = 1, CurrentPriests = 1,
                MonksWanted = 0, CurrentMonks = 0
            }
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void Profile_MatchesLegacyBody()
    {
        // 0x34: id, 18x (u16 sprite, u8 color) in profile *display* order, status, name, nation,
        // title, group-open, guild rank, class name, guild, legend, then the portrait/text tail
        // whose length is portrait + text + 4.
        const string author = "Deoch 5";
        var legacy = new LegacyServerPacket(0x34);
        legacy.WriteUInt32(0xCAFEBABE);
        for (var i = 0; i < 18; i++)
        {
            legacy.WriteUInt16((ushort)(0x8000 + i));
            legacy.WriteByte((byte)i);
        }

        legacy.WriteByte(3); // NeedGroup
        legacy.WriteString8("Kedian");
        legacy.WriteByte(2);
        legacy.WriteString8("");
        legacy.WriteByte(1); // grouping
        legacy.WriteString8("Sentinel");
        legacy.WriteString8("Rogue");
        legacy.WriteString8("Hy-brasyl");
        legacy.WriteByte(1);
        legacy.WriteByte(4);
        legacy.WriteByte(2);
        legacy.WriteString8(author);
        legacy.WriteString8("Reached rank 99");
        legacy.WriteUInt16(4); // portrait(0) + text(0) + 4
        legacy.WriteUInt16(0);
        legacy.WriteString16("");

        var equipment = DalibProfilePacket.EquipmentDisplayOrder
            .Select((slot, i) => new ProfileEquipmentSlot(slot, (ushort)(0x8000 + i), (byte)i))
            .ToList();

        var typed = Body(new DalibProfilePacket
        {
            Id = 0xCAFEBABE,
            Equipment = equipment,
            SocialStatus = SocialStatus.NeedGroup,
            Name = "Kedian",
            NationFlag = 2,
            Title = "",
            GroupOpen = true,
            GuildRank = "Sentinel",
            ClassName = "Rogue",
            GuildName = "Hy-brasyl",
            Legend = [new DalibLegendMark { Icon = 4, Color = 2, Prefix = author, Text = "Reached rank 99" }]
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    // --- Slack family: typed body == legacy minus exactly the dropped bytes ---

    [Fact]
    public void GroupAsk_DropsTrailingNuls()
    {
        // Legacy wrote [01][string8 name][00][00]; rung-1 (darkages-741 099-0x63) says the
        // client reads the name and stops.
        var legacy = new LegacyServerPacket(0x63);
        legacy.WriteByte(0x01);
        legacy.WriteString8("Kedian");
        legacy.WriteByte(0);
        legacy.WriteByte(0);

        var typed = Body(new GroupPromptPacket
        {
            ResponseType = GroupResponseType.Ask,
            SourceName = "Kedian"
        });

        var legacyBytes = legacy.BodyMemory.ToArray();
        Assert.Equal(legacyBytes.Length - 2, typed.Length);
        Assert.Equal(legacyBytes[..^2], typed);
    }

    [Fact]
    public void SelfProfile_DropsTrailingSlack()
    {
        // Legacy appended 9 bytes after the legend loop (0x00, u16 body style, 0x02,
        // u32 0, 0x00); rung-1 (darkages-741 057-0x39) ends the body at the legend loop.
        var legacy = new LegacyServerPacket(0x39);
        legacy.WriteByte(2);
        legacy.WriteString8("Sentinel");
        legacy.WriteString8("the Bold");
        legacy.WriteString8(SelfProfilePacket.GroupStatusSolo);
        legacy.WriteBoolean(true);
        legacy.WriteBoolean(false); // no recruit
        legacy.WriteByte(3);
        legacy.WriteByte(0x00);
        legacy.WriteByte(0x00);
        legacy.WriteString8("Rogue");
        legacy.WriteString8("Hy-brasyl");
        legacy.WriteByte(1);
        legacy.WriteByte(4);
        legacy.WriteByte(2);
        legacy.WriteString8("Deoch 5");
        legacy.WriteString8("Reached rank 99");
        // the slack
        legacy.WriteByte(0x00);
        legacy.WriteUInt16(0x1234);
        legacy.WriteByte(0x02);
        legacy.WriteUInt32(0x00);
        legacy.WriteByte(0x00);

        var typed = Body(new SelfProfilePacket
        {
            NationFlag = 2,
            GuildRank = "Sentinel",
            CurrentTitle = "the Bold",
            GroupStatusText = SelfProfilePacket.GroupStatusSolo,
            CanGroup = true,
            Recruit = null,
            Class = 3,
            ClassName = "Rogue",
            GuildName = "Hy-brasyl",
            Legend = [new DalibLegendMark { Icon = 4, Color = 2, Prefix = "Deoch 5", Text = "Reached rank 99" }]
        });

        var legacyBytes = legacy.BodyMemory.ToArray();
        Assert.Equal(legacyBytes.Length - 9, typed.Length);
        Assert.Equal(legacyBytes[..^9], typed);
    }

    [Fact]
    public void SelfProfile_GroupRosterMatchesLegacyText()
    {
        // The roster text format is load-bearing for the client's group pane; the legacy site built
        // it inline. Pin DALib's helper against that exact string.
        var expected = "Group members\n* Kedian\n  Brigid\nTotal 2";

        Assert.Equal(expected,
            SelfProfilePacket.FormatGroupRoster("Kedian", new[] { "Kedian", "Brigid" }));
    }

    // --- 0x31 board list, retail-true layout, same bytes ---

    [Fact]
    public void BoardList_RetailLayoutMatchesLegacyBytes()
    {
        // Legacy wrote [01][u16 count+1][u16 0][string8 "Mail"] then the boards. The client parses
        // that as [01][string8 heading][u8 count][entries] — the u16's zero high byte doubling as an
        // empty heading length. Below is the legacy byte sequence; the typed record must match it
        // while actually meaning what the client reads.
        var legacy = new LegacyServerPacket(0x31);
        legacy.WriteByte(0x01);
        legacy.WriteUInt16(3); // 2 boards + 1
        legacy.WriteUInt16(0);
        legacy.WriteString8("Mail");
        legacy.WriteUInt16(7);
        legacy.WriteString8("Rucesion");
        legacy.WriteUInt16(9);
        legacy.WriteString8("Mileth");

        var typed = Body(new BoardListPacket
        {
            ResponseType = BoardResponseType.BoardList,
            Name = string.Empty,
            Boards =
            [
                new BoardListEntry(0, "Mail"),
                new BoardListEntry(7, "Rucesion"),
                new BoardListEntry(9, "Mileth")
            ]
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void BoardIndex_MatchesLegacyBody()
    {
        // 0x31 type 2 (public board index). The legacy enum value for GetBoardIndex was 3, but the
        // builder wrote literal 0x02 — the wire type, which is what the typed record carries.
        var legacy = new LegacyServerPacket(0x31);
        legacy.WriteByte(0x02);
        legacy.WriteByte(0x01); // not a click
        legacy.WriteUInt16(7);
        legacy.WriteString8("Rucesion");
        legacy.WriteByte(1);
        legacy.WriteBoolean(true);
        legacy.WriteInt16(42);
        legacy.WriteString8("Kedian");
        legacy.WriteByte(6);
        legacy.WriteByte(14);
        legacy.WriteString8("Lost ring");

        var typed = Body(new BoardIndexPacket
        {
            ResponseType = BoardResponseType.PublicBoard,
            RefreshFlag = 0x01,
            BoardId = 7,
            BoardName = "Rucesion",
            Messages = [new BoardMessageHeader(true, 42, "Kedian", 6, 14, "Lost ring")]
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void MailPost_MatchesLegacyBody()
    {
        // 0x31 type 5 (mail). Legacy wrote 0x05, 0x03, then a hardcoded "read" true.
        var legacy = new LegacyServerPacket(0x31);
        legacy.WriteByte(0x05);
        legacy.WriteByte(0x03);
        legacy.WriteBoolean(true);
        legacy.WriteUInt16(42);
        legacy.WriteString8("Brigid");
        legacy.WriteByte(6);
        legacy.WriteByte(14);
        legacy.WriteString8("Re: Lost ring");
        legacy.WriteString16("Found it.");

        var typed = Body(new BoardPostPacket
        {
            ResponseType = BoardResponseType.PrivatePost,
            RefreshFlag = 0x03,
            Highlight = true,
            PostId = 42,
            Author = "Brigid",
            Month = 6,
            Day = 14,
            Subject = "Re: Lost ring",
            Body = "Found it."
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void BoardResult_MatchesLegacyBody()
    {
        // 0x31 types 6/7/8: [type][bool success][string8 message]. These three enum values did
        // match the wire in the legacy builder.
        var legacy = new LegacyServerPacket(0x31);
        legacy.WriteByte(0x07);
        legacy.WriteBoolean(false);
        legacy.WriteString8("Access denied.");

        var typed = Body(new BoardResultPacket
        {
            ResponseType = BoardResponseType.DeleteResult,
            Success = false,
            Message = "Access denied."
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }
}
