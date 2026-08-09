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

using System.Reflection;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class ProfilePacket(HybrasylFixture fixture)
{
    public HybrasylFixture Fixture { get; } = fixture;

    private static int SkipString8(byte[] data, int pos) => pos + 1 + data[pos];

    // Capture the 0x34 profile packet the subject sends to the invoker on click.
    private static byte[] ClickProfileData(User subject)
    {
        var invoker = new User { Name = "LegendInvoker" };
        var client = new TestClient(new TestSocket());
        typeof(User).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(invoker, client);

        subject.OnClick(invoker);

        Assert.True(client.ClientState.SendBufferTake(out var packet));
        Assert.Equal(0x34, packet.Opcode);

        // Post-DALib conversion the send queue carries the typed record directly (P5b); write
        // its body to get the same bytes the legacy Data field held.
        var record = Assert.IsAssignableFrom<DALib.Networking.Wire.ServerPacket>(packet.Packet);
        var writer = new DALib.Networking.Wire.PacketWriter();
        record.WriteBody(writer);
        return writer.WrittenSpan.ToArray();
    }

    [Fact]
    public void LegendCountByteMatchesEmittedPublicMarks()
    {
        var subject = Fixture.CreateUser("LegendSubject");
        subject.Legend.Clear();
        subject.PortraitData = new byte[0];
        subject.ProfileText = string.Empty;

        // Two public marks and one private mark: the count byte must reflect only
        // the two rows actually emitted, or everything after the legend block desyncs.
        subject.Legend.AddMark(LegendIcon.Community, LegendColor.White, "Public one", "pub1", isPublic: true);
        subject.Legend.AddMark(LegendIcon.Victory, LegendColor.Blue, "Public two", "pub2", isPublic: true);
        subject.Legend.AddMark(LegendIcon.Warrior, LegendColor.Red, "Private one", "priv1", isPublic: false);

        var data = ClickProfileData(subject);

        // Walk the fixed prefix up to the legend count byte.
        var pos = 4; // uint32 id
        pos += 18 * 3; // 18 equipment slots, 3 bytes each
        pos += 1; // group status
        pos = SkipString8(data, pos); // name
        pos += 1; // nation
        pos = SkipString8(data, pos); // title (always empty)
        pos += 1; // grouping flag
        pos = SkipString8(data, pos); // guild rank
        pos = SkipString8(data, pos); // class name
        pos = SkipString8(data, pos); // guild name

        var legendCount = data[pos++];

        // Count marks physically present between here and the 6-byte portrait/profile
        // tail (WriteUInt16 total-len + WriteUInt16 portrait-len + WriteString16 empty).
        var regionEnd = data.Length - 6;
        var emittedRows = 0;
        while (pos < regionEnd)
        {
            pos += 2; // icon + color
            pos = SkipString8(data, pos); // prefix
            pos = SkipString8(data, pos); // mark text
            emittedRows++;
        }

        Assert.Equal(regionEnd, pos); // legend block consumed exactly; tail intact
        Assert.Equal(2, emittedRows); // only the public marks were written
        Assert.Equal(emittedRows, legendCount); // count byte agrees with emitted rows
    }
}
