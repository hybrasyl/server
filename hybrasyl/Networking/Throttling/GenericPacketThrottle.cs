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

using System;
using System.Threading;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;

namespace Hybrasyl.Networking.Throttling;

/// <summary>
///     A generic throttler that can be used for any packet. It implements a simple throttle with no
///     squelching.
/// </summary>
public class GenericPacketThrottle : Throttle, IPacketThrottle
{
    /// <summary>
    ///     Constructor for a throttle without squelch.
    /// </summary>
    /// <param name="opcode">The packet opcode to be inspected.</param>
    /// <param name="interval">The minimum acceptable interval between accepted packets, in milliseconds. </param>
    /// <param name="duration">The duration of this throttle, once applied.</param>
    /// <param name="disconnectthreshold">
    ///     Maximum number of packets that can be sent during a throttle before a client is
    ///     forcibly disconnected.
    /// </param>
    public GenericPacketThrottle(byte opcode, int interval, int duration, int disconnectthreshold) : base(interval,
        duration, disconnectthreshold)
    {
        Opcode = opcode;
    }

    /// <summary>
    ///     Constructor for a throttle with squelch.
    /// </summary>
    /// <param name="opcode">The packet opcode to be inspected.</param>
    /// <param name="interval">The minimum acceptable interval between accepted packets, in milliseconds. </param>
    /// <param name="duration">The duration of this throttle, once applied.</param>
    /// <param name="disconnectthreshold">
    ///     Maximum number of packets that can be sent during a throttle before a client is
    ///     forcibly disconnected.
    /// </param>
    /// <param name="squelchcount">Number of times the same object can be seen in a specified interval.</param>
    /// <param name="squelchinterval">The time window to consider for squelching.</param>
    /// <param name="squelchduration">The duration of a squelch, once applied.</param>
    public GenericPacketThrottle(byte opcode, int interval, int duration, int squelchcount, int squelchinterval,
        int squelchduration, int disconnectthreshold) :
        base(interval, duration, squelchcount, squelchinterval, squelchduration, disconnectthreshold)
    {
        Opcode = opcode;
    }

    public byte Opcode { get; }

    public ThrottleResult ProcessThrottle(IPacketThrottleData throttleData) =>
        ProcessThrottle(throttleData.Client, throttleData.Packet);

    public override ThrottleResult ProcessThrottle(IThrottleData throttleData)
    {
        var packetData = throttleData as IPacketThrottleData;
        if (packetData == null) return ThrottleResult.Error;
        return ProcessThrottle(packetData);
    }

    public ThrottleResult ProcessThrottle(Client client, ClientPacket packet)
    {
        ThrottleInfo info;
        var result = ThrottleResult.Error;

        if (!client.ThrottleState.TryGetValue(packet.Opcode, out info))
        {
            client.ThrottleState[packet.Opcode] = new ThrottleInfo();
            info = client.ThrottleState[packet.Opcode];
        }

        Monitor.Enter(info);

        try
        {
            var rightnow = DateTime.Now;
            //GameLog.Warning($"Right now is {rightnow}");
            var transmitInterval = rightnow - info.LastReceived;
            var acceptedInterval = rightnow - info.LastAccepted;
            info.PreviousReceived = info.LastReceived;
            info.LastReceived = rightnow;
            info.TotalReceived++;
            GameLog.Debug(
                $"Begin: PA: {info.PreviousAccepted} LA: {info.LastAccepted} AInterval is {acceptedInterval.TotalMilliseconds} TInterval {transmitInterval.TotalMilliseconds}");

            if (info.Throttled)
            {
                result = ThrottleResult.Throttled;
                if (acceptedInterval.TotalMilliseconds >= ThrottleDuration &&
                    acceptedInterval.TotalMilliseconds >= Interval)
                {
                    //GameLog.Error($"Unthrottled: {acceptedInterval.TotalMilliseconds} > {ThrottleDuration} and {Interval}");
                    info.Throttled = false;
                    info.TotalThrottled = 0;
                    result = ThrottleResult.ThrottleEnd;
                    info.PreviousAccepted = info.LastAccepted;
                    info.LastAccepted = rightnow;
                    OnThrottleStop(new ClientTrigger(client));
                }
                else
                {
                    info.TotalThrottled++;
                    //GameLog.Error($"Throttled, count is {info.TotalThrottled}");

                    result = ThrottleResult.Throttled;
                    if (ThrottleDisconnectThreshold > 0 && info.TotalThrottled > ThrottleDisconnectThreshold)
                    {
                        result = ThrottleResult.Disconnect;
                        OnDisconnectThreshold(new ClientTrigger(client));
                    }
                }
            }
            else
            {
                if (acceptedInterval.TotalMilliseconds <= Interval && info.LastAccepted != DateTime.MinValue)
                {
                    GameLog.Debug(
                        $"TInterval {transmitInterval}, AInterval {acceptedInterval} - maximum is {Interval}, throttled");
                    info.Throttled = true;
                    OnThrottleStart(new ClientTrigger(client));
                    result = ThrottleResult.Throttled;
                }
                else
                {
                    info.PreviousAccepted = info.LastAccepted;
                    info.LastAccepted = rightnow;
                    info.TotalAccepted++;
                    result = ThrottleResult.OK;
                    GameLog.Debug($"Packet accepted, PA: {info.PreviousAccepted} LA: {info.LastAccepted}");
                }
            }
        }
        finally
        {
            Monitor.Exit(info);
        }

        return result;
    }

    public void OnDisconnectThreshold(IClientTrigger trigger)
    {
        trigger.Client.SendMessage("You have been automatically disconnected due to server abuse. Goodbye!",
            MessageTypes.SYSTEM_WITH_OVERHEAD);
        GameLog.Warning($"Client {trigger.Id}: disconnected due to packet spam");
        trigger.Client.Disconnect();
    }
}