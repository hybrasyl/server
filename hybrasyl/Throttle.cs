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
 
using System;
using System.Threading;
using Hybrasyl.Enums;

namespace Hybrasyl
{

    public interface IThrottleData
    {

    }

    public interface IPacketThrottleData : IThrottleData
    {
        Client Client { get; }
        ClientPacket Packet { get; }
        byte Opcode { get; }
    }

    public class PacketThrottleData : IPacketThrottleData
    {
        public Client Client { get; set; }
        public ClientPacket Packet { get; set; }
        public byte Opcode => Packet.Opcode;

        public PacketThrottleData(Client client, ClientPacket packet)
        {
            Client = client;
            Packet = packet;
        }
    }

    public interface IThrottle
    {
        /// <summary>
        /// Whether or not a throttle supports squelching.
        /// </summary>

        bool SupportSquelch { get; set; }

        /// <summary>
        /// Process a given packet to check throttling / squelching.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns>ThrottleResult indicating the result of processing.</returns>
        ThrottleResult ProcessThrottle(IThrottleData throttleData);
        /// <summary>
        /// A function that runs when a throttle starts.
        /// </summary>
        /// <param name="throttledClient"></param>
        void OnThrottleStart(IThrottleTrigger trigger);
        /// <summary>
        /// A function that runs when a throttle stops (ends).
        /// </summary>
        /// <param name="throttledClient"></param>
        void OnThrottleStop(IThrottleTrigger trigger);
        /// <summary>
        /// A function that runs when a squelch begins.
        /// </summary>
        /// <param name="squelchedClient"></param>
        void OnSquelchStart(IThrottleTrigger trigger);
        /// <summary>
        /// A function that runs when a squelch stops (ends).
        /// </summary>
        /// <param name="squelchedClient"></param>
        void OnSquelchStop(IThrottleTrigger trigger);
        /// <summary>
        /// A function that runs when a throttle's disconnect threshold (number of throttled packets)
        /// is exceeded.
        /// </summary>
        /// <param name="squelchedClient"></param>
        void OnDisconnectThreshold(IThrottleTrigger trigger);
    }

    public interface IPacketThrottle
    {
        ThrottleResult ProcessThrottle(IPacketThrottleData throttleData);
        byte Opcode { get; }
    }

    public interface IThrottleTrigger
    {
        Int64 Id { get; }
    }

    public interface IClientTrigger : IThrottleTrigger
    {
        Client Client { get; }
    }

    public class ClientTrigger : IClientTrigger
    {
        public Client Client { get; private set; }
        public Int64 Id => Client.ConnectionId;

        public ClientTrigger(Client client)
        {
            Client = client;
        }
    }

    /// <summary>
    /// An abstract class for Throttles.
    /// </summary>
    public abstract class Throttle : IThrottle
    {
        public int Interval { get; set; }
        public int SquelchCount { get; set; }    
        public int SquelchInterval { get; set; }
        public int SquelchDuration { get; set; }
        public int ThrottleDisconnectThreshold { get; set; }
        public bool SupportSquelch { get; set; }
        public int ThrottleDuration { get; set; }

        protected Throttle(int interval, int duration, int disconnectthreshold)
        {
            Interval = interval;
            ThrottleDuration = duration;
            ThrottleDisconnectThreshold = disconnectthreshold;
            SupportSquelch = false;
        }

        protected Throttle(int interval, int duration, int squelchcount, int squelchinterval, int squelchduration, int disconnectthreshold)
        {
            Interval = interval;
            ThrottleDuration = duration;
            SquelchCount = squelchcount;
            SquelchInterval = squelchinterval;
            SquelchDuration = squelchduration;
            ThrottleDisconnectThreshold = disconnectthreshold;
            SupportSquelch = true;
        }

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

        public void OnDisconnectThreshold(IThrottleTrigger trigger)
        {         
        }
    }


    /// <summary>
    /// A generic throttler that can be used for any packet. It implements a simple throttle with no
    /// squelching.
    /// </summary>
    public class GenericPacketThrottle : Throttle, IPacketThrottle
    {

