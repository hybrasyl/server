/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Objects;
using System;
using System.Timers;

namespace Hybrasyl.Jobs
{
    public static class MetricsJob
    {
        public static readonly int Interval = 20;

        public static void Execute(object obj, ElapsedEventArgs args)
        {
            if (Game.MetricsStore.Options.ReportingEnabled)
            {
                // Store queue depth before we run our report
                Game.MetricsStore.Measure.Gauge.SetValue(HybrasylMetricsRegistry.QueueDepth, 
                   World.MessageQueue.Count);
                Game.MetricsStore.Measure.Gauge.SetValue(HybrasylMetricsRegistry.ControlQueueDepth,
                   World.MessageQueue.Count);

                // this shouldn't be how this happens, but it doesn't work otherwise
                foreach (var a in Game.MetricsStore.ReportRunner.RunAllAsync())
                {
                    a.Wait();
                }
            }
        }
    }
}
