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

using Hybrasyl.Enums;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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
        public WorldPacketHandler[] PacketHandlers { get; private set; }
        public Dictionary<byte, IPacketThrottle> Throttles { get; private set; }

        public ControlMessageHandler[] ControlMessageHandlers { get; private set; }
        public ConcurrentDictionary<uint, Redirect> ExpectedConnections { get; private set; }

        public CancellationToken StopToken { get; set; }

        public bool Active { get; private set; }

        public static ManualResetEvent AllDone = new ManualResetEvent(false);

        public ConcurrentDictionary<IntPtr, Client> Clients;


        public Server(int port)
        {
            Clients = new ConcurrentDictionary<IntPtr, Client>();
            Port = port;
            PacketHandlers = new WorldPacketHandler[256];
            ControlMessageHandlers = new ControlMessageHandler[64];
            Throttles = new Dictionary<byte, IPacketThrottle>();
            ExpectedConnections = new ConcurrentDictionary<uint, Redirect>();
            for (int i = 0; i < 256; ++i)
                PacketHandlers[i] = (c, p) => Logger.DebugFormat("Server: Unhandled opcode 0x{0:X2}", p.Opcode);

        }

        public void RegisterPacketThrottle(IPacketThrottle newThrottle)
        {
            Throttles[newThrottle.Opcode] = newThrottle;
        }

        public ThrottleResult PacketThrottleCheck(Client client, ClientPacket packet)
        {
            IPacketThrottle throttle;
            if (Throttles.TryGetValue(packet.Opcode, out throttle))
            {
                return throttle.ProcessThrottle(new PacketThrottleData(client, packet));
            }
            return ThrottleResult.OK;
        }

        public void StartListening()
        {
            Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            Listener.Listen(100);
            Logger.InfoFormat("Starting TcpListener: {0}:{1}", IPAddress.Any.ToString(), Port);
            while (true)
            {
                if (StopToken.IsCancellationRequested)
                    return;
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
            Clients.TryAdd(handler.Handle, client);
            GlobalConnectionManifest.RegisterClient(client);
            
            if (this is Lobby)
            {
                var x7E = new ServerPacket(0x7E);
                x7E.WriteByte(0x1B);
                x7E.WriteString("CONNECTED SERVER\n");
                client.Enqueue(x7E);
                Logger.DebugFormat("Lobby: AcceptConnection occuring");
                Logger.DebugFormat("Lobby: cid is {0}", client.ConnectionId);
            }
            else if (this is Login)
            {
                Logger.DebugFormat("Login: AcceptConnection occuring");
                Logger.DebugFormat("Login: cid is {0}", client.ConnectionId);
            }
            else if (this is World)
            {
                Logger.DebugFormat("World: AcceptConnection occuring");
                Logger.DebugFormat("World: cid is {0}", client.ConnectionId);
            }
            try
            {
                handler.BeginReceive(client.ClientState.Buffer, 0, client.ClientState.Buffer.Length, 0,
                    new AsyncCallback(ReadCallback), client.ClientState);
                Logger.DebugFormat("AcceptConnection returning");
                clientSocket.BeginAccept(new AsyncCallback(AcceptConnection), clientSocket);
            }
            catch (SocketException)
            {
                handler.Close();
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            ClientState state = (ClientState)ar.AsyncState;
            Client client;
            SocketError errorCode = SocketError.SocketError;
            int bytesRead = 0;
            ClientPacket receivedPacket;
          
            Logger.Debug($"SocketConnected: {state.WorkSocket.Connected}, IAsyncResult: Completed: {ar.IsCompleted}, CompletedSynchronously: {ar.CompletedSynchronously}, queue size: {state.Buffer.Length}");
            Logger.Debug("Running read callback");

            if (!GlobalConnectionManifest.ConnectedClients.TryGetValue(state.Id, out client))
            {
                // Is this a redirect?
                Redirect redirect;
                if (!GlobalConnectionManifest.TryGetRedirect(state.Id, out redirect))
                {
                    Logger.ErrorFormat("Receive: data from unknown client (id {0}, closing connection", state.Id);
                    state.WorkSocket.Close();
                    state.WorkSocket.Dispose();
                    return;
                }
                client = redirect.Client;
                client.ClientState = state;
                GlobalConnectionManifest.RegisterClient(client);
            }

            try
            {
                bytesRead = state.WorkSocket.EndReceive(ar, out errorCode);
                if (bytesRead == 0 || errorCode != SocketError.Success)
                {
                    Logger.Error($"bytesRead: {bytesRead}, errorCode: {errorCode}");
                    client.Disconnect();
                }
            }
            catch (Exception e)
            {
                Logger.Fatal($"EndReceive Error:  {e.Message}");
                client.Disconnect();
            }

            try
            {
                // TODO: improve / refactor
                while (client.ClientState.TryGetPacket(out receivedPacket))
                {
                    client.Enqueue(receivedPacket);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ReadCallback error: {e.Message}");
            }
            ContinueReceiving(state, client);
        }

        private void ContinueReceiving(ClientState state, Client client)
        {
            // Continue getting dem bytes
            try
            {
                state.WorkSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0,
                    new AsyncCallback(this.ReadCallback), state);
                Logger.DebugFormat("Triggering receive callback");
            }
            catch (ObjectDisposedException e)
            {
                Logger.Fatal(e.Message);
                //client.Disconnect();
                state.WorkSocket.Close();
            }
            catch (SocketException e)
            {
                Logger.Fatal(e.Message);
                client.Disconnect();
            }
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

