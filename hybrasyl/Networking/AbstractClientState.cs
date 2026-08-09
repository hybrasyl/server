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

using Hybrasyl.Internals.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace Hybrasyl.Networking;

public abstract class AbstractClientState
{
    private const int BufferSize = 65600;
    private readonly ConcurrentQueue<InboundFrame> _receiveBuffer = new();
    private readonly object _receiveLock = new();
    private readonly object _sendLock = new();

    private byte[] _buffer = new byte[BufferSize];
    private ConcurrentQueue<OutboundPacket> _sendBuffer = new();
    public int BytesReceived { get; set; }

    public byte[] Buffer => _buffer;

    public object ReceiveLock
    {
        get
        {
            var frame = new StackFrame(1);
            GameLog.Debug(
                "Receive lock acquired by: {Method} on thread {ThreadId}", frame.GetMethod()?.Name, Thread.CurrentThread.ManagedThreadId);
            return _receiveLock;
        }
    }

    public object SendLock
    {
        get
        {
            var frame = new StackFrame(1);
            GameLog.Debug(
                "Send lock acquired by: {Method} on thread {ThreadId}", frame.GetMethod()?.Name, Thread.CurrentThread.ManagedThreadId);
            return _sendLock;
        }
    }

    public void ReceiveBufferAdd(InboundFrame frame) => _receiveBuffer.Enqueue(frame);
    public bool ReceiveBufferTake(out InboundFrame frame) => _receiveBuffer.TryDequeue(out frame);

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
        Array.Resize(ref _buffer, Buffer.Length - range);
        return ret;
    }

    public void SendBufferAdd(OutboundPacket packet) => _sendBuffer.Enqueue(packet);
    public bool SendBufferPeek(out OutboundPacket packet) => _sendBuffer.TryPeek(out packet);
    public bool SendBufferTake(out OutboundPacket packet) => _sendBuffer.TryDequeue(out packet);

    public void ResetReceive()
    {
        lock (ReceiveLock)
        {
            _buffer = new byte[BufferSize];
        }
    }

    public void ResetSend()
    {
        _sendBuffer = new ConcurrentQueue<OutboundPacket>();
    }

    public bool TryGetFrame(out InboundFrame frame)
    {
        frame = default;
        lock (ReceiveLock)
        {
            if (Buffer.Length == 0 || Buffer[0] != 0xAA || Buffer.Length <= 3) return false;
            var packetLength = (Buffer[1] << 8) + Buffer[2] + 3;
            // Complete frame, pop it off and return it
            if (BytesReceived < packetLength) return false;
            BytesReceived -= packetLength;
            frame = InboundFrame.FromWire(ReceiveBufferPop(packetLength).ToArray());
            return true;
        }
    }
}