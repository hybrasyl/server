using Hybrasyl.Objects;
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Hybrasyl.Enums;

namespace Hybrasyl
{


    public interface IThrottle
    {
        /// <summary>
        /// The opcode which will have this throttle applied.
        /// </summary>
        byte Opcode { get; set; }
        /// <summary>
        /// Minimum interval that must pass between consumption of a given packet (in milliseconds).
        /// </summary>
        int Interval { get; set; }
        /// <summary>
        /// Number of times we need to see an object (text, etc) in a row, during SquelchDuration, 
        /// before it will be squelched (ignored).
        /// </summary>
        int SquelchCount { get; set; }
        /// <summary>
        /// The time window we consider for squelching. For instance, if set to 1000ms, a player would
        /// need to send the same object (text, etc) >= SquelchCount times within this interval.
        /// </summary>
        int SquelchInterval { get; set; }
        /// <summary>
        /// The duration of a squelch once active (in milliseconds).
        /// </summary>
        int SquelchDuration { get; set; }
        /// <summary>
        /// The maximum number of throttled packets received during a throttle before a client
        /// is forcibly disconnected.
        /// </summary>
        int ThrottleDisconnectThreshold { get; set; }
        /// <summary>
        /// The duration of a throttle, in milliseconds.
        /// </summary>
        int ThrottleDuration { get; set; }
        /// <summary>
        /// Whether or not a throttle supports squelching. This is an automatic property.
        /// </summary>
        bool SupportSquelch { get; set; }

        /// <summary>
        /// Process a given packet to check throttling / squelching.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns>ThrottleResult indicating the result of processing.</returns>
        ThrottleResult ProcessPacket(Client throttledClient, ClientPacket packet);
        /// <summary>
        /// A function that runs when a throttle starts.
        /// </summary>
        /// <param name="throttledClient"></param>
        void OnThrottleStart(Client throttledClient);
        /// <summary>
        /// A function that runs when a throttle stops (ends).
        /// </summary>
        /// <param name="throttledClient"></param>
        void OnThrottleStop(Client throttledClient);
        /// <summary>
        /// A function that runs when a squelch begins.
        /// </summary>
        /// <param name="squelchedClient"></param>
        void OnSquelchStart(Client squelchedClient);
        /// <summary>
        /// A function that runs when a squelch stops (ends).
        /// </summary>
        /// <param name="squelchedClient"></param>
        void OnSquelchStop(Client squelchedClient);
        /// <summary>
        /// A function that runs when a throttle's disconnect threshold (number of throttled packets)
        /// is exceeded.
        /// </summary>
        /// <param name="squelchedClient"></param>
        void OnDisconnectThreshold(Client squelchedClient);
    }

    public abstract class Throttle : IThrottle
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public int Interval { get; set; }
        public int SquelchCount { get; set; }    
        public byte Opcode { get; set; }
        public int SquelchInterval { get; set; }
        public int SquelchDuration { get; set; }
        public int ThrottleDisconnectThreshold { get; set; }
        public bool SupportSquelch { get; set; }
        public int ThrottleDuration { get; set; }

        protected Throttle(byte opcode, int interval, int duration, int disconnectthreshold)
        {
            Opcode = opcode;
            Interval = interval;
            ThrottleDuration = duration;
            ThrottleDisconnectThreshold = disconnectthreshold;
            SupportSquelch = false;
        }

        protected Throttle(byte opcode, int interval, int duration, int squelchcount, int squelchinterval, int squelchduration, int disconnectthreshold)
        {
            Opcode = opcode;
            Interval = interval;
            ThrottleDuration = duration;
            SquelchCount = squelchcount;
            SquelchInterval = squelchinterval;
            SquelchDuration = squelchduration;
            ThrottleDisconnectThreshold = disconnectthreshold;
            SupportSquelch = true;
        }

        public abstract ThrottleResult ProcessPacket(Client client, ClientPacket packet);

        public void OnThrottleStart(Client trigger)
        {
            Logger.Debug($"Client {trigger.ConnectionId}: opcode {Opcode} throttled");
        }

        public void OnThrottleStop(Client trigger)
        {
            Logger.Debug($"Client {trigger.ConnectionId}: opcode {Opcode} throttle expired");
        }

