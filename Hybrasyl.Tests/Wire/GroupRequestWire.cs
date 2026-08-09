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
using System.Text;
using DALib.Networking.Packets.Client;
using Hybrasyl.Subsystems.Players.Grouping;
using Hybrasyl.Xml.Objects;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Hand-assembled 0x2E bodies. The oracle for these tests is the rung-1 wire layout written
///     out by hand — never DALib's own writer. A round-trip through <c>Groupbox(...)</c> agrees
///     with itself under either cap ordering, which is precisely how a Rogue/Monk transposition
///     survived two months and a full green suite inside DALib (HTOO-64).
/// </summary>
internal static class GroupRequestBodies
{
    private static void WriteString8(List<byte> body, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        body.Add((byte) bytes.Length);
        body.AddRange(bytes);
    }

    /// <summary>
    ///     Stage-4 (Groupbox) body. Cap order on the wire is Warrior, Wizard, Rogue, Priest, Monk
    ///     — the top-to-bottom row order of the client's recruit dialog. No trailing reserved byte
    ///     on this stage, unlike every simple stage.
    /// </summary>
    public static byte[] Groupbox(
        string leader, string title, string note,
        byte minLevel, byte maxLevel,
        byte warrior, byte wizard, byte rogue, byte priest, byte monk)
    {
        var body = new List<byte> { GroupRequestPacket.StageGroupbox };
        WriteString8(body, leader);
        WriteString8(body, title);
        WriteString8(body, note);
        body.Add(minLevel);
        body.Add(maxLevel);
        body.Add(warrior);
        body.Add(wizard);
        body.Add(rogue);
        body.Add(priest);
        body.Add(monk);
        return body.ToArray();
    }

    /// <summary>Simple-form body (stages 2/3/5/6/7): stage, target name, trailing reserved 0.</summary>
    public static byte[] Simple(byte stage, string name)
    {
        var body = new List<byte> { stage };
        WriteString8(body, name);
        body.Add(0x00);
        return body.ToArray();
    }
}

/// <summary>
///     0x2E GroupRequest wire mapping. Held back during the conversion until HTOO-64
///     shipped, because DALib read the stage-4 class caps with Rogue and Monk transposed and
///     converting sooner would have imported the swap into code that was already correct.
/// </summary>
public class GroupRequestWire
{
    [Fact]
    public void Groupbox_MapsEachCapByteToTheNamedClass()
    {
        // Distinct value per cap, so any two being swapped fails the assertion.
        var parsed = GroupRequestPacket.Parse(GroupRequestBodies.Groupbox(
            "Kedian", "raid", "bring food", 0x0D, 0x25,
            warrior: 1, wizard: 2, rogue: 3, priest: 4, monk: 5));

        Assert.Equal(GroupRequestPacket.StageGroupbox, parsed.Stage);
        Assert.Equal("Kedian", parsed.Leader);
        Assert.Equal("raid", parsed.Title);
        Assert.Equal("bring food", parsed.Note);
        Assert.Equal((byte) 0x0D, parsed.MinLevel);
        Assert.Equal((byte) 0x25, parsed.MaxLevel);

        // The assertion HTOO-64 turned on: wire byte 3 is Rogue and byte 5 is Monk.
        Assert.Equal((byte) 1, parsed.MaxWarrior);
        Assert.Equal((byte) 2, parsed.MaxWizard);
        Assert.Equal((byte) 3, parsed.MaxRogue);
        Assert.Equal((byte) 4, parsed.MaxPriest);
        Assert.Equal((byte) 5, parsed.MaxMonk);
    }

    /// <summary>
    ///     Stage 4 carries the sender's own name in Leader where the simple stages carry the
    ///     target's in Name. The converted handler picks the field by stage; if that inverted,
    ///     opening a recruit box would look up the wrong user.
    /// </summary>
    [Fact]
    public void Groupbox_PutsTheSenderNameInLeaderAndLeavesNameNull()
    {
        var parsed = GroupRequestPacket.Parse(
            GroupRequestBodies.Groupbox("Kedian", "raid", "note", 1, 99, 1, 2, 3, 4, 5));

        Assert.Equal("Kedian", parsed.Leader);
        Assert.Null(parsed.Name);
    }

