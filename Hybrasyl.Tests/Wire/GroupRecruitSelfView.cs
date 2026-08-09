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
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Players.Grouping;
using System;
using System.Reflection;
using System.Text;
using Xunit;
using GroupRequestPacket = DALib.Networking.Packets.Client.GroupRequestPacket;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     C&#8594;S 0x2E stage 5 (RecruitInfo) aimed at yourself must answer with your own recruit box.
/// </summary>
/// <remarks>
///     <para>
///         Brigid opens the recruit tab by sending a <em>self</em>-targeted ViewGroupBox
///         (<c>WorldScreen.cs:372</c>, <c>SendGroupInvite(ViewGroupBox, WorldState.PlayerName)</c>)
///         and populating the panel from the server's reply — the comment on
///         <c>GroupTabControl.OnRecruitTabOpened</c> spells the contract out. Retail answers it:
///         the recruitment window opens on USDA.
///     </para>
///     <para>
///         Hybrasyl refused it. The <c>RecruitInfo</c> arm guarded on
///         <c>partner == user || partner.GroupRecruit == null</c>, so the self-query returned
///         before <c>ShowTo</c> and nothing was ever sent. The observable is "start a recruitment,
///         click it, nothing happens."
///     </para>
///     <para>
///         Not conversion fallout, despite looking like it: that guard dates to
///         <c>aacdc07</c> (2024-05-24) and P4c left the arm untouched. Brigid grew the self-query in
///         <c>f11d341</c> (2026-04-18), so the two have simply never agreed.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class GroupRecruitSelfView
{
    public GroupRecruitSelfView(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    /// <summary>Simple form: <c>[u8 Stage][string8 Name][u8 0]</c>.</summary>
    private static byte[] SimpleForm(byte stage, string name)
    {
        var bytes = Encoding.ASCII.GetBytes(name);
        var body = new byte[bytes.Length + 3];
        body[0] = stage;
        body[1] = (byte)bytes.Length;
        bytes.CopyTo(body, 2);
        body[^1] = 0x00; // trailing reserved zero, always 0
        return body;
    }

    private static void Dispatch(User user, byte[] body)
    {
        var handler = typeof(World).GetMethod("PacketHandler_0x2E_GroupRequest",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handler);
        handler.Invoke(Game.World, [user, new InboundPacket(0x2E, body)]);
    }

    [Fact]
    public void SelfTargetedRecruitInfoReturnsYourOwnBox()
    {
        var user = Fixture.TestUser;
        Fixture.ResetTestUserStats();

        var clientField = typeof(User).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(clientField);
        var oldClient = clientField.GetValue(user);
        var client = new TestClient(new TestSocket());
        clientField.SetValue(user, client);
        Game.World.AddUser(user, user.ConnectionId);

        try
        {
            Assert.True(Game.World.UserConnected(user.Name), "test user must read as connected");

            // Stand up a recruit box the way stage 4 would have.
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

            // Drain anything the setup left queued so the assertion is about this dispatch.
            while (client.ClientState.SendBufferTake(out _)) { }

            Dispatch(user, SimpleForm(GroupRequestPacket.StageRecruitInfo, user.Name));

            Assert.True(client.ClientState.SendBufferTake(out var sent),
                "self-targeted 0x2E stage 5 should answer with the recruit box");
            Assert.Equal(0x63, sent.Opcode);

            // And it must be *this* box, not an empty one — the caps prove the body came from the
            // recruit we stood up rather than from a default-constructed reply.
            var info = Assert.IsType<DALib.Networking.Packets.Server.GroupRecruitInfoPacket>(sent.Packet);
            Assert.Equal(user.Name, info.Info.RecruiterName);
            Assert.Equal(3, info.Info.RoguesWanted);
            Assert.Equal(5, info.Info.MonksWanted);
        }
        finally
        {
            user.GroupRecruit = null;
            clientField.SetValue(user, oldClient);
        }
    }
}
