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
using System.Threading.Tasks;

namespace Hybrasyl.Networking;

public sealed class TestSocket : ISocketProxy
{
    private static nint _handle = 1000;

    public EndPoint RemoteEndPoint => new IPEndPoint(IPAddress.Parse("127.0.0.1"), 31337);

    public nint Handle
    {
        get
        {
            _handle++;
            return _handle;
        }
    }


    public bool Connected => true;

    public static ISocketProxy Create(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) =>
        throw new NotImplementedException();

    public static ISocketProxy CreateFromAsyncResult(IAsyncResult asyncResult) => (TestSocket)asyncResult.AsyncState;

    public ISocketProxy EndAccept(IAsyncResult asyncResult) => TaskToAsyncResult.End<TestSocket>(asyncResult);

    public IAsyncResult BeginAccept(AsyncCallback callback, object state) => throw new NotImplementedException();

    public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        AsyncCallback callback, object state) => throw new NotImplementedException();

    public IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback,
        object state) => throw new NotImplementedException();

    public void Bind(IPEndPoint remoteEndPoint)
    {
        throw new NotImplementedException();
    }

    public void Close() { }

    public void Close(int timeout) { }

    public void Disconnect(bool reuseSocket) { }

    public void Dispose() { }

    public int EndReceive(IAsyncResult asyncResult) => throw new NotImplementedException();

    public int EndReceive(IAsyncResult asyncResult, out SocketError error) => throw new NotImplementedException();

    public int EndSend(IAsyncResult asyncResult) => throw new NotImplementedException();

    public int EndSend(IAsyncResult asyncResult, out SocketError error) => throw new NotImplementedException();

    public void Listen(int backlog)
    {
        throw new NotImplementedException();
    }

    public void Shutdown(SocketShutdown how)
    {
        throw new NotImplementedException();
    }
}