        public void OnSquelchStart(Client trigger)
        {
            Logger.Debug($"Client {trigger.ConnectionId}: opcode {Opcode}: object squelched");
        }

        public void OnSquelchStop(Client trigger)
        {
            Logger.Debug($"Client {trigger.ConnectionId}: opcode {Opcode}: squelch expired");
        }

        public void OnDisconnectThreshold(Client trigger)
        {
            trigger.SendMessage("You have been automatically disconnected due to server abuse. Goodbye!", MessageTypes.SYSTEM_WITH_OVERHEAD);
            Logger.Warn($"Client {trigger.ConnectionId}: disconnected due to packet spam");
            trigger.Disconnect();
        }
    }


    /// <summary>
    /// A generic throttler that can be used for any packet. It implements a simple throttle with no
    /// squelching.
    /// </summary>
    public class GenericPacketThrottle : Throttle
    {
        /// <summary>
        /// Constructor for a throttle without squelch.
        /// </summary>
        /// <param name="opcode">The packet opcode to be inspected.</param>
        /// <param name="interval">The minimum acceptable interval between accepted packets, in milliseconds. </param>
        /// <param name="duration">The duration of this throttle, once applied.</param>
        /// <param name="disconnectthreshold">Maximum number of packets that can be sent during a throttle before a client is forcibly disconnected.</param>
        public GenericPacketThrottle(byte opcode, int interval, int duration, int disconnectthreshold) : base(opcode, interval, duration, disconnectthreshold) { }
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
            base(opcode, interval, duration, squelchcount, squelchinterval, squelchduration, disconnectthreshold) { }

        public override ThrottleResult ProcessPacket(Client client, ClientPacket packet)
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
                Int64 rightnow = DateTime.Now.Ticks;
                Int64 transmitInterval = (rightnow - info.LastReceived) / TimeSpan.TicksPerMillisecond;
                Int64 acceptedInterval = (rightnow - info.LastAccepted) / TimeSpan.TicksPerMillisecond;
                info.PreviousReceived = info.LastReceived;
                info.LastReceived = rightnow;
                //Logger.Error($"Interval is {interval} - maximum is {Interval}");
                info.TotalReceived++;

                if (info.Throttled)
                {
                    result = ThrottleResult.Throttled;
                    if (acceptedInterval > ThrottleDuration && acceptedInterval >= Interval)
                    {
                        Logger.Warn($"Transmit interval {transmitInterval}, last accepted {acceptedInterval} - maximum is {Interval}, not throttled");
                        info.Throttled = false;
                        info.TotalThrottled = 0;
                        result = ThrottleResult.ThrottleEnd;
                        info.PreviousAccepted = info.LastAccepted;
                        info.LastAccepted = rightnow;
                        OnThrottleStop(client);
                    }
                    else
                    {
                        info.TotalThrottled++;
                        Logger.Warn($"Throttled, count is {info.TotalThrottled}");

                        result = ThrottleResult.Throttled;
                        if (ThrottleDisconnectThreshold > 0 && info.TotalThrottled > ThrottleDisconnectThreshold)
                        {
                            result = ThrottleResult.Disconnect;
                            OnDisconnectThreshold(client);
                        }
                    }
                }
                else
                {
                    if (acceptedInterval <= Interval)
                    {
                        Logger.Warn($"Transmit interval {transmitInterval}, last accepted {acceptedInterval} - maximum is {Interval}, throttled");
                        info.Throttled = true;
                        OnThrottleStart(client);
                        result = ThrottleResult.Throttled;
                    }
                    else
                    {
                        info.LastAccepted = rightnow;
                        info.TotalAccepted++;
                        result = ThrottleResult.OK;
                    }
                }
            }
            finally
            {
                Monitor.Exit(info);

            }
            return result;
        }
    }

    // TODO: add other types of throttles

    public class ThrottleInfo
    {
        public Int64 PreviousReceived;
        public Int64 LastReceived;

        public Int64 PreviousAccepted;
        public Int64 LastAccepted;

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
            PreviousReceived = 0;
            TotalReceived = 0;
            TotalSquelched = 0;
            TotalThrottled = 0;
            SquelchCount = 0;
        }

    }

}
