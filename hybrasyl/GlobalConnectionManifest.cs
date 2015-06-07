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

using log4net;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;

namespace Hybrasyl
{
    public static class GlobalConnectionManifest
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static long _connectionId = 0;
        public static ConcurrentDictionary<long, Client> ConnectedClients = new ConcurrentDictionary<long, Client>();
        public static ConcurrentDictionary<long, Client> WorldClients = new ConcurrentDictionary<long, Client>();

        public static long GetNewConnectionId()
        {
            Interlocked.Increment(ref _connectionId);
            return _connectionId;
        }

        public static void RegisterClient(Client client)
        {
            Logger.DebugFormat("RegisterConnection: {0}", client.ConnectionId);
            ConnectedClients[client.ConnectionId] = client;
            if (client.ServerType == ServerTypes.World)
            {
                WorldClients[client.ConnectionId] = client;
            }
        }

        public static void DeregisterClient(Client client)
        {
            ((IDictionary)ConnectedClients).Remove(client.ConnectionId);
            if (client.ServerType == ServerTypes.World)
            {
                ((IDictionary)WorldClients).Remove(client.ConnectionId);
                World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.CleanupUser, client.ConnectionId));
            }
        }
    }
}

