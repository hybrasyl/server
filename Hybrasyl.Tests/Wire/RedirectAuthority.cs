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

using DALib.Networking.Packets.Client;
using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     What a redirect proves, and what it does not.
/// </summary>
[Collection("Hybrasyl")]
public class RedirectAuthority
{
    public RedirectAuthority(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    private static byte[] Key => "UrkcnItnI"u8.ToArray();

    /// <summary>
    ///     Holding a redirect for one account does not let you move another account's login state.
    /// </summary>
    /// <remarks>
    ///     The login join handler looked the client-supplied name up and persisted
    ///     <c>CurrentState = Login</c> <em>before</em> checking that the name belonged to the
    ///     redirect. That state is what Login and World read to refuse a second concurrent login,
    ///     so this was not vandalism — it removed the guard that makes a redirect race hard to win,
    ///     for any named account, from any caller holding any valid redirect of their own.
    /// </remarks>
    [Fact]
    public void AJoinNamingAnotherAccount_DoesNotTouchThatAccountsState()
    {
        var attacker = Fixture.CreateUser("Attackerone");
        var victim = Fixture.CreateUser("Victimone");
        _ = attacker.GuidReference;
        _ = victim.GuidReference;

        Assert.True(Game.World.WorldState.TryGetAuthInfo(victim.Name, out var victimAuth),
            "precondition: the victim has stored auth info");
        var stateBefore = victimAuth.CurrentState;
        Assert.NotEqual(UserState.Login, stateBefore);

        var socket = new TestSocket();
        var client = new TestClient(socket) { EncryptionSeed = 0, EncryptionKey = Key };
        GlobalConnectionManifest.RegisterClient(client);

        // A redirect the caller legitimately holds -- for their own account. Source is World
        // rather than Lobby only because the fixture does not stand a lobby up.
        var redirect = new Redirect(client, Game.World, Game.Login, attacker.Name, 0, Key);
        Game.Login.ExpectedConnections.TryAdd(redirect.Id, redirect);

        try
        {
            var join = new ClientJoinPacket
            {
                EncryptionSeed = 0,
                EncryptionKey = Key,
                Name = victim.Name,
                RedirectId = redirect.Id,
            };

            Game.Login.PacketHandlers[join.Opcode]
                .Invoke(client, new InboundBody(join.Opcode, join.ToBody()));

            Assert.True(Game.World.WorldState.TryGetAuthInfo(victim.Name, out var victimAuthAfter),
                "the victim's auth info vanished");
            Assert.Equal(stateBefore, victimAuthAfter.CurrentState);
            Assert.True(socket.IsDisconnected,
                "a join that does not match its redirect left the connection open");
        }
        finally
        {
            Game.Login.ExpectedConnections.TryRemove(redirect.Id, out _);
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    /// <summary>
    ///     Redirect ids are unpredictable.
    /// </summary>
    /// <remarks>
    ///     The id is the only part of <c>Redirect.Matches</c> an attacker does not already hold:
    ///     the name is public, the seed is one byte, and the key is a per-connection value that
    ///     falls back to a published constant when no encryption endpoint is configured. While the
    ///     id was a counter from zero, the whole credential was guessable, which is what makes the
    ///     takeover race worth running.
    /// </remarks>
    [Fact]
    public void RedirectIds_AreNotDrawnFromACounter()
    {
        var client = new TestClient(new TestSocket());
        var ids = Enumerable.Range(0, 32)
            .Select(_ => new Redirect(client, Game.World, Game.Login, "Kerden", 0, Key).Id)
            .ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());

        // 32 draws from a counter span 31. Thirty-two uniform draws over the full uint range span
        // most of it; the chance of them all landing inside a 2^24 window is far below any rate
        // this suite could observe.
        var span = ids.Max() - ids.Min();
        Assert.True(span > 1u << 24,
            $"redirect ids span only {span}, which is what a counter looks like");
    }

    /// <summary>
    ///     A world join whose connection closed while the packet sat in the queue is discarded.
    /// </summary>
    /// <remarks>
    ///     World packets are queued and drained by one thread, so anything may close the connection
    ///     between the join arriving and being handled — the admission reaper above all. Admitting
    ///     one that is gone persists <c>InWorld</c> against a socket nobody holds, which no later
    ///     cleanup undoes because the cleanup already ran.
    /// </remarks>
    [Fact]
    public void AJoinForAClosedConnection_DoesNotAdmitTheAccount()
    {
        var user = Fixture.CreateUser("Ghostone");
        _ = user.GuidReference;

        var client = new TestClient(new TestSocket()) { EncryptionSeed = 0, EncryptionKey = Key };
        GlobalConnectionManifest.RegisterClient(client);

        var redirect = new Redirect(client, Game.Login, Game.World, user.Name, 0, Key);
        Game.World.ExpectedConnections.TryAdd(redirect.Id, redirect);

        var join = new ClientJoinPacket
        {
            EncryptionSeed = 0,
            EncryptionKey = Key,
            Name = user.Name,
            RedirectId = redirect.Id,
        };

        try
        {
            // What the reaper does: the connection is gone, but the join is already queued.
            GlobalConnectionManifest.DeregisterClient(client);

            Game.World.WorldPacketHandlers[join.Opcode]
                .Invoke(client.ConnectionId, new InboundBody(join.Opcode, join.ToBody()));

            Assert.True(Game.World.WorldState.TryGetAuthInfo(user.Name, out var auth),
                "the user's auth info vanished");
            Assert.NotEqual(UserState.InWorld, auth.CurrentState);
        }
        finally
        {
            Game.World.ExpectedConnections.TryRemove(redirect.Id, out _);
        }
    }

    /// <summary>
    ///     A redirect whose id is already outstanding is refused rather than sent.
    /// </summary>
    /// <remarks>
    ///     Ids are drawn at random now, so a collision is vanishingly rare — and was entirely
    ///     silent: <c>TryAdd</c>'s result was discarded and the client was handed an id resolving to
    ///     another caller's credentials, to be refused at the far end for a mismatch it could not
    ///     see. Rarity is why this needs a test rather than why it does not.
    /// </remarks>
    [Fact]
    public void ARedirectWhoseIdIsAlreadyOutstanding_IsRefused()
    {
        var incumbentClient = new TestClient(new TestSocket());
        var collidingSocket = new TestSocket();
        // A real Client, not the double: refusing is the production path's behaviour, and the
        // double deliberately throws instead so a colliding id cannot pass unnoticed in a harness.
        var collidingClient = new Client(collidingSocket, Game.Login);

        var incumbent = new Redirect(incumbentClient, Game.World, Game.Login, "Kerden", 0, Key);
        Game.Login.ExpectedConnections.TryAdd(incumbent.Id, incumbent);

        var colliding = new Redirect(collidingClient, Game.World, Game.Login, "Someoneelse", 0, Key)
        {
            Id = incumbent.Id,
        };

        try
        {
            collidingClient.Redirect(colliding);

            Assert.Same(incumbent, Game.Login.ExpectedConnections[incumbent.Id]);
            // Client.Disconnect tears down through ClientState.Dispose rather than the socket's
            // own Disconnect, so Connected is the observable here and IsDisconnected is not.
            Assert.False(collidingClient.Connected,
                "a client was left holding a redirect id that resolves to someone else's credentials");
            Assert.Equal(0, collidingSocket.SentCount);
            Assert.False(GlobalConnectionManifest.Redirects.ContainsKey(collidingClient.ConnectionId),
                "the global table kept a redirect the destination never accepted");
        }
        finally
        {
            Game.Login.ExpectedConnections.TryRemove(incumbent.Id, out _);
            GlobalConnectionManifest.Redirects.TryRemove(incumbentClient.ConnectionId, out _);
            GlobalConnectionManifest.Redirects.TryRemove(collidingClient.ConnectionId, out _);
        }
    }
}
