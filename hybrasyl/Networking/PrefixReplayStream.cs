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
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hybrasyl.Networking;

/// <summary>
///     A duplex stream whose reads serve a fixed prefix before falling through to the inner
///     stream; writes pass straight through. This is the STARTTLS handoff seam: the ClientHello
///     bytes the receive loop has already pulled off the socket are replayed to
///     <c>SslStream</c> exactly as they arrived, so the handshake sees the same byte sequence
///     the wire carried and no pre-TLS byte is ever consumed as application data.
/// </summary>
/// <remarks>
///     A read never coalesces prefix and inner bytes: while any prefix remains, reads are
///     satisfied from it alone. Callers that want more loop, which every stream consumer
///     (including <c>SslStream</c>) already does.
/// </remarks>
public sealed class PrefixReplayStream : Stream
{
    private readonly Stream _inner;
    private readonly ReadOnlyMemory<byte> _prefix;
    private int _position;

    public PrefixReplayStream(ReadOnlyMemory<byte> prefix, Stream inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _prefix = prefix;
        _inner = inner;
    }

    /// <summary>Bytes of the prefix not yet served.</summary>
    public int PrefixRemaining => _prefix.Length - _position;

    public override bool CanRead => _inner.CanRead;
    public override bool CanWrite => _inner.CanWrite;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (PrefixRemaining == 0) return _inner.Read(buffer);

        var served = Math.Min(PrefixRemaining, buffer.Length);
        _prefix.Span.Slice(_position, served).CopyTo(buffer);
        _position += served;

        return served;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        // Serving from the in-memory prefix needs no async machinery, and sharing the sync path
        // keeps the two from drifting on the offset arithmetic.
        if (PrefixRemaining > 0) return ValueTask.FromResult(Read(buffer.Span));

        return _inner.ReadAsync(buffer, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        _inner.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);

    public override Task WriteAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken) =>
        _inner.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(buffer, cancellationToken);

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}
