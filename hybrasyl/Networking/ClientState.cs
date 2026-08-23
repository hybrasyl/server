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
using Hybrasyl.Protocol.Framing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace Hybrasyl.Networking;

public class ClientState(ISocketProxy incoming) : IClientState
{
    private const int BufferSize = 65600;

    /// <summary>[0xAA][u16 body length] precedes every retail C→S frame.</summary>
    private const int FrameHeaderLength = 3;

    /// <summary>
    ///     The header plus the opcode <see cref="InboundFrame.FromWire" /> reads at index 3; a
    ///     shorter frame declares a zero-length body and carries no opcode at all.
    /// </summary>
    private const int MinimumFrameLength = FrameHeaderLength + 1;

    private readonly object _recvlock = new();

    private readonly object _sendlock = new();
    private ConcurrentQueue<InboundFrame> _receiveBuffer = new();
    private ConcurrentQueue<QueuedPacket> _sendBuffer = new();

    public TransportKind Transport { get; set; } = TransportKind.Undecided;
    public TlsSession? Tls { get; set; }
    public int BytesReceived { get; set; }
    public ManualResetEvent SendComplete { get; set; } = new(false);

    public int SendBufferDepth => _sendBuffer.Count;
    public bool Connected { get; set; } = true;
    public long Id { get; } = GlobalConnectionManifest.GetNewConnectionId();

    // Both getters walk the stack only when debug logging is on: they sit on the per-frame receive
    // path, where a StackFrame construction and its reflective GetMethod cost far more than the
    // lock they annotate.
    public object ReceiveLock
    {
        get
        {
            if (GameLog.IsDebugEnabled())
                GameLog.Debug(
                    "Receive lock acquired by: {Method} on thread {ThreadId}", new StackFrame(1).GetMethod()?.Name, Environment.CurrentManagedThreadId);
            return _recvlock;
        }
    }

    public object SendLock
    {
        get
        {
            if (GameLog.IsDebugEnabled())
                GameLog.Debug(
                    "Send lock acquired by: {Method} on thread {ThreadId}", new StackFrame(1).GetMethod()?.Name, Environment.CurrentManagedThreadId);
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

    /// <summary>
    ///     Removes the first <paramref name="range" /> bytes from the receive buffer, slides the
    ///     unconsumed remainder down to offset 0, updates <see cref="BytesReceived" />, and returns
    ///     the removed bytes. A buffer grown past <see cref="BufferSize" /> shrinks back once the
    ///     remainder fits.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Allocates the returned frame and moves only the bytes <see cref="BytesReceived" />
    ///         says are live. This runs once per frame on a socket that has not authenticated, so
    ///         any cost proportional to the buffer rather than to the frame is an amplification the
    ///         sender chooses.
    ///     </para>
    ///     <para>
    ///         The shrink is what returns a buffer <see cref="GrowReceiveBuffer" /> stretched for
    ///         one oversized extension frame; without it a single large claim pins its size for the
    ///         life of the connection.
    ///     </para>
    /// </remarks>
    public byte[] ReceiveBufferPop(int range)
    {
        lock (ReceiveLock)
        {
            var ret = new byte[range];
            Array.Copy(Buffer, 0, ret, 0, range);
            Compact(range);
            return ret;
        }
    }

    /// <summary>
    ///     Drops the first <paramref name="range" /> bytes without materializing them. The TLS pump
    ///     decodes an extension frame in place and wants only this half; copying it out first would
    ///     allocate and discard up to <c>Game.TlsMaxFrameSize</c> per frame.
    /// </summary>
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

        if (Buffer.Length > BufferSize && remaining <= BufferSize)
        {
            var shrunk = new byte[BufferSize];
            Buffer.AsSpan(range, remaining).CopyTo(shrunk);
            Buffer = shrunk;
        }
        else
        {
            if (remaining > 0) Array.Copy(Buffer, range, Buffer, 0, remaining);
            // Zeroing the vacated tail preserves what the pre-compaction implementation gave for
            // free by allocating a fresh buffer each pop; bounded by the frame, not the buffer, so
            // it stays proportional to what the sender actually sent.
            Array.Clear(Buffer, remaining, Math.Min(range, Buffer.Length - remaining));
        }

        BytesReceived = remaining;
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

    /// <summary>
    ///     Pops one complete retail frame off the front of the receive buffer, if one has fully
    ///     arrived. Callers route by first byte and must only call this on a retail frame.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     The frame declares a zero-length body, so it carries no opcode for
    ///     <see cref="InboundFrame.FromWire" /> to read. Callers disconnect: the boundary is
    ///     unrecoverable, and reading past it lets one malformed write be replayed indefinitely.
    /// </exception>
    public bool TryGetFrame(out InboundFrame frame)
    {
        frame = default;
        lock (ReceiveLock)
        {
            if (BytesReceived < FrameHeaderLength || Buffer[0] != FrameRouter.RetailMarker) return false;

            var packetLength = (Buffer[1] << 8) + Buffer[2] + FrameHeaderLength;
            if (packetLength < MinimumFrameLength)
                throw new InvalidDataException($"cid {Id}: retail frame declares a zero-length body");

            if (BytesReceived < packetLength) return false;

            frame = InboundFrame.FromWire(ReceiveBufferPop(packetLength));
            return true;
        }
    }

    public void GrowReceiveBuffer(int newSize)
    {
        lock (ReceiveLock)
        {
            if (newSize <= Buffer.Length) return;
            var grown = new byte[newSize];
            Buffer.AsSpan(0, BytesReceived).CopyTo(grown);
            Buffer = grown;
        }
    }

    public void ReceiveBufferAdd(InboundFrame frame)
    {
        _receiveBuffer.Enqueue(frame);
    }

    public bool ReceiveBufferTake(out InboundFrame frame) => _receiveBuffer.TryDequeue(out frame);
}