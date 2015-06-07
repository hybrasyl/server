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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using Hybrasyl.Objects;
using log4net;
using System;
using System.Timers;

namespace Hybrasyl
{
    namespace Jobs
    {
        public static class CheckpointerJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            public static readonly int Interval = 300;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                try
                {
                    Logger.Debug("Job starting");
                    foreach (var client in GlobalConnectionManifest.WorldClients)
                    {
                        World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.SaveUser, client.Key));
                    }
                    Logger.Debug("Job complete");
                }
                catch (Exception e)
                {
                    Logger.Error("Exception occured in job:", e);
                }
            }
        }

        public static class IdleDetectionJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            public static readonly int Interval = 60;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                try
                {
                    Logger.Debug("Job starting");
                    foreach (var client in GlobalConnectionManifest.WorldClients.Values)
                    {
                        client.CheckIdle();
                    }
                    Logger.Debug("Job complete");
                }
                catch (Exception e)
                {
                    Logger.Error("Exception occured in job:", e);
                }
            }
        }

        public static class ByteHeartbeatJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            public static readonly int Interval = Constants.BYTE_HEARTBEAT_INTERVAL;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                try
                {
                    Logger.Debug("Job starting");
                    foreach (var client in GlobalConnectionManifest.WorldClients.Values)
                    {
                        client.SendByteHeartbeat();
                    }
                    Logger.Debug("Job complete");
                }
                catch (Exception e)
                {
                    Logger.Error("Exception occured in job:", e);
                }
            }
        }

        public static class TickHeartbeatJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            public static readonly int Interval = Constants.TICK_HEARTBEAT_INTERVAL;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                try
                {
                    Logger.Debug("Job starting");
                    foreach (var client in GlobalConnectionManifest.WorldClients.Values)
                    {
                        client.SendTickHeartbeat();
                    }
                    Logger.Debug("Job complete");
                }
                catch (Exception e)
                {
                    Logger.Error("Exception occured in job:", e);
                }
            }
        }

        /// <summary>
        /// This job reaps connections that haven't responded to either tick or byte based heartbeats
        /// in REAP_HEARTBEAT_TIME.
        /// </summary>
        public static class HeartbeatReaperJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            public static readonly int Interval = Constants.REAP_HEARTBEAT_INTERVAL;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                Logger.Debug("Job starting");
                try
                {
                    foreach (var connection in GlobalConnectionManifest.WorldClients)
                    {
                        var client = connection.Value;
                        var connectionId = connection.Key;
                        User user;
                        if (World.ActiveUsers.TryGetValue(connectionId, out user))
                        {
                            if (client.IsHeartbeatExpired())
                            {
                                Logger.InfoFormat("{0} (connection id {1}: heartbeat expired, disconnecting",
                                    user.Name, connectionId);
                                GlobalConnectionManifest.DeregisterClient(client);
                                World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.CleanupUser, connectionId));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Exception occurred in job:", e);
                }
            }
        }

        public static class RegenerationJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            public static readonly int Interval = 25;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                foreach (var connId in GlobalConnectionManifest.WorldClients.Keys)
                {
                    World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.RegenUser, connId));
                }
            }
        }
        public static class AutoSnoreJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            public static readonly int Interval = 10;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                Logger.Debug("Job starting");
                try
                {
                    foreach (var connection in GlobalConnectionManifest.WorldClients)
                    {
                        var client = connection.Value;
                        var connectionId = connection.Key;

                        if (client.IsIdle())
                        {
                            User user;
                            if (World.ActiveUsers.TryGetValue(connectionId, out user))
                            {
                                user.Motion(16, 120);
                            }
                            else
                            {
                                Logger.WarnFormat(
                                    "Connection id {0} marked as idle but no corresponding user found...?",
                                    connectionId);
                            }
                        }
                    }

                    Logger.Debug("Job complete");
                }
                catch (Exception e)
                {
                    Logger.Error("Exception occured in job:", e);
                }
            }
        }
    }
}
