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

        public static ConcurrentDictionary<IntPtr, Client> ConnectedClients = new ConcurrentDictionary<IntPtr, Client>();
        public static ConcurrentDictionary<IntPtr, Client> WorldClients = new ConcurrentDictionary<IntPtr, Client>();


        public static void RegisterClient(Client client)
        {
            Logger.DebugFormat("RegisterConnection: {0}", client.ConnectionId);
            ConnectedClients[client.ConnectionId] = client;
            if (client.ServerType == ServerTypes.World)
                WorldClients[client.ConnectionId] = client;
        }

        public static void DeregisterClient(Client client)
        {
            ((IDictionary)ConnectedClients).Remove(client.ConnectionId);
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
        public String Sender { get; private set; }
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
        public IntPtr ConnectionId { get; private set; }

        public HybrasylClientMessage(ClientPacket packet, IntPtr connectionId, params object[] arguments) : 
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

    
