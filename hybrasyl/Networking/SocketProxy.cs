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
using System;
using System.Net;
using System.Net.Sockets;

namespace Hybrasyl.Networking;

public sealed class SocketProxy(Socket toProxy) : ISocketProxy
{
    public EndPoint? RemoteEndPoint => toProxy.RemoteEndPoint;

    public bool Connected => toProxy.Connected;

    public IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback,
        object state) => toProxy.BeginSend(buffer, offset, size, socketFlags, callback, state);

    public void Dispose() => toProxy.Dispose();

    public int EndSend(IAsyncResult asyncResult) => toProxy.EndSend(asyncResult);

    public int EndSend(IAsyncResult asyncResult, out SocketError error) => toProxy.EndSend(asyncResult, out error);

    public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        AsyncCallback? callback, object? state) =>
        toProxy.BeginReceive(buffer, offset, size, socketFlags, callback, state);

    public int EndReceive(IAsyncResult asyncResult) => toProxy.EndReceive(asyncResult);

    public int EndReceive(IAsyncResult asyncResult, out SocketError error) =>
        toProxy.EndReceive(asyncResult, out error);

    public void Shutdown(SocketShutdown how) => toProxy.Shutdown(how);

    public void Close() => toProxy.Close();

    public void Close(int timeout) => toProxy.Close(timeout);

    public void Disconnect(bool reuseSocket) => toProxy.Disconnect(reuseSocket);
}