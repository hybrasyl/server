using System;
using System.Net;
using System.Net.Sockets;

namespace Hybrasyl.Interfaces;

public interface ISocketProxy : IDisposable
{
    public EndPoint? RemoteEndPoint { get; }
    public bool Connected { get; }

    public IAsyncResult? BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state);

    public int EndSend(IAsyncResult asyncResult);
    public int EndSend(IAsyncResult asyncResult, out SocketError error);

    public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        AsyncCallback? callback, object? state);

    public int EndReceive(IAsyncResult asyncResult);
    public int EndReceive(IAsyncResult asyncResult, out SocketError error);

    public void Shutdown(SocketShutdown how);

    public void Close();

    public void Close(int timeout);

    public void Disconnect(bool reuseSocket);

}