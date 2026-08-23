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

using Hybrasyl.Networking;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Hybrasyl.Tests;

/// <summary>
///     The STARTTLS clean-buffer invariant, as a stream property: a reader consuming a
///     <see cref="PrefixReplayStream" /> must observe exactly the prefix bytes followed by
///     exactly the inner bytes, however the reads are sized — because the reader in production
///     is <c>SslStream</c> reassembling a ClientHello, and any duplicated, dropped, or
///     reordered byte is a broken handshake at best.
/// </summary>
public class PrefixReplayStreamTests
{
    private static readonly byte[] Prefix = [0x16, 0x03, 0x01, 0x02];
    private static readonly byte[] InnerBytes = [0xDE, 0xAD, 0xBE, 0xEF, 0x42];

    private static PrefixReplayStream Make() =>
        new(Prefix, new MemoryStream(InnerBytes));

    /// <summary>Drains the stream with reads of <paramref name="readSize" /> bytes.</summary>
    private static byte[] DrainWithReadSize(Stream stream, int readSize)
    {
        using var drained = new MemoryStream();
        var buffer = new byte[readSize];
        int read;
        while ((read = stream.Read(buffer, 0, readSize)) > 0)
            drained.Write(buffer, 0, read);

        return drained.ToArray();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(64)]
    public void Read_ObservesPrefixThenInner_AtAnyReadSize(int readSize)
    {
        using var stream = Make();

        Assert.Equal([.. Prefix, .. InnerBytes], DrainWithReadSize(stream, readSize));
    }

    [Fact]
    public void Read_DoesNotCoalescePrefixAndInnerInOneRead()
    {
        using var stream = Make();
        var buffer = new byte[64];

        var first = stream.Read(buffer, 0, buffer.Length);

        Assert.Equal(Prefix.Length, first);
        Assert.Equal(Prefix, buffer[..first]);
    }

    [Fact]
    public void Read_WithEmptyPrefix_DelegatesToInner()
    {
        using var stream = new PrefixReplayStream(ReadOnlyMemory<byte>.Empty,
            new MemoryStream(InnerBytes));
        var buffer = new byte[64];

        var read = stream.Read(buffer, 0, buffer.Length);

        Assert.Equal(InnerBytes.Length, read);
        Assert.Equal(InnerBytes, buffer[..read]);
    }

    [Fact]
    public async Task ReadAsync_ObservesPrefixThenInner()
    {
        using var stream = Make();
        using var drained = new MemoryStream();
        var buffer = new byte[3];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory())) > 0)
            drained.Write(buffer, 0, read);

        Assert.Equal([.. Prefix, .. InnerBytes], drained.ToArray());
    }

    [Fact]
    public void Write_PassesThroughToInner_RegardlessOfUnservedPrefix()
    {
        var inner = new MemoryStream();
        using var stream = new PrefixReplayStream(Prefix, inner);

        stream.Write([0x01, 0x02], 0, 2);

        Assert.Equal(new byte[] { 0x01, 0x02 }, inner.ToArray());
        Assert.Equal(Prefix.Length, stream.PrefixRemaining);
    }

    [Fact]
    public void PrefixRemaining_TracksConsumption()
    {
        using var stream = Make();
        var buffer = new byte[3];

        Assert.Equal(Prefix.Length, stream.PrefixRemaining);
        stream.Read(buffer, 0, 3);
        Assert.Equal(Prefix.Length - 3, stream.PrefixRemaining);
        stream.Read(buffer, 0, 3);
        Assert.Equal(0, stream.PrefixRemaining);
    }
}
