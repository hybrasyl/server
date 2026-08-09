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
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hybrasyl.Networking;

public sealed class TestSocket : ISocketProxy
{
    private static nint _handle = 1000;

    private readonly ConcurrentQueue<byte[]> _sent = new();
    private readonly SemaphoreSlim _sendSignal = new(0);

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

    public static ISocketProxy CreateFromAsyncResult(IAsyncResult asyncResult) => (TestSocket)asyncResult.AsyncState!;

    public ISocketProxy EndAccept(IAsyncResult asyncResult) => TaskToAsyncResult.End<TestSocket>(asyncResult);

    public IAsyncResult BeginAccept(AsyncCallback? callback, object? state) => throw new NotImplementedException();

    public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        AsyncCallback? callback, object? state) => throw new NotImplementedException();

    /// <summary>
    ///     Records the outbound buffer instead of writing it anywhere. <see cref="Client" /> sends
    ///     from a background <c>Task.Run</c>, so a test asserting that something *was* sent must
    ///     wait — see <see cref="TryTakeSent" />.
    /// </summary>
    /// <remarks>
    ///     The completion callback is deliberately not invoked: <see cref="Client.SendCallback" />
    ///     calls <c>EndSend</c> and looks the connection up in
    ///     <see cref="GlobalConnectionManifest" />, which would force every send test to register a
    ///     real connection. Nothing under test depends on that bookkeeping.
    /// </remarks>
    public IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback,
        object state)
    {
        _sent.Enqueue(buffer.AsSpan(offset, size).ToArray());
        _sendSignal.Release();

        return Task.CompletedTask;
    }

    /// <summary>Number of buffers sent so far. Racy by nature; prefer <see cref="TryTakeSent" />.</summary>
    public int SentCount => _sent.Count;

    /// <summary>
    ///     Waits up to <paramref name="timeout" /> for one outbound buffer and dequeues it.
    ///     Returns false if nothing was sent in that window.
    /// </summary>
    public bool TryTakeSent(TimeSpan timeout, out byte[] buffer)
    {
        buffer = [];

        return _sendSignal.Wait(timeout) && _sent.TryDequeue(out buffer!);
    }

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