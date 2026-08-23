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

using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Protocol;
using Hybrasyl.Protocol.Framing;
using Hybrasyl.Protocol.Negotiation;
using Hybrasyl.Protocol.Transport;
using Hybrasyl.Protocol.Wire;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Hybrasyl.Networking;

/// <summary>
///     One connection's TLS channel after the STARTTLS upgrade: the handshake, the dialect
///     negotiation, the read pump that replaces the APM receive loop, and serialized writes.
///     Created by <c>Server.ReadCallback</c> when the transport probe sees a ClientHello;
///     lives on <see cref="IClientState.Tls" /> so the send path can route through it.
/// </summary>
public sealed class TlsSession
{
    /// <summary>The dialect range this server offers. V1-only until configuration grows a range.</summary>
    public static readonly ServerDialectPolicy Policy = ServerDialectPolicy.Create(Dialect.V1, Dialect.V1);

    /// <summary>
    ///     The typed extension codec, scanning Hybrasyl.Protocol plus this assembly for declared packets.
    ///     Stateless and shared process-wide, like <see cref="Client.Codec" />.
    /// </summary>
    internal static readonly ExtensionCodec ExtensionCodec =
        new([typeof(TlsSession).Assembly]);

    /// <summary>
    ///     Bounds the handshake plus negotiation, so a peer that sends 0x16 and stops cannot
    ///     pin a client, socket, stream, and task forever - no reaper watches pre-TLS
    ///     connections. Internal-settable for tests.
    /// </summary>
    internal static TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(10);

    private readonly SslStream _stream;
    private readonly int _maxFrameSize;

    /// <summary>
    ///     The codec bound to this connection's dialect, or null when none engaged. Null is the
    ///     invariant that no extension frame belongs here: the retail modes carry <c>0xAA</c>
    ///     frames, which are DALib's business, and <c>ForConnection</c> refuses to build one for
    ///     them.
    /// </summary>
    private readonly DialectCodec? _codec;

