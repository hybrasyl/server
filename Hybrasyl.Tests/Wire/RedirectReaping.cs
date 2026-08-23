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
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Jobs;
using System;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     That a redirect nobody consumes eventually leaves the tables.
/// </summary>
/// <remarks>
///     A redirect is a credential. Both tables only shrank on the happy path — the destination's
///     entry is removed by the join that spends it, and the global entry was removed by nothing —
///     so a login never followed by a join left one behind for the life of the process.
/// </remarks>
[Collection("Hybrasyl")]
public class RedirectReaping
{
    public RedirectReaping(HybrasylFixture fixture) { }

    [Fact]
    public void ExpiredRedirects_AreSweptFromBothTables()
    {
        var previousLifetime = Redirect.Lifetime;
        var client = new TestClient(new TestSocket());
        var destination = Game.World;

        try
        {
            // Zero rather than a sleep: Expired is a comparison against Created, so the shortest
            // possible lifetime expires everything already minted without spending wall clock.
            Redirect.Lifetime = TimeSpan.Zero;

            var redirect = new Redirect(client, Game.Login, destination, "Kerden", 0,
                "UrkcnItnI"u8.ToArray());
            GlobalConnectionManifest.RegisterRedirect(client, redirect);
            destination.ExpectedConnections.TryAdd(redirect.Id, redirect);

            Assert.True(GlobalConnectionManifest.Redirects.ContainsKey(client.ConnectionId),
                "precondition: the global table holds the redirect");
            Assert.True(destination.ExpectedConnections.ContainsKey(redirect.Id),
                "precondition: the destination expects the connection");

            RedirectReaperJob.Execute(null!, null!);

            Assert.False(GlobalConnectionManifest.Redirects.ContainsKey(client.ConnectionId),
                "an unconsumed redirect stayed in the global table");
            Assert.False(destination.ExpectedConnections.ContainsKey(redirect.Id),
                "an unconsumed redirect stayed in the destination's expected connections");
        }
        finally
        {
            Redirect.Lifetime = previousLifetime;
            GlobalConnectionManifest.Redirects.TryRemove(client.ConnectionId, out _);
        }
    }

    /// <summary>
    ///     A redirect still inside its lifetime is left alone — the sweep must not race the join
    ///     that is about to spend it.
    /// </summary>
    [Fact]
    public void LiveRedirects_AreLeftAlone()
    {
        var client = new TestClient(new TestSocket());
        var destination = Game.World;
        var redirect = new Redirect(client, Game.Login, destination, "Kerden", 0,
            "UrkcnItnI"u8.ToArray());

        try
        {
            GlobalConnectionManifest.RegisterRedirect(client, redirect);
            destination.ExpectedConnections.TryAdd(redirect.Id, redirect);

            RedirectReaperJob.Execute(null!, null!);

            Assert.True(GlobalConnectionManifest.Redirects.ContainsKey(client.ConnectionId),
                "a redirect inside its lifetime was swept, which would break a live handoff");
            Assert.True(destination.ExpectedConnections.ContainsKey(redirect.Id),
                "a redirect inside its lifetime was swept from the destination");
        }
        finally
        {
            GlobalConnectionManifest.Redirects.TryRemove(client.ConnectionId, out _);
            destination.ExpectedConnections.TryRemove(redirect.Id, out _);
        }
    }
}
