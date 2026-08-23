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
using Hybrasyl.Protocol.Transport;
using Hybrasyl.Protocol.Wire;
using Hybrasyl.Protocol.Packets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hybrasyl.Servers;

public delegate void LobbyPacketHandler(IClient client, InboundBody packet);

public delegate void LoginPacketHandler(IClient client, InboundBody packet);

public delegate void WorldPacketHandler(object obj, InboundBody packet);

public delegate void ControlMessageHandler(HybrasylControlMessage message);

public delegate void ExtensionPacketHandler(IClient client, IExtensionClientPacket packet);

public class Server
{
    protected static ManualResetEvent AllDone = new(false);

    private readonly ClientType _clientType;
    public readonly IPAddress BindAddress;

    public ConcurrentDictionary<IntPtr, IClient> Clients;

    public Server(IPAddress bindAddress, int port, bool isDefault = false, ClientType clientType = ClientType.Client)
    {
        Clients = new ConcurrentDictionary<IntPtr, IClient>();
        Port = port;
        _clientType = clientType;
        BindAddress = bindAddress;

        Throttles = new Dictionary<byte, IPacketThrottle>();
        ExpectedConnections = new ConcurrentDictionary<uint, Redirect>();
        for (var i = 0; i < 256; ++i)
            WorldPacketHandlers[(byte)i] = (c, p) => GameLog.Warning("{ServerType}: Unhandled opcode 0x{Opcode:X2}", GetType().Name, p.Opcode);
        foreach (var opcode in Enum.GetValues<ControlOpcode>())
            ControlMessageHandlers[opcode] = p =>
                GameLog.Warning("{ServerType}: Unhandled control message type {ControlOpcode}", GetType().Name, opcode);

        // Extension infrastructure opcodes are answered by every server in the stack; a
        // dialect connection on lobby, login, or world heartbeats the same way.
        // An exchange is one opcode across both directions, so the reply to a client probe is
        // another ClientEcho rather than a differently-numbered packet.
        // Both sides log the token at Info: it is the only field pairing a reply with its probe.
        ExtensionHandlers[ExtensionOpcodes.ClientEcho] = (client, packet) =>
        {
            if (packet is not ClientEcho echo) return;

            if (client.ClientState.Tls is not { } tls)
            {
                GameLog.Warning(
                    "cid {ConnectionId}: ClientEcho token 0x{Token:X16} arrived with no TLS session; not answered",
                    client.ConnectionId, echo.Token);

                return;
            }

            GameLog.Info("cid {ConnectionId}: ClientEcho token 0x{Token:X16} received, answering",
                client.ConnectionId, echo.Token);
            tls.Send(new ClientEcho(echo.Token));
        };
        ExtensionHandlers[ExtensionOpcodes.ServerEcho] = (client, packet) =>
        {
            if (packet is ServerEcho echo)
                GameLog.Info("cid {ConnectionId}: ServerEcho reply, token 0x{Token:X16}",
                    client.ConnectionId, echo.Token);
            else
                GameLog.Warning("cid {ConnectionId}: ServerEcho opcode carrying {Type}",
                    client.ConnectionId, packet.GetType().Name);
        };
        Default = isDefault;
        Task.Run(ProcessOutbound);
        Game.RegisterServer(this);
    }

    public int Port { get; }
    public bool Default { get; set; }
    public ISocketProxy? Listener { get; private set; }
    public Dictionary<byte, WorldPacketHandler> WorldPacketHandlers { get; } = new();

    /// <summary>
    ///     Handlers for native extension packets (u16 opcodes), keyed by opcode. Unlike the
    ///     byte-indexed retail arrays there is no 256-slot pre-fill; an unregistered opcode is
    ///     logged and discarded at dispatch.
    /// </summary>
    public Dictionary<ushort, ExtensionPacketHandler> ExtensionHandlers { get; } = new();

