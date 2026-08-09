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

using System;
using System.Text;
using DALib.Networking.Packets.Server;
using DALib.Networking.Wire;
using Xunit;
using LegacyServerPacket = Hybrasyl.Tests.Wire.LegacyBodyWriter;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Movement, map, visual and system send-path coverage. MATCH opcodes are pinned
///     byte-identical against the verbatim pre-conversion emit; slack-family opcodes
///     assert the typed body equals the legacy emit minus exactly the signed-off slack bytes,
///     and the status-bar cases assert the client-true color bytes.
/// </summary>
public class MovementAndVisualSendPath
{
    private static byte[] Body(DALib.Networking.Wire.ServerPacket record)
    {
        var writer = new PacketWriter();
        record.WriteBody(writer);
        return writer.WrittenSpan.ToArray();
    }

    // --- MATCH: byte-identical conversions ---

    [Fact]
    public void Location_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x04);
        legacy.WriteUInt16(12);
        legacy.WriteUInt16(34);
        legacy.WriteUInt16(0x00);
        legacy.WriteUInt16(0x00);

        var typed = Body(new LocationPacket { X = 12, Y = 34, Unknown1 = 0, Unknown2 = 0 });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void ConfirmWalk_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x0B);
        legacy.WriteByte(2); // South
        legacy.WriteUInt16(10);
        legacy.WriteUInt16(11);
        legacy.WriteUInt16(0x0B);
        legacy.WriteUInt16(0x0B);
        legacy.WriteByte(0x01);

        var typed = Body(new ConfirmWalkPacket
        {
            Direction = DALib.Enums.Direction.South,
            OldX = 10,
            OldY = 11
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void CreatureWalk_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x0C);
        legacy.WriteUInt32(0xDEAD1234);
        legacy.WriteUInt16(5);
        legacy.WriteUInt16(6);
        legacy.WriteByte(1); // East
        legacy.WriteByte(0x00);

        var typed = Body(new CreatureWalkPacket
        {
            SourceId = 0xDEAD1234,
            OldX = 5,
            OldY = 6,
            Direction = DALib.Enums.Direction.East
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void CreatureTurn_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x11);
        legacy.WriteUInt32(77);
        legacy.WriteByte(3); // West

        var typed = Body(new CreatureTurnPacket { SourceId = 77, Direction = DALib.Enums.Direction.West });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void RemoveObject_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x0E);
        legacy.WriteUInt32(0xCAFE);

        var typed = Body(new RemoveObjectPacket { SourceId = 0xCAFE });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void HealthBar_MatchesLegacyBody()
    {
        // Legacy builder: [u32 id][0][percent][sound ?? 0xFF]
        var legacy = new LegacyServerPacket(0x13);
        legacy.WriteUInt32(42);
        legacy.WriteByte(0);
        legacy.WriteByte(63);
        legacy.WriteByte(0xFF);

        var typed = Body(new HealthBarPacket { SourceId = 42, HealthPercent = 63 });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void MapInfo_ChecksumSwapKeepsWireBytesIdentical()
    {
        // Crc16.Calculate returns byteswapped CCITT; legacy wrote it low-then-high, which
        // lands big-endian true CCITT on the wire. The typed emit must reproduce that via
        // ReverseEndianness.
        const ushort hybrasylChecksum = 0x99BE; // byteswap of true CCITT 0xBE99 (lod505.map)

        var legacy = new LegacyServerPacket(0x15);
        legacy.WriteUInt16(500);
        legacy.WriteByte(50);
        legacy.WriteByte(50);
        legacy.WriteByte(0x03);
        legacy.WriteUInt16(0);
        legacy.WriteByte(hybrasylChecksum % 256);
        legacy.WriteByte(hybrasylChecksum / 256);
        legacy.WriteString8("Mileth");

        var typed = Body(new MapInfoPacket
        {
            MapId = 500,
            Width = 50,
            Height = 50,
            Flags = 0x03,
            Checksum = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(hybrasylChecksum),
            Name = "Mileth"
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void PlaySound_EffectAndMusicForms_MatchLegacyBodies()
    {
        var legacySound = new LegacyServerPacket(0x19);
        legacySound.WriteByte(12);
        Assert.Equal(legacySound.BodyMemory.ToArray(), Body(new PlaySoundPacket { Sound = 12 }));

        var legacyMusic = new LegacyServerPacket(0x19);
        legacyMusic.WriteByte(0xFF);
        legacyMusic.WriteByte(4);
        Assert.Equal(legacyMusic.BodyMemory.ToArray(),
            Body(new PlaySoundPacket { Sound = PlaySoundPacket.MusicMarker, MusicTrack = 4 }));
    }

    [Fact]
    public void LightLevel_And_Refresh_And_CancelCast_MatchLegacyBodies()
    {
        Assert.Equal(new byte[] { 13 }, Body(new LightLevelPacket { LightLevel = 13 }));
        Assert.Equal(new byte[] { 0x00 }, Body(new RefreshPacket()));
        Assert.Equal(new byte[] { 0x00 }, Body(new CancelCastPacket()));
    }

    [Fact]
    public void Door_EmptyAndSingle_MatchLegacyBodies()
    {
        // Post-walk empty door packet: [0x00]
        Assert.Equal(new byte[] { 0x00 }, Body(new DoorPacket()));

        var legacy = new LegacyServerPacket(0x32);
        legacy.WriteByte(1);
        legacy.WriteByte(8);
        legacy.WriteByte(9);
        legacy.WriteBoolean(true);
        legacy.WriteBoolean(false);

        var typed = Body(new DoorPacket
        {
            Doors = [new Door { X = 8, Y = 9, Closed = true, OpenRight = false }]
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void MapData_MatchesLegacyBody()
    {
        var rowData = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };

        var legacy = new LegacyServerPacket(0x3C);
        legacy.WriteUInt16(7);
        legacy.Write(rowData);

        var typed = Body(new MapDataPacket { RowIndex = 7, RowData = rowData });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void AcceptConnection_WithNewline_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x7E);
        legacy.WriteByte(0x1B);
        legacy.Write(Encoding.ASCII.GetBytes("CONNECTED SERVER\n"));

        var typed = Body(new AcceptConnectionPacket { Message = "CONNECTED SERVER\n" });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void PublicMessage_SpeechForm_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x0D);
        legacy.WriteBoolean(true); // shout
        legacy.WriteUInt32(99);
        legacy.WriteString8("Kerden! hello");

        var typed = Body(new PublicMessagePacket
        {
            Type = PublicMessagePacket.TypeShout,
            SourceId = 99,
            Message = "Kerden! hello"
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void DrawObjects_CreatureAndMerchant_MatchLegacyBodies()
    {
        // Non-merchant creature: no name, type 0
        var legacy = new LegacyServerPacket(0x07);
        legacy.WriteUInt16(1);
        legacy.WriteUInt16(20);
        legacy.WriteUInt16(21);
        legacy.WriteUInt32(1000);
        legacy.WriteUInt16(0x4123);
        legacy.WriteByte(0);
        legacy.WriteByte(0);
        legacy.WriteByte(0);
        legacy.WriteByte(0);
        legacy.WriteByte(2); // direction
        legacy.WriteByte(0);
        legacy.WriteByte(0);

        var typed = Body(new DrawObjectsPacket
        {
            Objects =
            [
                new CreatureWorldObject { X = 20, Y = 21, Id = 1000, Sprite = 0x4123, Direction = 2 }
            ]
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);

        // Merchant: type 2 + string8 name
        var legacyNpc = new LegacyServerPacket(0x07);
        legacyNpc.WriteUInt16(1);
        legacyNpc.WriteUInt16(20);
        legacyNpc.WriteUInt16(21);
        legacyNpc.WriteUInt32(1001);
        legacyNpc.WriteUInt16(0x4123);
        legacyNpc.WriteByte(0);
        legacyNpc.WriteByte(0);
        legacyNpc.WriteByte(0);
        legacyNpc.WriteByte(0);
        legacyNpc.WriteByte(1);
        legacyNpc.WriteByte(0);
        legacyNpc.WriteByte(2);
        legacyNpc.WriteString8("Riona");

        var typedNpc = Body(new DrawObjectsPacket
        {
            Objects =
            [
                new CreatureWorldObject
                {
                    X = 20, Y = 21, Id = 1001, Sprite = 0x4123, Direction = 1,
                    Type = CreatureWorldObject.TypeNamed, Name = "Riona"
                }
            ]
        });

        Assert.Equal(legacyNpc.BodyMemory.ToArray(), typedNpc);
    }

    // --- Slack family: typed body == legacy body minus the signed-off slack ---

    [Fact]
    public void DrawObjects_Item_DropsOneTrailingByte_W16()
    {
        // Legacy item emit: color + 3×0x00 (14 bytes/object); retail-true is 13.
        var legacy = new LegacyServerPacket(0x07);
        legacy.WriteUInt16(1);
        legacy.WriteUInt16(30);
        legacy.WriteUInt16(31);
        legacy.WriteUInt32(2000);
        legacy.WriteUInt16(0x8456);
        legacy.WriteByte(7); // color
        legacy.WriteByte(0);
        legacy.WriteByte(0);
        legacy.WriteByte(0);

        var typed = Body(new DrawObjectsPacket
        {
            Objects = [new ItemWorldObject { X = 30, Y = 31, Id = 2000, Sprite = 0x8456, Color = 7 }]
        });

        var legacyBody = legacy.BodyMemory.ToArray();
        Assert.Equal(legacyBody.Length - 1, typed.Length);
        Assert.Equal(legacyBody[..^1], typed);
    }

    [Fact]
    public void Reactor_ItemFormEmit_W17()
    {
        // The client consumes exactly 13 bytes for an item-range sprite; the legacy
        // creature-style tail (type + name) was never read. Typed emit is the item form.
        var typed = Body(new DrawObjectsPacket
        {
            Objects = [new ItemWorldObject { X = 3, Y = 4, Id = 500, Sprite = (ushort)(12192 + 0x8000) }]
        });

        var expected = new byte[]
        {
            0x00, 0x01, // count
            0x00, 0x03, // x
            0x00, 0x04, // y
            0x00, 0x00, 0x01, 0xF4, // id
            0xAF, 0xA0, // sprite 12192 + 0x8000
            0x00, 0x00, 0x00 // color, direction, unknown
        };
        Assert.Equal(expected, typed);
    }

    [Fact]
    public void UserAppearance_DropsTrailingZero_W15()
    {
        var legacy = new LegacyServerPacket(0x05);
        legacy.WriteUInt32(123);
        legacy.WriteByte(2); // direction
        legacy.WriteByte(0x00);
        legacy.WriteByte(1); // class
        legacy.WriteByte(0x00);
        legacy.WriteByte(1); // gender male
        legacy.WriteByte(0x00); // the slack byte

        var typed = Body(new UserAppearancePacket
        {
            Id = 123,
            Direction = 2,
            Class = 1,
            Gender = DALib.Enums.Gender.Male
        });

        var legacyBody = legacy.BodyMemory.ToArray();
        Assert.Equal(legacyBody.Length - 1, typed.Length);
        Assert.Equal(legacyBody[..^1], typed);
    }

    [Fact]
    public void PublicMessage_ChantForm_DropsThreeTrailingZeros_W18()
    {
        // Legacy CastLine builder: [2][u32 id][len][raw text][0][0][0]
        var legacy = new LegacyServerPacket(0x0D);
        legacy.WriteByte(2);
        legacy.WriteUInt32(55);
        legacy.WriteString8("ionic sal");
        legacy.WriteByte(0);
        legacy.WriteByte(0);
        legacy.WriteByte(0);

        var typed = Body(new PublicMessagePacket
        {
            Type = PublicMessagePacket.TypeChant,
            SourceId = 55,
            Message = "ionic sal"
        });

        var legacyBody = legacy.BodyMemory.ToArray();
        Assert.Equal(legacyBody.Length - 3, typed.Length);
        Assert.Equal(legacyBody[..^3], typed);
    }

    [Fact]
    public void PlayerAnimation_DropsTrailing0xFF_W4()
    {
        var legacy = new LegacyServerPacket(0x1A);
        legacy.WriteUInt32(321);
        legacy.WriteByte(6);
        legacy.WriteInt16(20);
        legacy.WriteByte(byte.MaxValue); // the slack byte

        var typed = Body(new PlayerAnimationPacket { SourceId = 321, Animation = 6, Speed = 20 });

        var legacyBody = legacy.BodyMemory.ToArray();
        Assert.Equal(legacyBody.Length - 1, typed.Length);
        Assert.Equal(legacyBody[..^1], typed);
    }

    [Fact]
    public void SpellAnimation_TargetedDropsTrailingZero_W19_AreaMatches()
    {
        var legacyTargeted = new LegacyServerPacket(0x29);
        legacyTargeted.WriteUInt32(10);
        legacyTargeted.WriteUInt32(11);
        legacyTargeted.WriteUInt16(50);
        legacyTargeted.WriteUInt16(51);
        legacyTargeted.WriteInt16(100);
        legacyTargeted.WriteByte(0x00); // the slack byte

        var typedTargeted = Body(new SpellAnimationPacket
        {
            TargetId = 10,
            SourceId = 11,
            TargetAnimation = 50,
            SourceAnimation = 51,
            Speed = 100
        });

        var legacyBody = legacyTargeted.BodyMemory.ToArray();
        Assert.Equal(legacyBody.Length - 1, typedTargeted.Length);
        Assert.Equal(legacyBody[..^1], typedTargeted);

        var legacyArea = new LegacyServerPacket(0x29);
        legacyArea.WriteUInt32(0);
        legacyArea.WriteUInt16(50);
        legacyArea.WriteInt16(100);
        legacyArea.WriteInt16(15);
        legacyArea.WriteInt16(16);

        var typedArea = Body(new SpellAnimationPacket
        {
            TargetId = 0,
            TargetAnimation = 50,
            Speed = 100,
            X = 15,
            Y = 16
        });

        Assert.Equal(legacyArea.BodyMemory.ToArray(), typedArea);
    }

    [Fact]
    public void ConfirmExit_DropsTrailingU16_W20()
    {
        Assert.Equal(new byte[] { 0x01 }, Body(new ConfirmExitPacket { ExitConfirmed = true }));
    }

    // --- client-true status bar colors ---

    [Theory]
    [InlineData(StatusBarColor.None, 0)]
    [InlineData(StatusBarColor.Blue, 1)]
    [InlineData(StatusBarColor.Green, 2)]
    [InlineData(StatusBarColor.Orange, 4)]
    [InlineData(StatusBarColor.Red, 5)]
    [InlineData(StatusBarColor.White, 6)]
    public void StatusBar_EmitsClientTrueColorBytes_W6(StatusBarColor color, byte expectedByte)
    {
        var body = Body(new StatusBarPacket { Icon = 0x0102, Color = color });
        Assert.Equal(new byte[] { 0x01, 0x02, expectedByte }, body);
    }
}
