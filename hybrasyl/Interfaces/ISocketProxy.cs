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

using System;
using System.Net;
using System.Net.Sockets;

namespace Hybrasyl.Interfaces;

public interface ISocketProxy : IDisposable
{
    public EndPoint? RemoteEndPoint { get; }
    public bool Connected { get; }

    public nint Handle { get; }

    public void Bind(IPEndPoint remoteEndPoint);
    public void Listen(int backlog);

    public static abstract ISocketProxy Create(AddressFamily addressFamily, SocketType socketType,
        ProtocolType protocolType);

    public static abstract ISocketProxy CreateFromAsyncResult(IAsyncResult asyncResult);

    public IAsyncResult? BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback,
        object state);

    public IAsyncResult BeginAccept(AsyncCallback? callback, object? state);

    public ISocketProxy EndAccept(IAsyncResult asyncResult);

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