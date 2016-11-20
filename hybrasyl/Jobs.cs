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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *
 */

using Hybrasyl.Objects;
using log4net;
using System;

using System.Linq;

using System.Timers;

namespace Hybrasyl
{
    namespace Jobs
    {
        // Jobs (each one is a class) to be scheduled at startup by Hybrasy's timers go here.
        // NOTE: ONLY JOB CLASSES GO HERE.
        //
        // Each class needs an Interval (which represents how often it should run, in seconds)
        // and a void Execute() which will do the work. If you want logging (and you should)
        // you need the following as well:
        //
        //        public static readonly ILog Logger =
        //       LogManager.GetLogger(
        //       System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //
        // Please note that a static logger is the ONLY way to guarantee threadsafe logging.
        //
        // Jobs run in their own thread, so there are some important considerations.
        //
        // 1) Never modify game state or call game logic directly. That's what message passing is for!
        //    Reading state is OK, unless you're using it for something critical (player state)
        //    in which case consistency (obviously) isn't guaranteed.
        //    Example: a job that, say, occasionally reported the number of logged in players to
        //    somewhere else via an external API call: probably fine. A job that checked to see if
        //    a player had a certain item and then took action based on that - BAD. The only exception
        //    to this is if the object implements some kind of locking (see Mailbox for an example).
        //
        // 2) You can send packets to clients from a job (since doing so is intended to be
        //    thread safe). An instance of how to use this is in the AutoSnoreJob, where we can
        //    send snore packets to idle clients without being a bother to anything else. Since this
        //    is effectively a threadsafe operation that doesn't change game state or logic - this
        //    is fine.
        //
        // The best way to use a job is to write a packet handler for a control message, and have
        // the job submit a control message. CheckpointerJob is a great example of this.
        //

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
                        // Insert a "save client" message onto the queue for each client.
                        // We do this rather than sending a "checkpoint" message so we don't
                        // randomly have a packet occupying shitloads of CPU time blocking
                        // everything else.

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

        public static class MailboxCleanupJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            // Clean up mailboxes once an hour
            public static readonly int Interval = 3600;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                try
                {
                    Logger.Debug("Job starting");

                    var now = DateTime.Now.Ticks;
                    foreach (var mailbox in Game.World.Mailboxes.Values.Where(mb => mb.Full))
                    {
                        try
                        {
                            mailbox.Lock();
                            mailbox.Cleanup();
                            mailbox.Unlock();
                        }
                        catch (MessageStoreLocked)
                        {
                            Logger.ErrorFormat("{0}: mailbox locked during cleanup...?", mailbox.Name);
                        }
                    }
                    foreach (var board in Game.World.Messageboards.Values.Where(mb => mb.Full))
                    {
                        try
                        {
                            board.Lock();
                            board.Cleanup();
                            board.Unlock();
                        }
                        catch (MessageStoreLocked)
                        {
                            Logger.ErrorFormat("{0}: board locked during cleanup...?", board.Name);
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

                    var now = DateTime.Now.Ticks;
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

                    var rnd = new Random();
                    foreach (var client in GlobalConnectionManifest.WorldClients.Values)
                    {
                        // Send the 0x3B heartbeat to logged in clients
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

                    var rnd = new Random();
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

        public static class StatusTickJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            public static readonly int Interval = 1;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                Logger.Debug("Status tick job starting");
                foreach (var connectionId in GlobalConnectionManifest.WorldClients.Keys)
                {
                    User user;
                    if (World.ActiveUsers.TryGetValue(connectionId, out user))
                    {
                        World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.StatusTick, user.Name));
                    }
                }
                Logger.Debug("Status tick job ending");

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
                    // FIXME: make this more efficient / don't break our own conventions
                    foreach (var connection in GlobalConnectionManifest.WorldClients)
                    {
                        var client = connection.Value;
                        var connectionId = connection.Key;

                        if (client.IsIdle())
                        {
                            User user;
                            if (World.ActiveUsers.TryGetValue(connectionId, out user))
                            {
                                user.Motion(16, 120); // send snore effect
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

        public static class MonsterSpawnJob
        {
            public static readonly ILog Logger =
                LogManager.GetLogger(
                    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            public static readonly int Interval = 20;

            public static void Execute(Object obj, ElapsedEventArgs args)
            {
                Logger.Debug("Job starting");
                try
                {
                    // FIXME: make this more efficient / don't break our own conventions
                    foreach (var monolith in Game.World.Monoliths)
                    {
                        monolith.Spawn();
                        Logger.InfoFormat("Attempting to spawn monsters.", monolith.MaxSpawns);
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
