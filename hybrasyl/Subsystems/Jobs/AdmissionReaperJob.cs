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

using Hybrasyl.Internals.Logging;
using Hybrasyl.Networking;
using System;
using System.Timers;

namespace Hybrasyl.Subsystems.Jobs;

/// <summary>
///     Hangs up connections that have never been admitted to the world.
/// </summary>
/// <remarks>
///     <para>
///         Every accepted socket allocates a 65,600-byte receive buffer and is registered globally
///         before any protocol selection or authentication, and the outbound poll walks every
///         registered client every 50 ms. Nothing else reaps them: the heartbeat reaper considers
///         only world clients that already resolve to a user, so a lobby connection, a login
///         connection, and a world connection that never joined would be kept for the life of the
///         process.
///     </para>
///     <para>
///         Admission is "this connection resolves to an active user", which is what the heartbeat
///         reaper takes over from here. Everything else ages out, including a client sitting at the
///         login prompt — which is why <see cref="Deadline" /> is set well past any plausible human
///         pause rather than at the few seconds the handoff actually needs.
///     </para>
/// </remarks>
public static class AdmissionReaperJob
{
    /// <summary>Seconds between sweeps; <c>World.StartTimers</c> reads this by reflection.</summary>
    public static readonly int Interval = 60;

    /// <summary>
    ///     How long a connection may go unadmitted. Deliberately generous: the only connection that
    ///     legitimately sits here is one whose player is reading the login screen, and disconnecting
    ///     them is worse than holding the socket. Internal-settable for tests.
    /// </summary>
    internal static TimeSpan Deadline { get; set; } = TimeSpan.FromMinutes(15);

    public static void Execute(object obj, ElapsedEventArgs args)
    {
        GameLog.Debug("Job starting");
        try
        {
            var now = DateTime.Now.Ticks;

            foreach (var (connectionId, client) in GlobalConnectionManifest.ConnectedClients)
            {
                if (new TimeSpan(now - client.ConnectedSince) <= Deadline) continue;
                if (Game.World.TryGetActiveUserById(connectionId, out _)) continue;

                GameLog.Info("cid {ConnectionId}: never admitted, disconnecting", connectionId);
                client.Disconnect();
            }
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error(e, "Exception occurred in job");
        }
    }
}
