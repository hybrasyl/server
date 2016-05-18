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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Policy;
using System.Threading;
using IronPython.Modules;

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
        public Socket Listener { get; private set; }
       // public TcpListener TcpListener { get; private set; }
        public WorldPacketHandler[] PacketHandlers { get; private set; }
        public ControlMessageHandler[] ControlMessageHandlers { get; private set; }
        public ConcurrentDictionary<uint, Redirect> ExpectedConnections { get; private set; }
        public bool Active { get; private set; }

        public static ManualResetEvent AllDone = new ManualResetEvent(false);

        public ConcurrentDictionary<IntPtr, Client> Clients;


        public Server(int port)
        {
            Clients = new ConcurrentDictionary<IntPtr, Client>();
            Port = port;
            PacketHandlers = new WorldPacketHandler[256];
            ControlMessageHandlers = new ControlMessageHandler[64];
            ExpectedConnections = new ConcurrentDictionary<uint, Redirect>();
            for (int i = 0; i < 256; ++i)
                PacketHandlers[i] = (c, p) => Logger.WarnFormat("Server: Unhandled opcode 0x{0:X2}", p.Opcode);

        }

        public void StartListening()
        {
            Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            Listener.Listen(100);
            Logger.InfoFormat("Starting TcpListener: {0}:{1}", IPAddress.Any.ToString(), Port);
            while (true)
            {
                AllDone.Reset();
                Listener.BeginAccept(new AsyncCallback(AcceptConnection), Listener);
                AllDone.WaitOne();
            }
        }


        public virtual void AcceptConnection(IAsyncResult ar)
        {
            // TODO: @norrismiv async callbacks+inheritance? and/or can these callbacks suck less?
            AllDone.Set();
            Socket clientSocket = (Socket) ar.AsyncState;
            Socket handler = clientSocket.EndAccept(ar);
            Client client = new Client(handler, this);
            Clients.GetOrAdd(clientSocket.Handle, client);
            if (this is Lobby)
            {
                var x7E = new ServerPacket(0x7E);
                x7E.WriteByte(0x1B);
                x7E.WriteString("CONNECTED SERVER\n");
                client.Enqueue(x7E);
            }
            handler.BeginReceive(client.RecvBuffer, 0, client.SocketBufferSize, 0,
                new AsyncCallback(ReadCallback), client);
        }

        public void Send(Client client, ServerPacket serverPacket)
        {
            byte[] byteData = serverPacket.ToArray();
            client.Socket.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        public void SendCallback(IAsyncResult ar)
        {
            try
            {
                Client client = (Client) ar.AsyncState;
                int bytesSent = client.Socket.EndSend(ar);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat("Error transmitting data: {0}", e.ToString());
            }
        }
        public void ReadCallback(IAsyncResult ar)
        {
            Client client = (Client) ar.AsyncState;
            Socket workSocket = client.Socket;
            SocketError errorCode;
            int bytesRead = workSocket.EndReceive(ar, out errorCode);

            if (errorCode != SocketError.Success)
            {
                bytesRead = 0;
            }

            if (bytesRead > 0)
            {
                client.FullRecvBuffer.AddRange(client.RecvBuffer);
                if (client.FullRecvBuffer[0] != 0xAA)
                {
                    Logger.DebugFormat("cid {0}: client is disconnected",
                        client.ConnectionId);
                    client.Connected = false;
                    return;
                }

                while (client.FullRecvBuffer.Count > 3)
                {
                    var length = ((int) client.FullRecvBuffer[1] << 8) + (int) client.FullRecvBuffer[2] + 3;
                    if (length > client.FullRecvBuffer.Count)
                    {
                        // Get moar bytes
                        workSocket.BeginReceive(client.RecvBuffer, 0, client.SocketBufferSize, 0,
                            new AsyncCallback(this.ReadCallback), client);
                    }
                    else
                    {
                        List<byte> range = client.FullRecvBuffer.GetRange(0, length);
                        client.FullRecvBuffer.RemoveRange(0, length);
                        ClientPacket receivedPacket = new ClientPacket(range.ToArray());
                        if (receivedPacket.ShouldEncrypt)
                        {
                            receivedPacket.Decrypt(client);
                        }
                        if (receivedPacket.Opcode == 0x39 || receivedPacket.Opcode == 0x3A)
                            receivedPacket.DecryptDialog();
                        try
                        {
                            if (this is Lobby)
                            {
                                Logger.DebugFormat("Lobby: 0x{0:X2}", receivedPacket.Opcode);
                                var handler = (this as Lobby).PacketHandlers[receivedPacket.Opcode];
                                handler.Invoke(client, receivedPacket);
                                client.UpdateLastReceived();
                            }
                            else if (this is Login)
                            {
                                Logger.DebugFormat("Login: 0x{0:X2}", receivedPacket.Opcode);
                                var handler = (this as Login).PacketHandlers[receivedPacket.Opcode];
                                handler.Invoke(client, receivedPacket);
                                client.UpdateLastReceived();
                            }
                            else
                            {
                                client.UpdateLastReceived(receivedPacket.Opcode != 0x45 && receivedPacket.Opcode != 0x75);
                                Logger.DebugFormat("Queuing: 0x{0:X2}", receivedPacket.Opcode);
                                World.MessageQueue.Add(new HybrasylClientMessage(receivedPacket, client.ConnectionId));
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.ErrorFormat("EXCEPTION IN HANDLING: 0x{0:X2}: {1}", receivedPacket.Opcode, e);
                        }
                       
                    }
                }

            }
            else
            {
                Logger.DebugFormat("cid {0}: client is disconnected or corrupt packets received", client.ConnectionId);
                client.Connected = false;
                return;
            }


        }

        public virtual void HandlePacket(ClientPacket clientPacket)
        {
           
        }
        public virtual void Shutdown()
        {
            Logger.WarnFormat("{0}: shutting down", this.GetType().ToString());
            Listener.Close();
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