    [Theory]
    [InlineData(GroupRequestPacket.StageTryInvite)]
    [InlineData(GroupRequestPacket.StageAcceptInvite)]
    [InlineData(GroupRequestPacket.StageRecruitInfo)]
    [InlineData(GroupRequestPacket.StageRemoveGroupBox)]
    [InlineData(GroupRequestPacket.StageRecruitJoin)]
    public void SimpleStages_PutTheTargetNameInNameAndLeaveLeaderNull(byte stage)
    {
        var parsed = GroupRequestPacket.Parse(GroupRequestBodies.Simple(stage, "Trocair"));

        Assert.Equal(stage, parsed.Stage);
        Assert.Equal("Trocair", parsed.Name);
        Assert.Null(parsed.Leader);
    }
}

/// <summary>
///     The server-side half: <see cref="GroupRecruit.FromRequest" /> maps a parsed stage-4 onto
///     the recruit box. This is the mapping the 0x2E conversion introduced, so it is tested
///     separately from DALib's parse — a correct parse feeding a transposed assignment would
///     produce exactly the bug HTOO-64 describes, one layer further in.
/// </summary>
[Collection("Hybrasyl")]
public class GroupRecruitMapping
{
    public GroupRecruitMapping(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    [Fact]
    public void FromRequest_AssignsEachCapToTheNamedClass()
    {
        var parsed = GroupRequestPacket.Parse(GroupRequestBodies.Groupbox(
            "Kedian", "raid", "bring food", 10, 50,
            warrior: 1, wizard: 2, rogue: 3, priest: 4, monk: 5));

        var recruit = GroupRecruit.FromRequest(parsed, Fixture.TestUser);

        Assert.Equal(1, recruit.WarriorsWanted);
        Assert.Equal(2, recruit.WizardsWanted);
        Assert.Equal(3, recruit.RoguesWanted);
        Assert.Equal(4, recruit.PriestsWanted);
        Assert.Equal(5, recruit.MonksWanted);

        // Title becomes the box name; Leader is consumed by the handler, not the box.
        Assert.Equal("raid", recruit.Name);
        Assert.Equal("bring food", recruit.Note);
        Assert.Equal(10, recruit.StartingLevelRange);
        Assert.Equal(50, recruit.EndingLevelRange);
        Assert.Equal(15, recruit.TotalWanted);
    }

    /// <summary>
    ///     <see cref="GroupRecruit.Wanted" /> indexes a wire-ordered array with a
    ///     <see cref="Class" /> enum that orders its members differently (Warrior, Rogue, Wizard,
    ///     Priest, Monk), which is why it special-cases Rogue and Wizard. Pinned because a
    ///     "simplification" that dropped those two arms would silently swap them.
    /// </summary>
    [Fact]
    public void Wanted_ByClassEnum_AgreesWithTheNamedProperties()
    {
        var parsed = GroupRequestPacket.Parse(GroupRequestBodies.Groupbox(
            "Kedian", "raid", "note", 1, 99,
            warrior: 1, wizard: 2, rogue: 3, priest: 4, monk: 5));

        var recruit = GroupRecruit.FromRequest(parsed, Fixture.TestUser);

        Assert.Equal(1, recruit.Wanted(Class.Warrior));
        Assert.Equal(2, recruit.Wanted(Class.Wizard));
        Assert.Equal(3, recruit.Wanted(Class.Rogue));
        Assert.Equal(4, recruit.Wanted(Class.Priest));
        Assert.Equal(5, recruit.Wanted(Class.Monk));
        Assert.Equal(0, recruit.Wanted(Class.Peasant));
    }

    /// <summary>
    ///     The clamps the legacy positional reader applied, preserved verbatim: caps cap at 13,
    ///     the starting level floors at 1, the ending level is clamped into 1..99. A client is
    ///     free to send anything here.
    /// </summary>
    [Fact]
    public void FromRequest_ClampsCapsAndLevelsLikeTheLegacyReader()
    {
        var parsed = GroupRequestPacket.Parse(GroupRequestBodies.Groupbox(
            "Kedian", "raid", "note", minLevel: 0, maxLevel: 200,
            warrior: 255, wizard: 13, rogue: 14, priest: 0, monk: 12));

        var recruit = GroupRecruit.FromRequest(parsed, Fixture.TestUser);

        Assert.Equal(13, recruit.WarriorsWanted);
        Assert.Equal(13, recruit.WizardsWanted);
        Assert.Equal(13, recruit.RoguesWanted);
        Assert.Equal(0, recruit.PriestsWanted);
        Assert.Equal(12, recruit.MonksWanted);
        Assert.Equal(1, recruit.StartingLevelRange);
        Assert.Equal(99, recruit.EndingLevelRange);
    }
}
