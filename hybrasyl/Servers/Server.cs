// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Networking;
using Hybrasyl.Networking.Throttling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hybrasyl.Servers;

public delegate void LobbyPacketHandler(IClient client, ClientPacket packet);

public delegate void LoginPacketHandler(IClient client, ClientPacket packet);

public delegate void WorldPacketHandler(object obj, ClientPacket packet);

public delegate void ControlMessageHandler(HybrasylControlMessage message);

public class Server
{
    protected static ManualResetEvent AllDone = new(false);

    private readonly ClientType _clientType;

    public ConcurrentDictionary<IntPtr, IClient> Clients;

    public Server(int port, bool isDefault = false, ClientType clientType = ClientType.Client)
    {
        Clients = new ConcurrentDictionary<IntPtr, IClient>();
        Port = port;
        _clientType = clientType;

        Throttles = new Dictionary<byte, IPacketThrottle>();
        ExpectedConnections = new ConcurrentDictionary<uint, Redirect>();
        for (byte i = 0; i < 255; ++i)
            WorldPacketHandlers[i] = (c, p) => GameLog.Warning($"{GetType().Name}: Unhandled opcode 0x{p.Opcode:X2}");
        foreach (var opcode in Enum.GetValues<ControlOpcode>())
            ControlMessageHandlers[opcode] = p =>
                GameLog.Warning($"{GetType().Name}: Unhandled control message type {opcode}");
        Default = isDefault;
        Task.Run(ProcessOutbound);
        Game.RegisterServer(this);
    }

    public int Port { get; }
    public bool Default { get; set; }
    public ISocketProxy Listener { get; private set; }
    public Dictionary<byte, WorldPacketHandler> WorldPacketHandlers { get; } = new();
    public Dictionary<byte, IPacketThrottle> Throttles { get; }

    public Dictionary<ControlOpcode, ControlMessageHandler> ControlMessageHandlers { get; } = new();
    public ConcurrentDictionary<uint, Redirect> ExpectedConnections { get; }

    public CancellationToken StopToken { get; set; }

    public bool Active { get; protected set; }

    public Guid Guid { get; } = Guid.NewGuid();

    public async void ProcessOutbound()
    {
        while (!StopToken.IsCancellationRequested)
        {
            foreach (var kvp in Clients)
            {
                var ptr = kvp.Key;
                var client = kvp.Value;
                switch (client.Connected)
                {
                    //GameLog.ErrorFormat($"Server {this.GetType().Name} Client {client.ConnectionId}: buffer sending");
                    case true when client.ClientState.SendBufferDepth > 0:
                        await Task.Run(client.FlushSendBuffer, StopToken);
                        break;
                    case false:
                        Clients.TryRemove(ptr, out var _);
                        break;
                }
            }

            // TODO: configurable?
            await Task.Delay(50, StopToken);
        }
    }

    public void RegisterPacketThrottle(IPacketThrottle newThrottle)
    {
        Throttles[newThrottle.Opcode] = newThrottle;
    }

    public ThrottleResult PacketThrottleCheck(Client client, ClientPacket packet) =>
        Throttles.TryGetValue(packet.Opcode, out var throttle)
            ? throttle.ProcessThrottle(new PacketThrottleData(client, packet))
            : ThrottleResult.OK;

    public void StartListening()
    {
        Listener = SocketProxy.Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
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
        AllDone.Set();
        if (!Active) return;
        ISocketProxy handler;
        ISocketProxy clientSocket;
        try
        {
            clientSocket = (ISocketProxy)ar.AsyncState;
            handler = clientSocket.EndAccept(ar);
        }
        catch (ObjectDisposedException e)
        {
            GameLog.Error($"Disposed socket {e.Message}");
            return;
        }

        var client = ClientFactory.CreateClient(_clientType, handler, this);
        Clients.TryAdd(handler.Handle, client);
        GlobalConnectionManifest.RegisterClient(client);

        switch (this)
        {
            case Lobby:
                {
                    var x7E = new ServerPacket(0x7E);
                    x7E.WriteByte(0x1B);
                    x7E.WriteString("CONNECTED SERVER\n");
                    client.Enqueue(x7E);
                    GameLog.DebugFormat("Lobby: AcceptConnection occuring");
                    GameLog.Info("Lobby: cid is {0}", client.ConnectionId);
                    break;
                }
            case Login:
                GameLog.DebugFormat("Login: AcceptConnection occuring");
                GameLog.Info("Login: cid is {0}", client.ConnectionId);
                break;
            case World:
                GameLog.DebugFormat("World: AcceptConnection occuring");
                GameLog.Info("World: cid is {0}", client.ConnectionId);
                break;
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
        var state = (IClientState)ar.AsyncState;

        GameLog.Debug(
            $"SocketConnected: {state.WorkSocket.Connected}, IAsyncResult: Completed: {ar.IsCompleted}, CompletedSynchronously: {ar.CompletedSynchronously}, queue size: {state.Buffer.Length}");
        GameLog.Debug("Running read callback");

        if (!GlobalConnectionManifest.ConnectedClients.TryGetValue(state.Id, out var client))
        {
            // Is this a redirect?
            if (!GlobalConnectionManifest.TryGetRedirect(state.Id, out var redirect))
            {
                GameLog.ErrorFormat("Receive: data from unknown client (id {0}, closing connection", state.Id);
                state.WorkSocket.Close();
                state.WorkSocket.Dispose();
                return;
            }

            client = redirect.Client;
            client.ClientState = state;
            client.EncryptionKey ??= redirect.EncryptionKey;
            GlobalConnectionManifest.RegisterClient(client);
            return;
        }

        try
        {
            var bytesRead = state.WorkSocket.EndReceive(ar, out var errorCode);
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
            while (client.ClientState.TryGetPacket(out var receivedPacket)) client.Enqueue(receivedPacket);
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            GameLog.Error("ReadCallback error: {e}", e);
        }

        if (state.Connected)
            ContinueReceiving(state, client);
    }

    private void ContinueReceiving(IClientState state, IClient client)
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

    public Redirect(IClient client, Server source, Server destination, string name, byte seed, byte[] key)
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
    public IClient Client { get; set; }
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