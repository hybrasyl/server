using Hybrasyl.Interfaces;
using Hybrasyl.Networking.Throttling;
using Hybrasyl.Servers;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Networking;

public class TestClient : IClient
{
    public ClientState ClientState
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public long ConnectedSince => throw new NotImplementedException();

    public byte ServerOrdinal => throw new NotImplementedException();

    public Dictionary<byte, ThrottleInfo> ThrottleState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool Connected => throw new NotImplementedException();

    public ISocketProxy Socket => throw new NotImplementedException();

    public long ConnectionId => throw new NotImplementedException();

    public string RemoteAddress => throw new NotImplementedException();

    public byte EncryptionSeed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public byte[] EncryptionKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public string NewCharacterName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public string NewCharacterPassword { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public int ServerType => throw new NotImplementedException();

    public void CheckIdle()
    {
        throw new NotImplementedException();
    }

    public void Disconnect()
    {
        throw new NotImplementedException();
    }

    public void Enqueue(ServerPacket packet)
    {
        throw new NotImplementedException();
    }

    public void Enqueue(ClientPacket packet)
    {
        throw new NotImplementedException();
    }

    public void FlushReceiveBuffer()
    {
        throw new NotImplementedException();
    }

    public void FlushSendBuffer()
    {
        throw new NotImplementedException();
    }

    public byte[] GenerateKey(ushort bRand, byte sRand)
    {
        throw new NotImplementedException();
    }

    public void GenerateKeyTable(string seed)
    {
        throw new NotImplementedException();
    }

    public bool IsHeartbeatExpired()
    {
        throw new NotImplementedException();
    }

    public bool IsHeartbeatValid(byte a, byte b)
    {
        throw new NotImplementedException();
    }

    public bool IsHeartbeatValid(int localTickCount, int clientTickCount)
    {
        throw new NotImplementedException();
    }

    public bool IsIdle()
    {
        throw new NotImplementedException();
    }

    public void LoginMessage(string message, byte type)
    {
        throw new NotImplementedException();
    }

    public void Redirect(Redirect redirect, bool isLogoff, int transmitDelay)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
}