        public byte Opcode { get; private set; }
        /// <summary>
        /// Constructor for a throttle without squelch.
        /// </summary>
        /// <param name="opcode">The packet opcode to be inspected.</param>
        /// <param name="interval">The minimum acceptable interval between accepted packets, in milliseconds. </param>
        /// <param name="duration">The duration of this throttle, once applied.</param>
        /// <param name="disconnectthreshold">Maximum number of packets that can be sent during a throttle before a client is forcibly disconnected.</param>
        public GenericPacketThrottle(byte opcode, int interval, int duration, int disconnectthreshold) : base(interval, duration, disconnectthreshold)
        {
            Opcode = opcode;
        }
        /// <summary>
        /// Constructor for a throttle with squelch.
        /// </summary>
        /// <param name="opcode">The packet opcode to be inspected.</param>
        /// <param name="interval">The minimum acceptable interval between accepted packets, in milliseconds. </param>
        /// <param name="duration">The duration of this throttle, once applied.</param>
        /// <param name="disconnectthreshold">Maximum number of packets that can be sent during a throttle before a client is forcibly disconnected.</param>
        /// <param name="squelchcount">Number of times the same object can be seen in a specified interval.</param>
        /// <param name="squelchinterval">The time window to consider for squelching.</param>
        /// <param name="squelchduration">The duration of a squelch, once applied.</param>
        public GenericPacketThrottle(byte opcode, int interval, int duration, int squelchcount, int squelchinterval, int squelchduration, int disconnectthreshold) :
            base(interval, duration, squelchcount, squelchinterval, squelchduration, disconnectthreshold) {
            Opcode = opcode;
        }

        public override ThrottleResult ProcessThrottle(IThrottleData throttleData)
        {
            var packetData = throttleData as IPacketThrottleData;
            if (packetData == null) { return ThrottleResult.Error; }
            return ProcessThrottle(packetData);
        }
        public ThrottleResult ProcessThrottle(IPacketThrottleData throttleData)
        {
            return ProcessThrottle(throttleData.Client, throttleData.Packet);
        }

        public ThrottleResult ProcessThrottle(Client client, ClientPacket packet)
        {
            ThrottleInfo info;
            ThrottleResult result = ThrottleResult.Error;

            if (!client.ThrottleState.TryGetValue(packet.Opcode, out info))
            {
                client.ThrottleState[packet.Opcode] = new ThrottleInfo();
                info = client.ThrottleState[packet.Opcode];
            }

            Monitor.Enter(info);

            try
            {
                DateTime rightnow = DateTime.Now;
                GameLog.Warning($"Right now is {rightnow}");
                var transmitInterval = (rightnow - info.LastReceived);
                var acceptedInterval = (rightnow - info.LastAccepted);
                info.PreviousReceived = info.LastReceived;
                info.LastReceived = rightnow;
                info.TotalReceived++;
                GameLog.Debug($"Begin: PA: {info.PreviousAccepted} LA: {info.LastAccepted} AInterval is {acceptedInterval.TotalMilliseconds} TInterval {transmitInterval.TotalMilliseconds}");

                if (info.Throttled)
                {
                    result = ThrottleResult.Throttled;
                    if (acceptedInterval.TotalMilliseconds >= ThrottleDuration && acceptedInterval.TotalMilliseconds >= Interval)
                    {
                        GameLog.Error($"Unthrottled: {acceptedInterval.TotalMilliseconds} > {ThrottleDuration} and {Interval}");
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
                        GameLog.Error($"Throttled, count is {info.TotalThrottled}");

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
                        GameLog.Debug($"TInterval {transmitInterval}, AInterval {acceptedInterval} - maximum is {Interval}, throttled");
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
            trigger.Client.SendMessage("You have been automatically disconnected due to server abuse. Goodbye!", MessageTypes.SYSTEM_WITH_OVERHEAD);
            GameLog.Warning($"Client {trigger.Id}: disconnected due to packet spam");
            trigger.Client.Disconnect();

        }

    }

    // TODO: add other types of throttles

    public class ThrottleInfo
    {
        public DateTime PreviousReceived;
        public DateTime LastReceived;

        public DateTime PreviousAccepted;
        public DateTime LastAccepted;

        public Int64 TotalReceived;
        public Int64 TotalAccepted;
        public Int64 TotalSquelched;
        public Int64 TotalThrottled;
        public Int64 SquelchCount;

        public string SquelchObject { get; set; }

        public bool Squelched { get; set; }
        public bool Throttled { get; set; }

        public ThrottleInfo()
        {
            PreviousReceived = DateTime.MinValue;
            LastReceived = DateTime.MinValue;
            PreviousAccepted = DateTime.MinValue;
            LastAccepted = DateTime.MinValue;
            TotalReceived = 0;
            TotalSquelched = 0;
            TotalThrottled = 0;
            SquelchCount = 0;
            Throttled = false;
            Squelched = false;
        }

    }

}
