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
using Hybrasyl.Networking;
using Hybrasyl.Networking.ServerPackets;
using Hybrasyl.Xml.Objects;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class DisplayUserPacket
{
    // 0x33 DisplayUser wire layout (fixed prefix):
    //   [0..1] X, [2..3] Y, [4] Direction, [5..8] Id, [9..10] Helmet/first-sprite field
    // The client only renders an aisling in creature form when the first sprite field
    // carries the 0xFFFF sentinel, immediately followed by the 16-bit monster sprite.
    private const int SpriteFieldOffset = 9;

    private static byte[] DataOf(DisplayUser display)
    {
        var packet = display.Packet();
        return (byte[]) typeof(Packet)
            .GetField("Data", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(packet)!;
    }

    [Fact]
    public void MorphedUserEmitsSentinelThenMonsterSprite()
    {
        var data = DataOf(new DisplayUser
        {
            DisplayAsMonster = true,
            Helmet = 0x1122,
            MonsterSprite = 0x0405
        });

        // Sentinel replaces the helmet/first-sprite 16-bit field...
        Assert.Equal(0xFF, data[SpriteFieldOffset]);
        Assert.Equal(0xFF, data[SpriteFieldOffset + 1]);
        // ...and the monster sprite follows immediately (big-endian).
        Assert.Equal(0x04, data[SpriteFieldOffset + 2]);
        Assert.Equal(0x05, data[SpriteFieldOffset + 3]);
    }

    [Fact]
    public void NonMorphedUserPayloadUnchanged()
    {
        var data = DataOf(new DisplayUser
        {
            DisplayAsMonster = false,
            Helmet = 0x1122,
            Gender = Gender.Male,
            BodySpriteOffset = 3
        });

        // Real helmet sprite is written (no sentinel), and the body sprite byte
        // that follows is unchanged: (byte)Gender * 16 + BodySpriteOffset.
        Assert.Equal(0x11, data[SpriteFieldOffset]);
        Assert.Equal(0x22, data[SpriteFieldOffset + 1]);
        Assert.Equal((byte) ((byte) Gender.Male * 16 + 3), data[SpriteFieldOffset + 2]);
    }
}
