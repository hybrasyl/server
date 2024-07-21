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
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;

namespace Hybrasyl.Networking.Throttling;

/// <summary>
///     An abstract class for Throttles.
/// </summary>
public abstract class Throttle : IThrottle
{
    protected Throttle(int interval, int duration, int disconnectthreshold)
    {
        Interval = interval;
        ThrottleDuration = duration;
        ThrottleDisconnectThreshold = disconnectthreshold;
        SupportSquelch = false;
    }

    protected Throttle(int interval, int duration, int squelchcount, int squelchinterval, int squelchduration,
        int disconnectthreshold)
    {
        Interval = interval;
        ThrottleDuration = duration;
        SquelchCount = squelchcount;
        SquelchInterval = squelchinterval;
        SquelchDuration = squelchduration;
        ThrottleDisconnectThreshold = disconnectthreshold;
        SupportSquelch = true;
    }

    public int Interval { get; set; }
    public int SquelchCount { get; set; }
    public int SquelchInterval { get; set; }
    public int SquelchDuration { get; set; }
    public int ThrottleDisconnectThreshold { get; set; }
    public int ThrottleDuration { get; set; }
    public bool SupportSquelch { get; set; }

    public abstract ThrottleResult ProcessThrottle(IThrottleData throttleObject);

    public void OnThrottleStart(IThrottleTrigger trigger)
    {
        GameLog.Debug($"Client {trigger.Id}: throttled");
    }

    public void OnThrottleStop(IThrottleTrigger trigger)
    {
        GameLog.Debug($"Client {trigger.Id}: throttle expired");
    }

    public void OnSquelchStart(IThrottleTrigger trigger)
    {
        GameLog.Debug($"Client {trigger.Id}: squelched");
    }

    public void OnSquelchStop(IThrottleTrigger trigger)
    {
        GameLog.Debug($"Client {trigger.Id}: squelch expired");
    }

    public void OnDisconnectThreshold(IThrottleTrigger trigger) { }
}