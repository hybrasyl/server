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
using Hybrasyl.Subsystems.Players.Grouping;
using Xunit;
using GroupRequestPacket = DALib.Networking.Packets.Client.GroupRequestPacket;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     C&#8594;S 0x2E stage 5 (RecruitInfo) aimed at yourself must answer with your own recruit box.
/// </summary>
/// <remarks>
///     <para>
///         Brigid opens its recruit tab by sending a <em>self</em>-targeted ViewGroupBox and
///         populating the panel from the server's reply, so a server that refuses the self-query
///         leaves the tab blank — "start a recruitment, click it, nothing happens". Observed
///         working against USDA and not against Hybrasyl, which is what prompted this.
///     </para>
///     <para>
///         Grounding, since the two halves differ: that Brigid emits it is read from Brigid's
///         source, and that a live server answers it is J's observation. Whether the <em>retail</em>
///         client emits stage 5 at all is still disputed — the protocol reference marks it
///         unresolved pending a fresh disassembly sweep (HTOO-259). This test pins Hybrasyl's side of the
///         exchange, which is correct under either answer.
///     </para>
///     <para>
///         Dispatch goes through <c>WorldPacketHandlers</c> rather than at the handler method, so
///         the <c>[PacketHandler(0x2E)]</c> registration is part of what this covers. The body is
///         hand-assembled by <see cref="GroupRequestBodies" /> rather than by DALib's writer, so a
///         writer bug cannot make a broken chain look wired.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class GroupRecruitSelfView(HybrasylFixture fixture)
{
    private HybrasylFixture Fixture { get; } = fixture;

    [Fact]
    public void SelfTargetedRecruitInfoReturnsYourOwnBox()
    {
        var user = Fixture.TestUser;
        Fixture.ResetTestUserStats();

        var client = HybrasylFixture.AttachTestClient(user, out var restore);

        try
        {
            Assert.True(Game.World.UserConnected(user.Name), "test user must read as connected");

            user.GroupRecruit = GroupRecruit.FromRequest(new GroupRequestPacket
            {
                Stage = GroupRequestPacket.StageGroupbox,
                Leader = user.Name,
                Title = "a group",
                Note = "a note",
                MinLevel = 1,
                MaxLevel = 99,
                MaxWarrior = 1,
                MaxWizard = 2,
                MaxRogue = 3,
                MaxPriest = 4,
                MaxMonk = 5
            }, user);

            // Drain the setup's traffic so the assertion is about this dispatch.
            while (client.ClientState.SendBufferTake(out _)) { }

            var body = GroupRequestBodies.Simple(GroupRequestPacket.StageRecruitInfo, user.Name);
            Game.World.WorldPacketHandlers[0x2E].Invoke(user, new InboundPacket(0x2E, body));

            Assert.True(client.ClientState.SendBufferTake(out var sent),
                "self-targeted 0x2E stage 5 should answer with the recruit box");
            Assert.Equal(0x63, sent.Opcode);

            // It must be *this* box: the caps prove the body came from the recruit set up above
            // rather than from a default-constructed reply.
            var info = Assert.IsType<DALib.Networking.Packets.Server.GroupRecruitInfoPacket>(sent.Packet);
            Assert.Equal(user.Name, info.Info.RecruiterName);
            Assert.Equal(3, info.Info.RoguesWanted);
            Assert.Equal(5, info.Info.MonksWanted);
        }
        finally
        {
            user.GroupRecruit = null;
            restore.Dispose();
        }

        // The helper must undo the registration as well as the client, or every later test in the
        // collection inherits a user that resolves by name but cannot be sent to.
        Assert.False(Game.World.TryGetActiveUser(user.Name, out _),
            "AttachTestClient's restore must deregister the user it added");
    }
}
