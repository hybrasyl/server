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
using System;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     An expired redirect is refused where it is spent, not merely where it is swept.
/// </summary>
/// <remarks>
///     <see cref="RedirectReaperJob" /> runs on an interval, so leaving expiry to the sweep alone
///     makes the real lifetime "<c>Redirect.Lifetime</c> plus up to one interval" and puts the
///     security property in a race with the join it is supposed to bound. These drive the two
///     handlers that actually spend a redirect.
/// </remarks>
[Collection("Hybrasyl")]
public class RedirectExpiry(HybrasylFixture fixture)
{
    private static readonly byte[] Key = "UrkcnItnI"u8.ToArray();

    private HybrasylFixture Fixture { get; } = fixture;

    [Fact]
    public void AnExpiredRedirect_IsNotHonouredByTheWorldJoin()
    {
        var user = Fixture.CreateUser("Expiryworld");
        _ = user.GuidReference;

        var socket = new TestSocket();
        var client = new TestClient(socket) { EncryptionSeed = 0, EncryptionKey = Key };
        GlobalConnectionManifest.RegisterClient(client);

        var saved = Redirect.Lifetime;
        Redirect.Lifetime = TimeSpan.Zero;
        var redirect = new Redirect(client, Game.Login, Game.World, user.Name, 0, Key);
        Game.World.ExpectedConnections.TryAdd(redirect.Id, redirect);

        try
        {
            Assert.True(redirect.Expired, "precondition: the redirect has outlived its lifetime");

            var join = new ClientJoinPacket
            {
                EncryptionSeed = 0, EncryptionKey = Key, Name = user.Name, RedirectId = redirect.Id
            };
            Game.World.WorldPacketHandlers[join.Opcode]
                .Invoke(client.ConnectionId, new InboundBody(join.Opcode, join.ToBody()));

            Assert.True(Game.World.WorldState.TryGetAuthInfo(user.Name, out var auth),
                "the account's auth info vanished");
            Assert.NotEqual(UserState.InWorld, auth.CurrentState);
            Assert.True(socket.IsDisconnected, "an expired redirect left the connection open");
        }
        finally
        {
            Redirect.Lifetime = saved;
            Game.World.ExpectedConnections.TryRemove(redirect.Id, out _);
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    [Fact]
    public void AnExpiredRedirect_IsNotHonouredByTheLoginJoin()
    {
        var user = Fixture.CreateUser("Expirylogin");
        _ = user.GuidReference;

        var socket = new TestSocket();
        var client = new TestClient(socket) { EncryptionSeed = 0, EncryptionKey = Key };
        GlobalConnectionManifest.RegisterClient(client);

        var saved = Redirect.Lifetime;
        Redirect.Lifetime = TimeSpan.Zero;
        var redirect = new Redirect(client, Game.World, Game.Login, user.Name, 0, Key);
        Game.Login.ExpectedConnections.TryAdd(redirect.Id, redirect);

        try
        {
            Assert.True(redirect.Expired, "precondition: the redirect has outlived its lifetime");

            var join = new ClientJoinPacket
            {
                EncryptionSeed = 0, EncryptionKey = Key, Name = user.Name, RedirectId = redirect.Id
            };
            Game.Login.PacketHandlers[join.Opcode]
                .Invoke(client, new InboundBody(join.Opcode, join.ToBody()));

            Assert.True(socket.IsDisconnected, "an expired redirect left the connection open");

            // Refused before the spend, so the credential is never consumed by the rejected join.
            Assert.True(Game.Login.ExpectedConnections.ContainsKey(redirect.Id),
                "the refused join consumed the redirect anyway");
        }
        finally
        {
            Redirect.Lifetime = saved;
            Game.Login.ExpectedConnections.TryRemove(redirect.Id, out _);
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }
}
