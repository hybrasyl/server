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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace Hybrasyl.Networking;

public class ClientState(ISocketProxy incoming) : IClientState
{
    private const int BufferSize = 65600;

    private readonly object _recvlock = new();

    private readonly object _sendlock = new();
    private ConcurrentQueue<InboundFrame> _receiveBuffer = new();
    private ConcurrentQueue<QueuedPacket> _sendBuffer = new();

    public int BytesReceived { get; set; }
    public ManualResetEvent SendComplete { get; set; } = new(false);

    public int SendBufferDepth => _sendBuffer.Count;
    public bool Connected { get; set; } = true;
    public long Id { get; } = GlobalConnectionManifest.GetNewConnectionId();

    public object ReceiveLock
    {
        get
        {
            var frame = new StackFrame(1);
            GameLog.Debug(
                "Receive lock acquired by: {Method} on thread {ThreadId}", frame.GetMethod()?.Name, Environment.CurrentManagedThreadId);
            return _recvlock;
        }
    }

    public object SendLock
    {
        get
        {
            var frame = new StackFrame(1);
            GameLog.Debug(
                "Send lock acquired by: {Method} on thread {ThreadId}", frame.GetMethod()?.Name, Environment.CurrentManagedThreadId);
            return _sendlock;
        }
    }

    public ISocketProxy WorkSocket { get; } = incoming;
    public byte[] Buffer { get; set; } = new byte[BufferSize];
    public bool SendBufferEmpty => _sendBuffer.IsEmpty;

    public IEnumerable<byte> ReceiveBufferTake(int range)
    {
        lock (ReceiveLock)
        {
            return Buffer.Take(range);
        }
    }

    public IEnumerable<byte> ReceiveBufferPop(int range)
    {
        var ret = Buffer.Take(range);
        var asList = Buffer.ToList();
        asList.RemoveRange(0, range);
        Buffer = new byte[BufferSize];
        Array.ConstrainedCopy(asList.ToArray(), 0, Buffer, 0, asList.ToArray().Length);
        return ret;
    }

    public void SendBufferAdd(QueuedPacket packet)
    {
        _sendBuffer.Enqueue(packet);
    }

    public bool SendBufferPeek(out QueuedPacket packet) => _sendBuffer.TryPeek(out packet);

    public bool SendBufferTake(out QueuedPacket packet) => _sendBuffer.TryDequeue(out packet);

    public void ResetReceive()
    {
        lock (ReceiveLock)
        {
            Buffer = new byte[BufferSize];
            _receiveBuffer = new ConcurrentQueue<InboundFrame>();
        }
    }

    public void ResetSend()
    {
        _sendBuffer = new ConcurrentQueue<QueuedPacket>();
    }

    public void Dispose()
    {
        try
        {
            WorkSocket.Shutdown(SocketShutdown.Both);
            ResetReceive();
            ResetSend();
            WorkSocket.Close();
            WorkSocket.Dispose();
        }
        catch (Exception e)
        {
            Game.ReportException(e);
            WorkSocket.Close();
            WorkSocket.Dispose();
        }

        Connected = false;
    }

    public bool TryGetFrame(out InboundFrame frame)
    {
        frame = default;
        lock (ReceiveLock)
        {
            if (Buffer.Length != 0 && Buffer[0] == 0xAA && Buffer.Length > 3)
            {
                var packetLength = (Buffer[1] << 8) + Buffer[2] + 3;
                // Complete frame, pop it off and return it
                if (BytesReceived >= packetLength)
                {
                    BytesReceived -= packetLength;
                    frame = InboundFrame.FromWire(ReceiveBufferPop(packetLength).ToArray());
                    return true;
                }
            }

            return false;
        }
    }

    public void ReceiveBufferAdd(InboundFrame frame)
    {
        _receiveBuffer.Enqueue(frame);
    }

    public bool ReceiveBufferTake(out InboundFrame frame) => _receiveBuffer.TryDequeue(out frame);
}