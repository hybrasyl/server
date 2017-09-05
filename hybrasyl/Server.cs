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
                if (StopToken.IsCancellationRequested)
                    return;
                AllDone.Reset();
                Listener.BeginAccept(new AsyncCallback(AcceptConnection), Listener);
                AllDone.WaitOne();
            }
        }


        public void SendLoop()
        {
            while (true)
            {
                if (StopToken.IsCancellationRequested)
                    return;
                //Logger.InfoFormat("GCM value: {0}", GlobalConnectionManifest.ConnectedClients.Count);
                foreach (var client in GlobalConnectionManifest.ConnectedClients.Select(kvp => kvp.Value))
                {
                    try
                    {
                        if (client.IsReceiving) continue;
                        
                        ServerPacket packet;
                        if (!client.ClientState.SendBufferTake(out packet)) continue;
                        if (packet.ShouldEncrypt)
                        {
                            ++client.ServerOrdinal;
                            packet.Ordinal = client.ServerOrdinal;

                            packet.GenerateFooter();
                            packet.Encrypt(client);
                        }
                        if (packet.TransmitDelay != 0)
                        {
                            Thread.Sleep(packet.TransmitDelay);
                        }
                        var buffer = packet.ToArray();

                        var byteData = (byte[]) packet;
                        if (client.ClientState.WorkSocket.Connected)
                        {
                            try
                            {
                                client.ClientState.WorkSocket.BeginSend(buffer, 0, buffer.Length, 0,
                                new AsyncCallback(SendCallback), client.ClientState);
                            }
                            catch (SocketException e)
                            {
                                Logger.Fatal(e.Message);
                            }
                            
                        }

                    }
                    catch (Exception e)
                    {
                       Logger.Fatal(e.Message);
                    }
                    
                }
                
                Thread.Sleep(100);
            }
        }

       

        public byte[] SendPacket(Client client, ServerPacket packet)
        {
            if (packet == null) return null;
            if (packet.ShouldEncrypt)
            {
                ++client.ServerOrdinal;
                packet.Ordinal = client.ServerOrdinal;

                packet.GenerateFooter();
                packet.Encrypt(client);
            }
            if (packet.TransmitDelay != 0)
            {
                Thread.Sleep(packet.TransmitDelay);
            }
            var buffer = packet.ToArray();

            return buffer;
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

        public void SendCallback(IAsyncResult ar)
        {

            ClientState state = (ClientState) ar.AsyncState;
            Client client;
            Logger.DebugFormat("EndSend");
            try
            {
                SocketError errorCode;
                var bytesSent = state.WorkSocket.EndSend(ar, out errorCode);
                if (!GlobalConnectionManifest.ConnectedClients.TryGetValue(state.Id, out client))
                {
                    Logger.ErrorFormat("Send: socket should not exist: cid {0}", state.Id);
                    state.WorkSocket.Close();
                    state.WorkSocket.Dispose();
                    return;
                }

                if (bytesSent == 0 || errorCode != SocketError.Success)
                {
                    Logger.ErrorFormat("cid {0}: disconnected");
                    client.Disconnect();
                    throw new SocketException((int)errorCode);
                }
            }
            catch (SocketException e)
            {
                Logger.Fatal($"Error Code: {e.ErrorCode}, {e.Message}");
                state.WorkSocket.Close();
            }
            catch (ObjectDisposedException)
            {
                //client.Disconnect();
                state.WorkSocket.Close();
            }
        }

        private void Send(ClientState handler, byte[] packet)
        {
            try
            {
                if (packet == null) return;
                handler.WorkSocket.BeginSend(packet, 0, packet.Length, SocketFlags.None, new AsyncCallback(SendCallback), handler);
            }
            catch (Exception e)
            {
                Logger.Fatal(e.Message);
                throw;
            }

        }

        public void ReadCallback(IAsyncResult ar)
        {
            ClientState state = (ClientState) ar.AsyncState;
            Client client;
            SocketError errorCode = SocketError.SocketError;
            int bytesRead = 0;

            try
            {
                bytesRead = state.WorkSocket.EndReceive(ar, out errorCode);
                if (errorCode != SocketError.Success)
                {
                    bytesRead = 0;
                }
                else if (errorCode == SocketError.NoData || errorCode == SocketError.AccessDenied ||
                         errorCode == SocketError.Fault || errorCode == SocketError.InvalidArgument ||
                         errorCode == SocketError.NoBufferSpaceAvailable)
                {
                    throw new SocketException((int) errorCode);
                }
            }
            catch (SocketException e)
            {
                Logger.Fatal($"ErrorCode: {e.ErrorCode}, {e.Message}");
                state.WorkSocket.Close();
            }
            catch (ObjectDisposedException)
            {
                state.WorkSocket.Close();
            }

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

            if (bytesRead > 0)
            {

                var inboundBytes = state.ReceiveBufferTake(bytesRead).ToArray();
                if (inboundBytes[0] != 0xAA)
                {
                    Logger.DebugFormat("cid {0}: client is sending corrupt data, potentially",
                        client.ConnectionId);
                    state.ResetReceive();
                }
                else
                {
                    while (inboundBytes.Length > 3)
                    {
                        var packetLength = ((int) inboundBytes[1] << 8) + (int) inboundBytes[2] + 3;
                        if (packetLength > inboundBytes.Length)
                        {
                            // We haven't received the entire packet yet; read more bytes
                            break;
                        }
                        else
                        {
                            // We've received an intact packet, pop it off
                            ClientPacket receivedPacket =
                                new ClientPacket(state.ReceiveBufferPop(packetLength).ToArray());
                            // Also remove it from our local buffer...this seems kinda gross to me
                            inboundBytes =
                                new List<byte>(inboundBytes).GetRange(packetLength,
                                        inboundBytes.Length - packetLength)
                                    .ToArray();

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
                                    if (!client.IsReceiving)
                                    {
                                        client.IsReceiving = true;
                                        ServerPacket sendBuff;
                                        state.SendBufferTake(out sendBuff);
                                        Send(state, SendPacket(client, sendBuff));
                                        client.IsReceiving = false;
                                    }
                                    Logger.DebugFormat("Lobby packet done");
                                    client.UpdateLastReceived();
                                }
                                else if (this is Login)
                                {
                                    Logger.DebugFormat("Login: 0x{0:X2}", receivedPacket.Opcode);
                                    var handler = (this as Login).PacketHandlers[receivedPacket.Opcode];
                                    handler.Invoke(client, receivedPacket);
                                    if (!client.IsReceiving)
                                    {
                                        client.IsReceiving = true;
                                        ServerPacket sendBuff;
                                        state.SendBufferTake(out sendBuff);
                                        Send(state, SendPacket(client, sendBuff));
                                        client.IsReceiving = false;
                                    }
                                    Logger.DebugFormat("Login packet done");
                                    client.UpdateLastReceived();
                                }
                                else
                                {
                                    client.UpdateLastReceived(receivedPacket.Opcode != 0x45 &&
                                                              receivedPacket.Opcode != 0x75);
                                    Logger.DebugFormat("Queuing: 0x{0:X2}", receivedPacket.Opcode);
                                    // Check for throttling
                                    ThrottleInfo throttleInfo = null;
                                    if (client.Throttle.ContainsKey(receivedPacket.Opcode))
                                    {
                                        throttleInfo = client.Throttle[receivedPacket.Opcode];
                                    }

                                    if (throttleInfo == null || throttleInfo.IsThrottled == false)
                                    {
                                        if (throttleInfo != null) client.Throttle[receivedPacket.Opcode].Received();
                                        World.MessageQueue.Add(new HybrasylClientMessage(receivedPacket,
                                            client.ConnectionId));
                                        if (!client.IsReceiving)
                                        {
                                            client.IsReceiving = true;
                                            ServerPacket sendBuff;
                                            state.SendBufferTake(out sendBuff);
                                            Send(state, SendPacket(client, sendBuff));
                                            client.IsReceiving = false;
                                        }
                                    }
                                    else
                                    {
                                        client.Throttle[receivedPacket.Opcode].TotalThrottled++;
                                    }

                                }
                            }
                            catch (Exception e)
                            {
                                Logger.ErrorFormat("EXCEPTION IN HANDLING: 0x{0:X2}: {1}", receivedPacket.Opcode, e);
                            }

                        }
                    }
                }
            }
            else
            {
                if (errorCode != SocketError.Success)
                {
                    Logger.DebugFormat("cid {0}: client is disconnected or corrupt packets received",
                        client.ConnectionId);
                    client.Disconnect();
                }
                return;
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

