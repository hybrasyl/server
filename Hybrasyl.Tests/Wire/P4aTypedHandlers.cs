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
using System.Buffers.Binary;
using DALib.Networking.Packets.Client;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Phase 4a (movement / combat / system) receive-path coverage. Each case feeds DALib's
///     <c>Parse</c> the bytes the retail client sends and pins the field mapping the converted
///     handler now relies on — the receive-side counterpart of the P3 emit goldens.
/// </summary>
public class P4aTypedHandlers
{
    [Theory]
    [InlineData(0, 0x11)]
    [InlineData(3, 0x00)]
    public void Walk_ParsesDirectionAndSequence(byte direction, byte sequence)
    {
        var parsed = WalkPacket.Parse([direction, sequence]);

        Assert.Equal(direction, (byte) parsed.Direction);
        Assert.Equal(sequence, parsed.Sequence);
    }

    [Fact]
    public void Walk_OutOfRangeDirectionSurvivesParseSoTheHandlerGuardStillMatters()
    {
        // DALib casts the wire byte straight to the enum without validating it, which is why
        // the handler keeps its own > 3 check against crafted packets.
        var parsed = WalkPacket.Parse([0x7F, 0x00]);

        Assert.Equal(0x7F, (byte) parsed.Direction);
        Assert.False(Enum.IsDefined(parsed.Direction));
    }

    [Fact]
    public void Turn_ParsesDirection()
    {
        Assert.Equal(2, (byte) TurnPacket.Parse([0x02]).Direction);
    }

    [Fact]
    public void UseSpell_TargetedCarriesSerialInArgs()
    {
        // [u8 slot][u32 target][u16 x][u16 y] — the handler reads only the serial.
        var body = new byte[9];
        body[0] = 12;
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(1), 0xDEADBEEF);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(5), 40);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(7), 41);

        var parsed = UseSpellPacket.Parse(body);

        Assert.Equal(12, parsed.Slot);
        Assert.Equal(8, parsed.Args.Length);
        Assert.Equal(0xDEADBEEFu, BinaryPrimitives.ReadUInt32BigEndian(parsed.Args));
    }

    [Fact]
    public void UseSpell_NoTargetLeavesArgsEmpty()
    {
        // The legacy positional read threw IndexOutOfRangeException here and the queue consumer
        // swallowed it, so the cast silently never happened. The handler now treats an absent
        // tail as target 0, which UseSpell already models as "no target".
        var parsed = UseSpellPacket.Parse([7]);

        Assert.Equal(7, parsed.Slot);
        Assert.Empty(parsed.Args);
    }

    [Fact]
    public void UseSkill_ParsesSlot()
    {
        Assert.Equal(5, UseSkillPacket.Parse([5]).Slot);
    }

    [Theory]
    [InlineData(0, ExitSignal.Confirm)]
    [InlineData(1, ExitSignal.Request)]
    public void ClientExit_ParsesSignal(byte wire, ExitSignal expected)
    {
        // Pinned through the legacy test injector: the bytes the client actually sends.
        var injected = (Hybrasyl.Networking.ClientPacket) new Hybrasyl.Networking.ClientPackets.LeaveWorld(wire);

        Assert.Equal(expected, ClientExitPacket.Parse(injected.PayloadData).Signal);
    }

    [Fact]
    public void ClientJoin_ParsesRedirectFieldsFromTheLegacyInjector()
    {
        var injected = (Hybrasyl.Networking.ClientPacket)
            new Hybrasyl.Networking.ClientPackets.JoinWorld(0x0B, "NILCHIRSTNA", "Kerden", 0x12345678);

        var parsed = ClientJoinPacket.Parse(injected.PayloadData);

        Assert.Equal(0x0B, parsed.EncryptionSeed);
        Assert.Equal("NILCHIRSTNA"u8.ToArray(), parsed.EncryptionKey);
        Assert.Equal("Kerden", parsed.Name);
        Assert.Equal(0x12345678u, parsed.RedirectId);
    }

    [Fact]
    public void ByteHeartbeat_KeepsWireOrder()
    {
        // The client answers with the two bytes reversed; the handler relabels them, so the
        // record must hand them back in wire order.
        var parsed = ByteHeartbeatPacket.Parse([0xAA, 0xBB]);

        Assert.Equal(0xAA, parsed.First);
        Assert.Equal(0xBB, parsed.Second);
    }

    [Fact]
    public void TickHeartbeat_ParsesBothTicksBigEndian()
    {
        var body = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(body, 0x0000ABCD);
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(4), 0x00001234);

        var parsed = TickHeartbeatPacket.Parse(body);

        Assert.Equal(0x0000ABCD, (int) parsed.ServerTick);
        Assert.Equal(0x00001234, (int) parsed.ClientTick);
    }

    [Fact]
    public void Settings_ParsesSettingNumber()
    {
        Assert.Equal(0, SettingsPacket.Parse([0]).SettingNumber);
        Assert.Equal(6, SettingsPacket.Parse([6]).SettingNumber);
    }

    [Fact]
    public void Emote_ParsesIndex()
    {
        Assert.Equal(35, EmotePacket.Parse([35]).EmoteIndex);
    }

    [Fact]
    public void Status_ParsesGroupStatus()
    {
        Assert.Equal(7, StatusPacket.Parse([7]).Status);
    }

    [Fact]
    public void CastLine_ParsesString8()
    {
        var parsed = CastLinePacket.Parse([0x05, (byte) 'a', (byte) 'r', (byte) 'd', (byte) 'c', (byte) 'r']);

        Assert.Equal("ardcr", parsed.Line);
    }

    [Fact]
    public void RequestMetafile_NameIsPresentExactlyWhenAllIsUnset()
    {
        var manifest = RequestMetafilePacket.Parse([0x01]);
        Assert.True(manifest.All);
        Assert.Null(manifest.Name);

        var byName = RequestMetafilePacket.Parse([0x00, 0x07, .. "SClass1"u8]);
        Assert.False(byName.All);
        Assert.Equal("SClass1", byName.Name);
    }
}
