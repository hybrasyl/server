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
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using log4net;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Hybrasyl
{
    public delegate void LobbyPacketHandler(Client client, ClientPacket packet);
    public delegate void LoginPacketHandler(Client client, ClientPacket packet);
    public delegate void WorldPacketHandler(Object obj, ClientPacket packet);
    public delegate void ControlMessageHandler(HybrasylControlMessage message);

    public class Server
    {
        public static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public int Port { get; private set; }
        public Socket Socket { get; private set; }
       // public TcpListener TcpListener { get; private set; }
        public WorldPacketHandler[] PacketHandlers { get; private set; }
        public ControlMessageHandler[] ControlMessageHandlers { get; private set; }
        public ConcurrentDictionary<uint, Redirect> ExpectedConnections { get; private set; }
        public bool Active { get; private set; }

        public ConcurrentDictionary<IntPtr, Client> Clients;


        public Server(int port)
        {
            Clients = new ConcurrentDictionary<IntPtr, Client>();
            Port = port;
            PacketHandlers = new WorldPacketHandler[256];
            ControlMessageHandlers = new ControlMessageHandler[64];
            ExpectedConnections = new ConcurrentDictionary<uint, Redirect>();

            for (int i = 0; i < 256; ++i)
                PacketHandlers[i] = (c, p) => Logger.WarnFormat("World: Unhandled opcode 0x{0:X2}", p.Opcode);
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket.Bind(new IPEndPoint(IPAddress.Any, Port));
            Socket.Listen(10);
            Socket.BeginAccept(new AsyncCallback(AcceptConnection), null);
            //TcpListener = new TcpListener(IPAddress.Any, port);
            Logger.InfoFormat("Starting TcpListener: {0}:{1}", IPAddress.Any.ToString(), port);
            //TcpListener.Start();
        }

        
        public virtual void AcceptConnection(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = Socket.EndAccept(ar);
                Client client = new Client(clientSocket, this);
                Clients.GetOrAdd(clientSocket.Handle, client);
                client.Begin();
                Socket.BeginAccept(new AsyncCallback(AcceptConnection), null);
            }
            catch
            {
            }
        }

        public virtual void Shutdown()
        {
            Logger.WarnFormat("{0}: shutting down", this.GetType().ToString());
            Socket.Close();
            Logger.WarnFormat("{0}: shutdown complete", this.GetType().ToString());
        }
    }

    public class Redirect
    {
        private static uint id = 0;
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public uint Id { get; set; }
        public Client Client { get; set; }
        public Server Source { get; set; }
        public Server Destination { get; set; }
        public string Name { get; set; }
        public byte[] EncryptionKey { get; set; }
        public byte EncryptionSeed { get; set; }

        public Redirect(Client client, Server source, Server destination, string name, byte seed, byte[] key)
        {
            Id = id++;
            Client = client;
            Source = source;
            Destination = destination;
            Name = name;
            EncryptionSeed = seed;
            EncryptionKey = key;
        }

        public bool Matches(string name, byte[] key, byte seed)
        {
            if (key.Length != EncryptionKey.Length || name != Name || seed != EncryptionSeed)
                return false;

            for (int i = 0; i < key.Length; ++i)
            {
                if (key[i] != EncryptionKey[i])
                    return false;
            }

            return true;
        }
    }
}
