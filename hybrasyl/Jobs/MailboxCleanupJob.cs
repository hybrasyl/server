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

using Hybrasyl.Messaging;
using System;
using System.Linq;
using System.Timers;

namespace Hybrasyl.Jobs;

public static class MailboxCleanupJob
{
    // Clean up mailboxes once an hour
    public static readonly int Interval = 3600;

    public static void Execute(object obj, ElapsedEventArgs args)
    {
        try
        {
            GameLog.Debug("Job starting");

            var now = DateTime.Now.Ticks;
            foreach (var mailbox in Game.World.WorldState.Values<Mailbox>().Where(predicate: mb => mb.Full))
                try
                {
                    mailbox.Cleanup();
                }
                catch (MessageStoreLocked e)
                {
                    Game.ReportException(e);
                    GameLog.ErrorFormat("{0}: mailbox locked during cleanup...?", mailbox.Name);
                }

            foreach (var board in Game.World.WorldState.Values<Board>().Where(predicate: mb => mb.Full))
                try
                {
                    board.Cleanup();
                }
                catch (MessageStoreLocked e)
                {
                    Game.ReportException(e);
                    GameLog.ErrorFormat("{0}: board locked during cleanup...?", board.Name);
                }

            GameLog.Debug("Job complete");
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error("Exception occured in job:", e);
        }
    }
}