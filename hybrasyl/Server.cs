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

using Hybrasyl.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hybrasyl;

public delegate void LobbyPacketHandler(Client client, ClientPacket packet);

public delegate void LoginPacketHandler(Client client, ClientPacket packet);

public delegate void WorldPacketHandler(object obj, ClientPacket packet);

public delegate void ControlMessageHandler(HybrasylControlMessage message);

public class Server
{
    public static ManualResetEvent AllDone = new(false);

    public ConcurrentDictionary<IntPtr, Client> Clients;

    public Server(int port, bool isDefault = false)
    {
        Clients = new ConcurrentDictionary<IntPtr, Client>();
        Port = port;

        Throttles = new Dictionary<byte, IPacketThrottle>();
        ExpectedConnections = new ConcurrentDictionary<uint, Redirect>();
        for (byte i = 0; i < 255; ++i)
            WorldPacketHandlers[i] = (c, p) => GameLog.Warning($"{GetType().Name}: Unhandled opcode 0x{p.Opcode:X2}");
        foreach (ControlOpcode opcode in Enum.GetValues<ControlOpcode>())
        {
            ControlMessageHandlers[opcode] = (p) => GameLog.Warning($"{GetType().Name}: Unhandled control message type {opcode}");
        }
        Default = isDefault;
        Task.Run(ProcessOutbound);
        Game.RegisterServer(this);
    }

    public int Port { get; }
    public bool Default { get; set; }
    public Socket Listener { get; private set; }
    public Dictionary<byte, WorldPacketHandler> WorldPacketHandlers { get; } = new();
    public Dictionary<byte, IPacketThrottle> Throttles { get; }

    public Dictionary<ControlOpcode, ControlMessageHandler> ControlMessageHandlers { get; } = new();
    public ConcurrentDictionary<uint, Redirect> ExpectedConnections { get; }

    public CancellationToken StopToken { get; set; }

    public bool Active { get; protected set; }

    public Guid Guid { get; } = new();

    public async void ProcessOutbound()
    {
        while (!StopToken.IsCancellationRequested)
        {
            foreach (var kvp in Clients)
            {
                var ptr = kvp.Key;
                var client = kvp.Value;
                if (client.Connected && client.ClientState.SendBufferDepth > 0)
                    //GameLog.ErrorFormat($"Server {this.GetType().Name} Client {client.ConnectionId}: buffer sending");
                    await Task.Run(action: () => client.FlushSendBuffer());
                if (!client.Connected)
                    Clients.TryRemove(ptr, out var _);
            }

            // TODO: configurable?
            await Task.Delay(50);
        }
    }

    public void RegisterPacketThrottle(IPacketThrottle newThrottle)
    {
        Throttles[newThrottle.Opcode] = newThrottle;
    }

    public ThrottleResult PacketThrottleCheck(Client client, ClientPacket packet)
    {
        IPacketThrottle throttle;
        if (Throttles.TryGetValue(packet.Opcode, out throttle))
            return throttle.ProcessThrottle(new PacketThrottleData(client, packet));
        return ThrottleResult.OK;
    }

    public void StartListening()
    {
        Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Listener.Bind(new IPEndPoint(IPAddress.Any, Port));
        Active = true;
        Listener.Listen(100);
        GameLog.InfoFormat("Starting TcpListener: {0}:{1}", IPAddress.Any.ToString(), Port);
        while (true)
        {
            if (StopToken.IsCancellationRequested)
            {
                Active = false;
                return;
            }

            AllDone.Reset();
            Listener.BeginAccept(AcceptConnection, Listener);
            AllDone.WaitOne();
        }
    }

