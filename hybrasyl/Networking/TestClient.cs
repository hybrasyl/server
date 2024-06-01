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
using Hybrasyl.Internals.Logging;
using Hybrasyl.Networking.Throttling;
using Hybrasyl.Servers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Networking;

public class TestClient : AbstractClient, IClient
{
    private static int _clientId;
    private readonly object _lock = new();

    public TestClient(ISocketProxy proxy, Server server = null)
    {
        lock (_lock)
        {
            _clientId++;
            ConnectionId = _clientId;
        }

        Server = server;
        ClientState = new TestClientState(proxy);
    }

    public Redirect LastRedirect { get; set; }

    public IClientState ClientState { get; set; }

    public long ConnectedSince { get; init; } = DateTime.Now.Ticks;

    public byte ServerOrdinal { get; set; }

    public Dictionary<byte, ThrottleInfo> ThrottleState
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public bool Connected => Socket.Connected;

    public ISocketProxy Socket => ClientState.WorkSocket;

    public long ConnectionId { get; }

    public string RemoteAddress => "127.0.0.1";

    public byte EncryptionSeed { get; set; }

    public byte[] EncryptionKey { get; set; }

    public string NewCharacterName { get; set; }

    public string NewCharacterPassword { get; set; }

    public void CheckIdle() { }

    public void Disconnect()
    {
        ClientState.WorkSocket?.Disconnect(false);
        ClientState.Dispose();
    }

    public void Enqueue(ServerPacket packet, bool flush = false) => ClientState.SendBufferAdd(packet);

    public void Enqueue(ClientPacket packet) => ClientState.ReceiveBufferAdd(packet);

    public void FlushReceiveBuffer()
    {
        throw new NotImplementedException();
    }

    public void FlushSendBuffer()
    {
        throw new NotImplementedException();
    }

    public bool IsHeartbeatExpired() => throw new NotImplementedException();

    public bool IsHeartbeatValid(byte a, byte b) => throw new NotImplementedException();

    public bool IsHeartbeatValid(int localTickCount, int clientTickCount) => throw new NotImplementedException();

    public bool IsIdle() => throw new NotImplementedException();

    public void LoginMessage(string message, byte type)
    {
        LastMessage = message;
    }

    public void Redirect(Redirect redirect, bool isLogoff, int transmitDelay)
    {
        LastRedirect = redirect;
        GameLog.InfoFormat("Processing redirect");
        GlobalConnectionManifest.RegisterRedirect(this, redirect);
        GameLog.InfoFormat("Redirect: cid {0}", ConnectionId);
        GameLog.Info($"Redirect EncryptionKey is {Encoding.ASCII.GetString(redirect.EncryptionKey)}");
        if (isLogoff) GlobalConnectionManifest.DeregisterClient(this);
        redirect.Destination.ExpectedConnections.TryAdd(redirect.Id, redirect);
    }

    public void SendByteHeartbeat()
    {
        throw new NotImplementedException();
    }

    public void SendCallback(IAsyncResult ar)
    {
        throw new NotImplementedException();
    }

    public void SendMessage(string message, byte type)
    {
        LastMessage = message;
    }

    public void SendTickHeartbeat()
    {
        throw new NotImplementedException();
    }

    public void ToggleIdle()
    {
        throw new NotImplementedException();
    }

    public void UpdateLastReceived(bool updateIdle)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        Disconnect();
        ClientState.WorkSocket.Close();
        ClientState.WorkSocket.Dispose();
    }
}