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

using Hybrasyl.Networking;
using Hybrasyl.Networking.Throttling;
using Hybrasyl.Servers;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Interfaces;

/// <summary>
///     Interface for a client connection. This will be used eventually to provide a full-scale mock
///     for testing.
/// </summary>
public interface IClient
{
    public ClientState ClientState { get; set; }
    public long ConnectedSince { get; }
    public byte ServerOrdinal { get; }

    public Dictionary<byte, ThrottleInfo> ThrottleState { get; set; }

    public bool Connected { get; }
    public ISocketProxy Socket { get; }

    public long ConnectionId { get; }

    public string RemoteAddress { get; }

    public byte EncryptionSeed { get; set; }
    public byte[] EncryptionKey { get; set; }

    public string NewCharacterName { get; set; }
    public string NewCharacterPassword { get; set; }

    public int ServerType { get; }

    public void SendByteHeartbeat();
    public void CheckIdle();
    public void SendTickHeartbeat();
    public bool IsHeartbeatValid(byte a, byte b);
    public bool IsHeartbeatValid(int localTickCount, int clientTickCount);
    public bool IsHeartbeatExpired();
    public void UpdateLastReceived(bool updateIdle);
    public void ToggleIdle();
    public bool IsIdle();

    public void Disconnect();
    public byte[] GenerateKey(ushort bRand, byte sRand);
    public void FlushSendBuffer();
    public void FlushReceiveBuffer();
    public void SendCallback(IAsyncResult ar);
    public void GenerateKeyTable(string seed);
    public void Enqueue(ServerPacket packet);
    public void Enqueue(ClientPacket packet);
    public void Redirect(Redirect redirect, bool isLogoff = true, int transmitDelay = 0);
    public void LoginMessage(string message, byte type);
    public void SendMessage(string message, byte type);
}