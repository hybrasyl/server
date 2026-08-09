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

using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class DisplayUserPacket(HybrasylFixture fixture)
{
    // 0x33 body offsets (opcode excluded):
    //   [0..1] X, [2..3] Y, [4] Direction, [5..8] Id, [9..10] appearance discriminator
    // The discriminator is 0xFFFF for the creature form (the monster sprite follows it
    // immediately); any other value is the head sprite of the equipment form.
    private const int DiscriminatorOffset = 9;

    public HybrasylFixture Fixture { get; } = fixture;

    // Capture the 0x33 the subject sends about itself and return its body bytes.
    private static byte[] UpdateBody(User subject)
    {
        var client = new TestClient(new TestSocket());
        subject.SendUpdateToUser(client);

        Assert.True(client.ClientState.SendBufferTake(out var packet));
        Assert.Equal(0x33, packet.Opcode);

        var record = Assert.IsAssignableFrom<DALib.Networking.Wire.ServerPacket>(packet.Packet);
        var writer = new DALib.Networking.Wire.PacketWriter();
        record.WriteBody(writer);
        return writer.WrittenSpan.ToArray();
    }

    [Fact]
    public void MorphedUserEmitsSentinelThenMonsterSprite()
    {
        var subject = Fixture.CreateUser("MorphSubject");
        subject.DisplayAsMonster = true;
        subject.MonsterSprite = 0x0405;

        var body = UpdateBody(subject);

        // Sentinel occupies the discriminator field...
        Assert.Equal(0xFF, body[DiscriminatorOffset]);
        Assert.Equal(0xFF, body[DiscriminatorOffset + 1]);
        // ...and the monster sprite follows immediately (big-endian), carrying the 0x4000
        // namespace tag the client subtracts back off to reach mns%03d.mpf.
        Assert.Equal(0x44, body[DiscriminatorOffset + 2]);
        Assert.Equal(0x05, body[DiscriminatorOffset + 3]);
    }

    [Fact]
    public void NonMorphedUserEmitsHeadSpriteNotSentinel()
    {
        var subject = Fixture.CreateUser("NonMorphSubject");
        subject.DisplayAsMonster = false;
        subject.HairStyle = 0x1122;

        var body = UpdateBody(subject);

        // No helmet equipped, so the head sprite is the hair style — and the client
        // must read the equipment form, not the creature form.
        Assert.Equal(0x11, body[DiscriminatorOffset]);
        Assert.Equal(0x22, body[DiscriminatorOffset + 1]);
    }
}