    /// <summary>
    ///     Opcodes with a real handler, as opposed to the unhandled-opcode logger every one of the
    ///     256 slots is pre-filled with in the constructor.
    /// </summary>
    /// <remarks>
    ///     The pre-fill is why <c>WorldPacketHandlers.ContainsKey</c> cannot answer "is this opcode
    ///     handled" — it is always true. The receive loop's unknown-opcode gate asked exactly that
    ///     and was therefore dead code from the moment the pre-fill was added, so an unregistered
    ///     opcode was decrypted and unwrapped in full before reaching a logger that discards it.
    ///     Populated by <see cref="World.SetPacketHandlers" />.
    /// </remarks>
    public HashSet<byte> RegisteredWorldOpcodes { get; } = [];
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
                    case true when client.ClientState.SendBufferDepth > 0:
                        await Task.Run(client.FlushSendBuffer, StopToken);
                        break;
                    case false:
                        Clients.TryRemove(ptr, out var _);
                        break;
                }
            }

            await Task.Delay(50, StopToken);
        }
    }

    public void RegisterPacketThrottle(IPacketThrottle newThrottle)
    {
        Throttles[newThrottle.Opcode] = newThrottle;
    }

    public ThrottleResult PacketThrottleCheck(Client client, InboundBody packet) =>
        Throttles.TryGetValue(packet.Opcode, out var throttle)
            ? throttle.ProcessThrottle(new PacketThrottleData(client, packet))
            : ThrottleResult.OK;

    public void StartListening()
    {
        Listener = SocketProxy.Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Listener.Bind(new IPEndPoint(BindAddress, Port));
        Active = true;
        Listener.Listen(100);
        GameLog.InfoFormat("Starting TcpListener: {0}:{1}", BindAddress.ToString(), Port);
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
            // AsyncState is the listener socket we passed to BeginAccept; never null here.
            clientSocket = (ISocketProxy)ar.AsyncState!;
            handler = clientSocket.EndAccept(ar);
        }
        catch (ObjectDisposedException e)
        {
            GameLog.Error(e, "Disposed socket");
            return;
        }

        var client = ClientFactory.CreateClient(_clientType, handler, this);

        Clients.TryAdd(handler.Handle, client);
        GlobalConnectionManifest.RegisterClient(client);

        switch (this)
        {
            case Lobby:
                {
                    var greeting = new DALib.Networking.Packets.Server.AcceptConnectionPacket
                    {
                        Message = "CONNECTED SERVER\n"
                    };
                    // Nothing further may be sent here: a capable client upgrades only if the
                    // lobby falls silent after the greeting, so a second enqueue would quietly
                    // keep it on retail rather than break anything.
                    client.Enqueue(Game.TlsCertificate is null
                        ? greeting
                        : new CapabilityGreetingPacket(greeting));
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
        // AsyncState is the IClientState we passed to BeginReceive; never null here.
        var state = (IClientState)ar.AsyncState!;

        GameLog.Debug(
            "SocketConnected: {SocketConnected}, IAsyncResult: Completed: {Completed}, CompletedSynchronously: {CompletedSynchronously}, queue size: {QueueSize}", state.WorkSocket.Connected, ar.IsCompleted, ar.CompletedSynchronously, state.Buffer.Length);
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
                GameLog.Error("bytesRead: {BytesRead}, errorCode: {ErrorCode}", bytesRead, errorCode);
                client.Disconnect();
            }

            client.ClientState.BytesReceived += bytesRead;
        }
        catch (Exception e)
        {
            GameLog.Fatal("EndReceive Error:  {Exception}", e);
            Game.ReportException(e);
            client.Disconnect();
        }

        // The connection's first byte decides its transport, once (HS-1621): 0xAA is retail
        // framing and stays on this path; 0x16 is a TLS ClientHello answering the capability
        // marker; anything else is garbage and fatal. Retail clients never reach the other arms.
        if (client.ClientState.Transport == TransportKind.Undecided && client.ClientState.BytesReceived > 0)
            switch (TlsProbe.Peek(client.ClientState.Buffer.AsSpan(0, client.ClientState.BytesReceived)))
            {
                case InboundKind.Retail:
                    client.ClientState.Transport = TransportKind.Plaintext;
                    break;
                case InboundKind.TlsHandshake when Game.TlsCertificate is { } certificate:
                    {
                        client.ClientState.Transport = TransportKind.Tls;
                        // The buffered bytes are the front of the ClientHello; they belong to the
                        // handshake, never to the application. No receive is re-armed - from here
                        // the session's pump is the socket's only reader.
                        var clientHello = client.ClientState.Buffer
                            .AsSpan(0, client.ClientState.BytesReceived).ToArray();
                        client.ClientState.BytesReceived = 0;
                        _ = TlsSession.StartAsync(client, clientHello, certificate, Game.TlsMaxFrameSize);
                        return;
                    }
                case InboundKind.TlsHandshake:
                    GameLog.Warning(
                        "cid {ConnectionId}: TLS ClientHello received but no certificate is configured, disconnecting", client.ConnectionId);
                    client.Disconnect();
                    return;
                default:
                    GameLog.Warning(
                        "cid {ConnectionId}: first byte 0x{FirstByte:X2} is neither retail framing nor TLS, disconnecting", client.ConnectionId, client.ClientState.Buffer[0]);
                    client.Disconnect();
                    return;
            }

        try
        {
            while (client.ClientState.TryGetFrame(out var frame)) client.ReceiveFrame(frame);
        }
        catch (InvalidDataException e)
        {
            // A frame boundary that cannot be recovered from; leaving the socket armed lets one
            // malformed write be replayed for the cost of sending it.
            GameLog.Warning("{Reason}; disconnecting", e.Message);
            client.Disconnect();
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