    public virtual void AcceptConnection(IAsyncResult ar)
    {
        // TODO: @norrismiv async callbacks+inheritance? and/or can these callbacks suck less?
        AllDone.Set();
        if (!Active) return;
        Socket handler;
        Socket clientSocket;
        try
        {
            clientSocket = (Socket)ar.AsyncState;
            handler = clientSocket.EndAccept(ar);
        }
        catch (ObjectDisposedException e)
        {
            GameLog.Error($"Disposed socket {e.Message}");
            return;
        }

        var client = new Client(handler, this);
        Clients.TryAdd(handler.Handle, client);
        GlobalConnectionManifest.RegisterClient(client);

        if (this is Lobby)
        {
            var x7E = new ServerPacket(0x7E);
            x7E.WriteByte(0x1B);
            x7E.WriteString("CONNECTED SERVER\n");
            client.Enqueue(x7E);
            GameLog.DebugFormat("Lobby: AcceptConnection occuring");
            GameLog.DebugFormat("Lobby: cid is {0}", client.ConnectionId);
        }
        else if (this is Login)
        {
            GameLog.DebugFormat("Login: AcceptConnection occuring");
            GameLog.DebugFormat("Login: cid is {0}", client.ConnectionId);
        }
        else if (this is World)
        {
            GameLog.DebugFormat("World: AcceptConnection occuring");
            GameLog.DebugFormat("World: cid is {0}", client.ConnectionId);
        }

        try
        {
            handler.BeginReceive(client.ClientState.Buffer, 0, client.ClientState.Buffer.Length, 0,
                ReadCallback, client.ClientState);
            GameLog.DebugFormat("AcceptConnection returning");
            clientSocket.BeginAccept(AcceptConnection, clientSocket);
        }
        catch (SocketException e)
        {
            Game.ReportException(e);
            handler.Close();
        }
    }

    public void ReadCallback(IAsyncResult ar)
    {
        var state = (ClientState)ar.AsyncState;
        Client client;
        var errorCode = SocketError.SocketError;
        var bytesRead = 0;
        ClientPacket receivedPacket;

        GameLog.Debug(
            $"SocketConnected: {state.WorkSocket.Connected}, IAsyncResult: Completed: {ar.IsCompleted}, CompletedSynchronously: {ar.CompletedSynchronously}, queue size: {state.Buffer.Length}");
        GameLog.Debug("Running read callback");

        if (!GlobalConnectionManifest.ConnectedClients.TryGetValue(state.Id, out client))
        {
            // Is this a redirect?
            Redirect redirect;
            if (!GlobalConnectionManifest.TryGetRedirect(state.Id, out redirect))
            {
                GameLog.ErrorFormat("Receive: data from unknown client (id {0}, closing connection", state.Id);
                state.WorkSocket.Close();
                state.WorkSocket.Dispose();
                return;
            }

            client = redirect.Client;
            client.ClientState = state;
            if (client.EncryptionKey == null)
                client.EncryptionKey = redirect.EncryptionKey;
            GlobalConnectionManifest.RegisterClient(client);
        }

        try
        {
            bytesRead = state.WorkSocket.EndReceive(ar, out errorCode);
            if (bytesRead == 0 || errorCode != SocketError.Success)
            {
                GameLog.Error($"bytesRead: {bytesRead}, errorCode: {errorCode}");
                client.Disconnect();
            }

            client.ClientState.BytesReceived += bytesRead;
        }
        catch (Exception e)
        {
            GameLog.Fatal($"EndReceive Error:  {e.Message}");
            Game.ReportException(e);
            client.Disconnect();
        }

        try
        {
            // TODO: improve / refactor
            while (client.ClientState.TryGetPacket(out receivedPacket)) client.Enqueue(receivedPacket);
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error("ReadCallback error: {e}", e);
        }

        ContinueReceiving(state, client);
    }

    private void ContinueReceiving(ClientState state, Client client)
    {
        // Continue getting dem bytes
        try
        {
            state.WorkSocket.BeginReceive(state.Buffer, state.BytesReceived, state.Buffer.Length - state.BytesReceived,
                0,
                ReadCallback, state);
            GameLog.DebugFormat("Triggering receive callback");
        }
        catch (ObjectDisposedException e)
        {
            GameLog.Fatal(e.Message);
            //client.Disconnect();
            state.WorkSocket.Close();
        }
        catch (SocketException e)
        {
            GameLog.Fatal(e.Message);
            client.Disconnect();
        }
    }


    public virtual void Shutdown()
    {
        GameLog.WarningFormat("{ServerType}: shutting down", GetType().ToString());
        Listener?.Close();
        GameLog.WarningFormat("{ServerType}: shutdown complete", GetType().ToString());
    }
}

public class Redirect
{
    private static uint id;

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

    public uint Id { get; set; }
    public Client Client { get; set; }
    public Server Source { get; set; }
    public Server Destination { get; set; }
    public string Name { get; set; }
    public byte[] EncryptionKey { get; set; }
    public byte EncryptionSeed { get; set; }

    public bool Matches(string name, byte[] key, byte seed)
    {
        if (key.Length != EncryptionKey.Length || name != Name || seed != EncryptionSeed)
            return false;

        for (var i = 0; i < key.Length; ++i)
            if (key[i] != EncryptionKey[i])
                return false;

        return true;
    }
}