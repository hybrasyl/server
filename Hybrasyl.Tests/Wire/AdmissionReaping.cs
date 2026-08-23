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
using Hybrasyl.Subsystems.Jobs;
using System;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     That a connection which never authenticates is eventually hung up on.
/// </summary>
/// <remarks>
///     The heartbeat reaper only ever considered world clients that already resolve to a user, so
///     nothing reaped a lobby connection, a login connection, or a world connection that never
///     joined — each holding a 65,600-byte buffer and a slot in a list walked every 50 ms.
/// </remarks>
[Collection("Hybrasyl")]
public class AdmissionReaping
{
    public AdmissionReaping(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    [Fact]
    public void AConnectionPastTheDeadlineWithNoUser_IsDisconnected()
    {
        var previous = AdmissionReaperJob.Deadline;
        var socket = new TestSocket();
        var client = new TestClient(socket);
        GlobalConnectionManifest.RegisterClient(client);

        try
        {
            // Zero rather than a sleep: the check is a comparison against ConnectedSince, so the
            // shortest possible deadline ages out everything already connected.
            AdmissionReaperJob.Deadline = TimeSpan.Zero;

            AdmissionReaperJob.Execute(null!, null!);

            Assert.True(socket.IsDisconnected,
                "an unadmitted connection was kept, so its buffer and poll slot are held for free");
        }
        finally
        {
            AdmissionReaperJob.Deadline = previous;
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    /// <summary>
    ///     A connection inside the deadline is left alone — the sweep must not disconnect a player
    ///     who is still reading the login screen.
    /// </summary>
    [Fact]
    public void AConnectionInsideTheDeadline_IsLeftAlone()
    {
        var socket = new TestSocket();
        var client = new TestClient(socket);
        GlobalConnectionManifest.RegisterClient(client);

        try
        {
            AdmissionReaperJob.Execute(null!, null!);

            Assert.False(socket.IsDisconnected,
                "a connection well inside the deadline was reaped");
        }
        finally
        {
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    /// <summary>
    ///     A connection that <em>is</em> admitted is exempt however old it is.
    /// </summary>
    /// <remarks>
    ///     This is the assertion that matters most in this file. The other two fail toward holding
    ///     a socket too long; this one fails toward disconnecting everyone who is logged in, and it
    ///     is the only one that exercises the exemption at all — the deadline is set to zero so
    ///     that being admitted is the sole reason the client survives. Handoff from here belongs to
    ///     the heartbeat reaper, which considers exactly these connections.
    /// </remarks>
    [Fact]
    public void AnAdmittedConnection_IsExemptFromTheDeadline()
    {
        var previous = AdmissionReaperJob.Deadline;
        // Indexes the user by the attached client's connection id, which is what
        // TryGetActiveUserById resolves against.
        var client = HybrasylFixture.AttachTestClient(Fixture.TestUser, out var restore);
        var socket = (TestSocket) client.ClientState.WorkSocket;
        GlobalConnectionManifest.RegisterClient(client);

        try
        {
            Assert.True(Game.World.TryGetActiveUserById(client.ConnectionId, out _),
                "precondition: the connection must resolve to an active user");

            AdmissionReaperJob.Deadline = TimeSpan.Zero;

            AdmissionReaperJob.Execute(null!, null!);

            Assert.False(socket.IsDisconnected,
                "a logged-in player was disconnected by the admission reaper");
        }
        finally
        {
            AdmissionReaperJob.Deadline = previous;
            GlobalConnectionManifest.DeregisterClient(client);
            restore.Dispose();
        }
    }
}
