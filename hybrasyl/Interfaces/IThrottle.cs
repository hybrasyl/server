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

namespace Hybrasyl.Networking.Throttling;

public interface IThrottle
{
    /// <summary>
    ///     Whether or not a throttle supports squelching.
    /// </summary>

    bool SupportSquelch { get; set; }

    /// <summary>
    ///     Process a given packet to check throttling / squelching.
    /// </summary>
    /// <param name="packet"></param>
    /// <returns>ThrottleResult indicating the result of processing.</returns>
    ThrottleResult ProcessThrottle(IThrottleData throttleData);

    /// <summary>
    ///     A function that runs when a throttle starts.
    /// </summary>
    /// <param name="throttledClient"></param>
    void OnThrottleStart(IThrottleTrigger trigger);

    /// <summary>
    ///     A function that runs when a throttle stops (ends).
    /// </summary>
    /// <param name="throttledClient"></param>
    void OnThrottleStop(IThrottleTrigger trigger);

    /// <summary>
    ///     A function that runs when a squelch begins.
    /// </summary>
    /// <param name="squelchedClient"></param>
    void OnSquelchStart(IThrottleTrigger trigger);

    /// <summary>
    ///     A function that runs when a squelch stops (ends).
    /// </summary>
    /// <param name="squelchedClient"></param>
    void OnSquelchStop(IThrottleTrigger trigger);

    /// <summary>
    ///     A function that runs when a throttle's disconnect threshold (number of throttled packets)
    ///     is exceeded.
    /// </summary>
    /// <param name="squelchedClient"></param>
    void OnDisconnectThreshold(IThrottleTrigger trigger);
}