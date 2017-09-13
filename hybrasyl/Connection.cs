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
        public static long GetNewConnectionId()
        {
            Interlocked.Increment(ref _connectionId);
            return _connectionId;
        }

        public static ConcurrentDictionary<long, Client> ConnectedClients = new ConcurrentDictionary<long, Client>();
        public static ConcurrentDictionary<long, Client> WorldClients = new ConcurrentDictionary<long, Client>();
        public static ConcurrentDictionary<long, Redirect> Redirects = new ConcurrentDictionary<long, Redirect>();

        public static void RegisterRedirect(Client client, Redirect redirect)
        {
            Redirects[client.ConnectionId] = redirect;
        }

        public static bool TryGetRedirect(long cid, out Redirect redirect)
        {
            return Redirects.TryGetValue(cid, out redirect);
        }

        public static void RegisterClient(Client client)
        {
            ConnectedClients[client.ConnectionId] = client;
            if (client.ServerType == ServerTypes.World)
                WorldClients[client.ConnectionId] = client;
        }

        public static void DeregisterClient(Client client)
        {
            ((IDictionary) ConnectedClients).Remove(client.ConnectionId);
                Logger.InfoFormat("Deregistering {0}", client.ConnectionId);
            // Send a control message to clean up after World users; Lobby and Login handle themselves
            if (client.ServerType == ServerTypes.World)
            {
                ((IDictionary)WorldClients).Remove(client.ConnectionId);
                // This will also handle removing the user from WorldClients if necessary
                World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.CleanupUser, client.ConnectionId));
            }
        }
    }

    public class HybrasylMessage
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Int64 Ticks { get; private set; }
        // Maybe this can be like, idk, function name or something? Thread context? Whatever?
        public string Sender { get; private set; }
        public object[] Arguments { get; private set; }

        public HybrasylMessage(string sender = "HybrasylMessage", params object[] parameters)
        {
            Ticks = DateTime.Now.Ticks;
            Sender = sender;
            Arguments = parameters;
        }
    }

    public class HybrasylClientMessage : HybrasylMessage
    {
        public ClientPacket Packet { get; private set; }
        public long ConnectionId { get; private set; }

        public HybrasylClientMessage(ClientPacket packet, long connectionId, params object[] arguments) : 
            base("HybrasylClientMessage", arguments)
        {
            Packet = packet;
            ConnectionId = connectionId;
        }
    }

    public class HybrasylControlMessage : HybrasylMessage
    {
        public int Opcode;
 
        public HybrasylControlMessage(int opcode, params object[] parameters) 
            : base("HybrasylControlMessage", parameters)
        {
            Opcode = opcode;
        }
    }

}

    
