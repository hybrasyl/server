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
using System.Timers;
using Hybrasyl.Internals.Logging;

namespace Hybrasyl.Subsystems.Jobs;

public static class TimeOfDayJob
{
    public static readonly int Interval = 900;

    public static void Execute(object obj, ElapsedEventArgs args)
    {
        GameLog.Debug("Time of day job starting");
        try
        {
            foreach (var user in Game.World.ActiveUsers)
            {
                if (user.Location.Map is { DynamicLighting: true })
                {
                    user.SendLightLevel();
                }
            }
            GameLog.Debug("Job complete");
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error(e, "Exception occurred in job");
        }
    }
}
