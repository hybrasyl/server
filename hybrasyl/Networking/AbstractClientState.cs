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
using Hybrasyl.Protocol.Framing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;

namespace Hybrasyl.Networking;

public abstract class AbstractClientState
{
    private const int BufferSize = 65600;

    /// <summary>[0xAA][u16 body length] precedes every retail C→S frame.</summary>
    private const int FrameHeaderLength = 3;

    /// <summary>
    ///     The header plus the opcode <see cref="InboundFrame.FromWire" /> reads at index 3; a
    ///     shorter frame declares a zero-length body and carries no opcode at all.
    /// </summary>
    private const int MinimumFrameLength = FrameHeaderLength + 1;

    private readonly ConcurrentQueue<InboundFrame> _receiveBuffer = new();
    private readonly object _receiveLock = new();
    private readonly object _sendLock = new();

    private byte[] _buffer = new byte[BufferSize];
    private ConcurrentQueue<QueuedPacket> _sendBuffer = new();
    public TransportKind Transport { get; set; } = TransportKind.Undecided;
    public TlsSession? Tls { get; set; }
    public int BytesReceived { get; set; }

    public byte[] Buffer => _buffer;

    // Both getters walk the stack only when debug logging is on, matching ClientState: a StackFrame
    // construction and its reflective GetMethod cost far more than the lock they annotate.
    public object ReceiveLock
    {
        get
        {
            if (GameLog.IsDebugEnabled())
                GameLog.Debug(
                    "Receive lock acquired by: {Method} on thread {ThreadId}", new StackFrame(1).GetMethod()?.Name, Environment.CurrentManagedThreadId);
            return _receiveLock;
        }
    }

    public object SendLock
    {
        get
        {
            if (GameLog.IsDebugEnabled())
                GameLog.Debug(
                    "Send lock acquired by: {Method} on thread {ThreadId}", new StackFrame(1).GetMethod()?.Name, Environment.CurrentManagedThreadId);
            return _sendLock;
        }
    }

    public void GrowReceiveBuffer(int newSize)
    {
        lock (ReceiveLock)
        {
            if (newSize <= _buffer.Length) return;
            var grown = new byte[newSize];
            _buffer.AsSpan(0, BytesReceived).CopyTo(grown);
            _buffer = grown;
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

    public byte[] ReceiveBufferPop(int range)
    {
        lock (ReceiveLock)
        {
            var ret = new byte[range];
            Array.Copy(_buffer, 0, ret, 0, range);
            Compact(range);
            return ret;
        }
    }

    public void ReceiveBufferConsume(int range)
    {
        lock (ReceiveLock)
        {
            Compact(range);
        }
    }

    /// <remarks>Callers hold <see cref="ReceiveLock" />.</remarks>
    private void Compact(int range)
    {
        var remaining = Math.Max(0, BytesReceived - range);

        if (_buffer.Length > BufferSize && remaining <= BufferSize)
        {
            var shrunk = new byte[BufferSize];
            _buffer.AsSpan(range, remaining).CopyTo(shrunk);
            _buffer = shrunk;
        }
        else
        {
            if (remaining > 0) Array.Copy(_buffer, range, _buffer, 0, remaining);
            Array.Clear(_buffer, remaining, Math.Min(range, _buffer.Length - remaining));
        }

        BytesReceived = remaining;
    }

    public void SendBufferAdd(QueuedPacket packet) => _sendBuffer.Enqueue(packet);
    public bool SendBufferPeek(out QueuedPacket packet) => _sendBuffer.TryPeek(out packet);
    public bool SendBufferTake(out QueuedPacket packet) => _sendBuffer.TryDequeue(out packet);

    public void ResetReceive()
    {
        lock (ReceiveLock)
        {
            _buffer = new byte[BufferSize];
        }
    }

    public void ResetSend()
    {
        _sendBuffer = new ConcurrentQueue<QueuedPacket>();
    }

    public bool TryGetFrame(out InboundFrame frame)
    {
        frame = default;
        lock (ReceiveLock)
        {
            if (BytesReceived < FrameHeaderLength || Buffer[0] != FrameRouter.RetailMarker) return false;

            var packetLength = (Buffer[1] << 8) + Buffer[2] + FrameHeaderLength;
            if (packetLength < MinimumFrameLength)
                throw new InvalidDataException("retail frame declares a zero-length body");

            if (BytesReceived < packetLength) return false;

            frame = InboundFrame.FromWire(ReceiveBufferPop(packetLength));
            return true;
        }
    }
}