    // Guards _stream writes: FlushSendBuffer is invoked from ProcessOutbound's polling loop
    // and from flush-on-enqueue callers concurrently, and SslStream does not allow
    // overlapping writes. Reads need no guard - the pump is the only reader.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private TlsSession(SslStream stream, DialectResolution resolution, string clientVersion,
        int maxFrameSize)
    {
        _stream = stream;
        Resolution = resolution;
        ClientVersion = clientVersion;
        _maxFrameSize = maxFrameSize;
        _codec = resolution.Mode == ConnectionMode.DialectOverTls
            ? ExtensionCodec.ForConnection(resolution, maxFrameSize)
            : null;
    }

    /// <summary>The connection mode and dialect both sides derived from (offer, choice).</summary>
    public DialectResolution Resolution { get; }

    /// <summary>The client version string reported in its DialectChoice.</summary>
    public string ClientVersion { get; }

    /// <summary>
    ///     Upgrades <paramref name="client" />'s connection: TLS handshake (with
    ///     <paramref name="clientHello" /> — the bytes the receive loop already pulled off the
    ///     socket — replayed to <c>SslStream</c> verbatim), dialect negotiation, then the read
    ///     pump until disconnect. Never throws; any failure disconnects the client.
    /// </summary>
    /// <remarks>
    ///     The caller must not have re-armed the APM receive: from here the pump is the socket's
    ///     only reader, and <c>Buffer</c>/<c>BytesReceived</c> are pump-confined.
    /// </remarks>
    public static async Task StartAsync(IClient client, ReadOnlyMemory<byte> clientHello,
        X509Certificate2 certificate, int maxFrameSize)
    {
        try
        {
            var stream = new SslStream(
                new PrefixReplayStream(clientHello, client.Socket.GetStream()),
                leaveInnerStreamOpen: false);

            await using (stream.ConfigureAwait(false))
            {
                await stream.AuthenticateAsServerAsync(TlsConfig.ServerOptions(certificate))
                    .WaitAsync(HandshakeTimeout).ConfigureAwait(false);

                // ServerOptions leaves the enabled protocols at the platform default, so "TLS 1.3
                // only" holds only if it is checked here; nothing has crossed the channel yet.
                TlsChannel.VerifyNegotiatedProtocol(stream);

                var negotiated = await DialectNegotiator.NegotiateAsServerAsync(stream, Policy)
                    .WaitAsync(HandshakeTimeout).ConfigureAwait(false);

                var session = new TlsSession(stream, negotiated.Resolution,
                    negotiated.Choice.ClientVersion, maxFrameSize);
                client.ClientState.Tls = session;

                GameLog.Info(
                    "cid {ConnectionId}: TLS established, mode {Mode}, dialect {Dialect}, client {ClientVersion}",
                    client.ConnectionId, negotiated.Resolution.Mode, negotiated.Resolution.Dialect,
                    negotiated.Choice.ClientVersion);

                await session.PumpAsync(client).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            GameLog.Warning(e, "cid {ConnectionId}: TLS session ended by error", client.ConnectionId);
        }
        finally
        {
            if (client.Connected) client.Disconnect();
        }
    }

    /// <summary>
    ///     Encodes and sends a native extension packet on the negotiated dialect, fire-and-forget
    ///     with failures logged - the shape handlers need. Requires an engaged dialect.
    /// </summary>
    /// <exception cref="InvalidOperationException">The connection resolved to a retail mode; it
    ///     has no dialect to stamp frames with.</exception>
    public void Send(IExtensionServerPacket packet)
    {
        if (_codec is not { } codec)
            throw new InvalidOperationException(
                $"Cannot send extension packet 0x{packet.Opcode:X4} on a {Resolution.Mode} connection.");

        var wire = codec.EncodeServer(packet);
        _ = Task.Run(async () =>
        {
            try
            {
                await WriteAsync(wire).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                GameLog.Warning(e, "extension send 0x{Opcode:X4} failed", packet.Opcode);
            }
        });
    }

    /// <summary>Writes one already-encoded buffer to the channel, serialized against other writers.</summary>
    public async Task WriteAsync(ReadOnlyMemory<byte> wire)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(wire).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    ///     The post-upgrade receive loop: decrypted bytes land in the same
    ///     <c>Buffer</c>/<c>BytesReceived</c> accounting the APM path uses, and complete frames
    ///     drain through the same machinery.
    /// </summary>
    private async Task PumpAsync(IClient client)
    {
        var state = client.ClientState;

        while (state.Connected)
        {
            if (state.BytesReceived == state.Buffer.Length && !TryGrowForFrontFrame(client))
            {
                GameLog.Warning(
                    "cid {ConnectionId}: receive buffer full without a complete frame, disconnecting",
                    client.ConnectionId);
                return;
            }

            var read = await _stream.ReadAsync(
                    state.Buffer.AsMemory(state.BytesReceived, state.Buffer.Length - state.BytesReceived))
                .ConfigureAwait(false);
            if (read == 0) return;

            state.BytesReceived += read;
            Drain(client);
        }
    }

    /// <summary>
    ///     A full buffer whose front frame is an incomplete extension frame grows to exactly
    ///     that frame's total size (Drain has already validated the claimed length against
    ///     <see cref="_maxFrameSize" /> - an oversized claim threw there). The pop that consumes
    ///     the frame shrinks the buffer back. A full buffer fronted by a retail frame cannot
    ///     legally need more room and stays fatal.
    /// </summary>
    private bool TryGrowForFrontFrame(IClient client)
    {
        var state = client.ClientState;
        if (state.Buffer[0] == FrameRouter.RetailMarker) return false;

        var claimed = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(state.Buffer);
        if (ExtensionFrame.LengthFieldLength + (long)claimed > _maxFrameSize) return false;

        state.GrowReceiveBuffer(ExtensionFrame.LengthFieldLength + (int)claimed);

        return true;
    }

    /// <summary>
    ///     Routes complete frames off the front of the buffer by their first byte: retail
    ///     <c>0xAA</c> frames through the existing framing/dispatch machinery (the dialect is
    ///     additive — un-migrated packets keep retail framing inside TLS), anything else through
    ///     the extension codec into the server's u16 handler table. A malformed frame, an
    ///     unregistered opcode, or an extension frame on a retail-mode connection throws to the
    ///     pump's handler and disconnects — the frame boundary is unrecoverable in the first
    ///     case and the peer is in protocol violation in the others.
    /// </summary>
    private void Drain(IClient client)
    {
        var state = client.ClientState;

        while (state.BytesReceived > 0)
        {
            if (state.Buffer[0] == FrameRouter.RetailMarker)
            {
                if (!state.TryGetFrame(out var frame)) return;
                client.ReceiveFrame(frame);
                continue;
            }

            // No bound codec means no dialect engaged, so no extension frame belongs here. The
            // codec enforces the negotiated signature itself, at the framing layer, before a
            // foreign frame's claimed body is buffered.
            if (_codec is not { } codec)
                throw new System.IO.InvalidDataException(
                    $"Extension frame received on a {Resolution.Mode} connection.");

            if (!codec.TryDecodeClient(state.Buffer.AsMemory(0, state.BytesReceived),
                    out var packet, out var consumed))
                return;

            state.ReceiveBufferPop(consumed);
            state.BytesReceived -= consumed;
            client.ReceiveExtensionPacket(packet!);
        }
    }
}
