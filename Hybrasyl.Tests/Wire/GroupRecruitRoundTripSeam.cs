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

using DALib.Networking.Wire;
using Hybrasyl.Networking;
using Xunit;
using GroupRequestPacket = DALib.Networking.Packets.Client.GroupRequestPacket;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The whole 0x2E → 0x63 chain, driven the way a DALib client drives it: DALib's own
///     <c>WriteBody</c> produces the C→S bytes, Hybrasyl's registered handler consumes them,
///     and the S→C reply is decoded back to bytes and read by hand.
/// </summary>
/// <remarks>
///     Every existing test covers one link. <see cref="GroupRequestWire" /> pins the parse
///     against a hand-built body; <see cref="GroupRecruitSelfView" /> asserts on the reply's
///     typed properties rather than its bytes. Neither runs a byte-in/byte-out round trip, so a
///     transposition introduced anywhere between DALib's writer and DALib's wire order would
///     survive both. The reply is decoded here by hand rather than with DALib's reader, so a
///     reader that agrees with a broken writer cannot make this pass.
/// </remarks>
[Collection("Hybrasyl")]
public class GroupRecruitRoundTripSeam(HybrasylFixture fixture)
{
    private HybrasylFixture Fixture { get; } = fixture;

    private static byte[] ClientBody(GroupRequestPacket packet)
    {
        var writer = new PacketWriter();
        packet.WriteBody(writer);

        return writer.WrittenSpan.ToArray();
    }

    private static byte[] ServerBody(IServerPacket packet)
    {
        var writer = new PacketWriter();
        packet.WriteBody(writer);

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Skips a string8 and returns the offset just past it.</summary>
    private static int SkipString8(byte[] body, int offset) => offset + 1 + body[offset];

    [Fact]
    public void CapsSurviveTheFullClientServerClientRoundTrip()
    {
        var user = Fixture.TestUser;
        Fixture.ResetTestUserStats();

        var client = HybrasylFixture.AttachTestClient(user, out var restore);

        try
        {
            // C→S exactly as a DALib client emits it: named arguments in, writer's order out.
            var submitted = ClientBody(GroupRequestPacket.Groupbox(
                user.Name, "raid", "bring food", 1, 99,
                maxWarrior: 1, maxWizard: 2, maxRogue: 3, maxPriest: 4, maxMonk: 5));

            Game.World.WorldPacketHandlers[0x2E].Invoke(user, new InboundPacket(0x2E, submitted));

            while (client.ClientState.SendBufferTake(out _)) { }

            // The self-targeted stage 5 Brigid sends to populate its recruit tab.
            var query = ClientBody(GroupRequestPacket.ViewRecruitInfo(user.Name));
            Game.World.WorldPacketHandlers[0x2E].Invoke(user, new InboundPacket(0x2E, query));

            Assert.True(client.ClientState.SendBufferTake(out var sent), "expected a 0x63 reply");
            Assert.Equal(0x63, sent.Opcode);

            var body = ServerBody(sent.Packet);

            // [04][string8 recruiter][string8 group][string8 note][u8 min][u8 max]
            // then five (wanted, current) pairs — Warrior, Wizard, Rogue, Priest, Monk.
            Assert.Equal(0x04, body[0]);
            var offset = SkipString8(body, 1);
            offset = SkipString8(body, offset);
            offset = SkipString8(body, offset);
            offset += 2; // min, max level

            var wanted = new[]
            {
                body[offset], body[offset + 2], body[offset + 4], body[offset + 6], body[offset + 8]
            };

            // What the player typed top-to-bottom must come back top-to-bottom.
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, wanted);
        }
        finally
        {
            user.GroupRecruit = null;
            restore.Dispose();
        }
    }
}
