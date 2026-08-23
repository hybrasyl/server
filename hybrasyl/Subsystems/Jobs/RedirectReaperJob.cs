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
///     Drops redirects that were minted and never consumed.
/// </summary>
/// <remarks>
///     Both tables otherwise shrink only on the happy path: a server's <c>ExpectedConnections</c>
///     entry is removed by the join that uses it, and the global <c>Redirects</c> entry by nothing
///     at all — so a login never followed by a join leaves a credential in memory unbounded in both
///     lifetime and count. Sweeping is the only safe place to do this: the global entry is
///     registered immediately before the client is deregistered on logoff, so cleaning up at
///     deregistration would delete the redirect that had just been created.
/// </remarks>
public static class RedirectReaperJob
{
    /// <summary>Seconds between sweeps; <c>World.StartTimers</c> reads this by reflection.</summary>
    public static readonly int Interval = 60;

    public static void Execute(object obj, ElapsedEventArgs args)
    {
        GameLog.Debug("Job starting");
        try
        {
            var swept = 0;

            foreach (var (connectionId, redirect) in GlobalConnectionManifest.Redirects)
                if (redirect.Expired && GlobalConnectionManifest.Redirects.TryRemove(connectionId, out _))
                    swept++;

            foreach (var server in Game.RegisteredServers)
            foreach (var (id, redirect) in server.ExpectedConnections)
                if (redirect.Expired && server.ExpectedConnections.TryRemove(id, out _))
                    swept++;

            if (swept > 0)
                GameLog.Info("Reaped {Count} expired redirect(s)", swept);
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error(e, "Exception occurred in job");
        }
    }
}
