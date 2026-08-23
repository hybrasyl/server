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

using DALib.Networking.Crypto;
using DALib.Networking.Packets.Client;
using DALib.Networking.Packets.Server;
using Hybrasyl.Networking;
using Hybrasyl.Protocol;
using Hybrasyl.Protocol.Framing;
using Hybrasyl.Protocol.Negotiation;
using Hybrasyl.Protocol.Packets;
using Hybrasyl.Protocol.Transport;
using Hybrasyl.Protocol.Wire;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The STARTTLS upgrade end-to-end (HS-1621 slice 4), over a real loopback socket pair with
///     a real handshake: ClientHello in at the real <c>ReadCallback</c>, prefix-replayed into
///     <c>SslStream</c>, dialect negotiated, then frames both directions. A stream double
///     cannot stand in here - the prefix-replay handoff is exactly the thing only a live
///     handshake exercises.
/// </summary>
[Collection("Hybrasyl")]
public class TlsUpgradeWiring
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public TlsUpgradeWiring(HybrasylFixture fixture) { }

    private static X509Certificate2 MakeCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        // Same PKCS#12 round-trip as TlsCertificateLoader: SChannel rejects the ephemeral key
        // on Windows, and this suite runs there too.
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12),
            password: null);
    }

    /// <summary>
    ///     A connected loopback pair: the server side is a real <see cref="Client" /> whose
    ///     first receive is armed with the real <c>ReadCallback</c>; the client side is an
    ///     upgraded, negotiated <see cref="SslStream" />.
    /// </summary>
    private sealed class Loopback : IDisposable
    {
        public Client ServerClient = null!;
        public SslStream ClientStream = null!;
        public ClientNegotiationResult Negotiation;
        private TcpListener _listener = null!;
        private TcpClient _tcp = null!;

        public static async Task<Loopback> ConnectAsync(ClientDialectPolicy clientPolicy,
            string clientVersion = "test/1.0", int maxFrameSize = 1_048_576)
        {
            var loopback = await AcceptAsync(maxFrameSize);

            await loopback.UpgradeClientAsync(
                TlsConfig.ClientOptions("localhost", (_, _, _, _) => true));
            loopback.Negotiation = await DialectNegotiator
                .NegotiateAsClientAsync(loopback.ClientStream, clientPolicy, clientVersion)
                .WaitAsync(Timeout);

            return loopback;
        }

        /// <summary>
        ///     A connected pair with the server side armed and nothing sent yet, for tests that
        ///     drive the client handshake themselves.
        /// </summary>
        public static async Task<Loopback> AcceptAsync(int maxFrameSize = 1_048_576)
        {
            var loopback = new Loopback();
            Game.TlsCertificate = MakeCertificate();
            Game.TlsMaxFrameSize = maxFrameSize;

            loopback._listener = new TcpListener(IPAddress.Loopback, 0);
            loopback._listener.Start();
            loopback._tcp = new TcpClient();
            var connect = loopback._tcp.ConnectAsync(IPAddress.Loopback,
                ((IPEndPoint)loopback._listener.LocalEndpoint).Port);
            var accepted = await loopback._listener.AcceptSocketAsync().WaitAsync(Timeout);
            await connect.WaitAsync(Timeout);

            var proxy = new SocketProxy(accepted);
            loopback.ServerClient = new Client(proxy, Game.Login)
            {
                EncryptionKey = "UrkcnItnI"u8.ToArray()
            };
            var state = loopback.ServerClient.ClientState;
            proxy.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None,
                Game.Login.ReadCallback, state);

            return loopback;
        }

        /// <summary>Runs the client half of the handshake with the given options.</summary>
        public Task UpgradeClientAsync(SslClientAuthenticationOptions options)
        {
            ClientStream = new SslStream(_tcp.GetStream(), leaveInnerStreamOpen: false);
            return ClientStream.AuthenticateAsClientAsync(options).WaitAsync(Timeout);
        }

        /// <summary>The server session, waiting for the upgrade task to publish it.</summary>
        public async Task<TlsSession> ServerSessionAsync()
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (ServerClient.ClientState.Tls is { } session) return session;
                await Task.Delay(10);
            }

            throw new TimeoutException("the server never published a TLS session");
        }

        /// <summary>Waits for the server to notice a disconnect the client provoked.</summary>
        public async Task<bool> ServerDisconnectedAsync()
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (ServerClient.Connected && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            return !ServerClient.Connected;
        }

        public void Dispose()
        {
            Game.TlsCertificate = null;
            Game.TlsMaxFrameSize = 1_048_576;
            GlobalConnectionManifest.DeregisterClient(ServerClient);
            if (ServerClient.Connected) ServerClient.Disconnect();
            ClientStream?.Dispose();
            _tcp.Dispose();
            _listener.Stop();
        }
    }

    private static ClientJoinPacket Join() => new()
    {
        EncryptionSeed = 0x0B,
        EncryptionKey = "NILCHIRSTNA"u8.ToArray(),
        Name = "Kerden",
        RedirectId = 0x12345678,
    };

    /// <summary>Sends a retail frame through the TLS stream and waits for handler delivery.</summary>
    private static async Task<byte[]> DeliverOverTlsAsync(Loopback loopback, ClientJoinPacket packet)
    {
        var delivered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var original = Game.Login.PacketHandlers[packet.Opcode];

        try
        {
            Game.Login.PacketHandlers[packet.Opcode] = (_, p) => delivered.TrySetResult(p.Body.ToArray());

            var wire = Client.Codec.EncodeClient(packet, loopback.ServerClient.Crypto).ToArray();
            await loopback.ClientStream.WriteAsync(wire).AsTask().WaitAsync(Timeout);
            await loopback.ClientStream.FlushAsync().WaitAsync(Timeout);

            return await delivered.Task.WaitAsync(Timeout);
        }
        finally
        {
            Game.Login.PacketHandlers[packet.Opcode] = original;
        }
    }

    /// <summary>
    ///     A client that can only offer TLS 1.2 gets no session. Since Hybrasyl.Protocol 0.0.4 the
    ///     handshake options ask the platform for nothing in particular - macOS refuses an explicit
    ///     request above 1.2 - so the server's post-handshake check is the only thing holding
    ///     "TLS 1.3 only".
    /// </summary>
    [Fact]
    public async Task Tls12Client_IsRefused_WithNoSessionPublished()
    {
        using var loopback = await Loopback.AcceptAsync();

        var options = new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            EnabledSslProtocols = SslProtocols.Tls12,
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
        };

        // The server finishes the 1.2 handshake before refusing it, so the client's own
        // authenticate may return successfully or fail on the drop; either is consistent with the
        // server-side invariant under test.
        try
        {
            await loopback.UpgradeClientAsync(options);
        }
        catch (Exception e) when (e is AuthenticationException or IOException)
        {
        }

        Assert.True(await loopback.ServerDisconnectedAsync());
        Assert.Null(loopback.ServerClient.ClientState.Tls);
    }

    [Fact]
    public async Task ClientHello_Upgrades_NegotiatesV1_AndDeliversARetailFrameInsideTls()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1));

        var session = await loopback.ServerSessionAsync();
        Assert.Equal(ConnectionMode.DialectOverTls, session.Resolution.Mode);
        Assert.Equal(Dialect.V1, session.Resolution.Dialect);
        Assert.Equal(ConnectionMode.DialectOverTls, loopback.Negotiation.Resolution.Mode);
        Assert.Equal("test/1.0", session.ClientVersion);
        Assert.Equal(TransportKind.Tls, loopback.ServerClient.ClientState.Transport);

        var packet = Join();
        var delivered = await DeliverOverTlsAsync(loopback, packet);
        Assert.Equal(packet.ToBody(), delivered);
    }

    /// <summary>
    ///     A client whose dialect is outside the server's range gets retail semantics inside TLS
    ///     - the 0xAA-over-TLS mode. Retail frames must flow exactly as in the engaged mode.
    /// </summary>
    [Fact]
    public async Task OutOfRangeClient_ResolvesRetailOverTls_AndRetailFramesStillFlow()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy((Dialect)0xB1));

        var session = await loopback.ServerSessionAsync();
        Assert.Equal(ConnectionMode.RetailOverTls, session.Resolution.Mode);
        Assert.Null(session.Resolution.Dialect);

        var packet = Join();
        var delivered = await DeliverOverTlsAsync(loopback, packet);
        Assert.Equal(packet.ToBody(), delivered);
    }

    /// <summary>The client-side role: reads one extension frame off the TLS stream.</summary>
    private static async Task<IExtensionServerPacket> ReadExtensionPacketAsync(SslStream stream)
    {
        var codec = new ExtensionCodec();
        var lengthField = new byte[4];
        await stream.ReadExactlyAsync(lengthField).AsTask().WaitAsync(Timeout);
        var rest = new byte[System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(lengthField)];
        await stream.ReadExactlyAsync(rest).AsTask().WaitAsync(Timeout);

        byte[] whole = [.. lengthField, .. rest];
        Assert.True(codec.TryDecodeServer(whole, out var packet, out _, Dialect.V1));
        return packet!;
    }

    [Fact]
    public async Task ClientEcho_IsAnsweredWithTheSameExchangeEchoingTheToken()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1));
        await loopback.ServerSessionAsync();

        var wire = new ExtensionCodec().EncodeClient(new ClientEcho(0xDEADBEEFCAFEF00D), Dialect.V1);
        await loopback.ClientStream.WriteAsync(wire).AsTask().WaitAsync(Timeout);

        var reply = await ReadExtensionPacketAsync(loopback.ClientStream);

        var echo = Assert.IsType<ClientEcho>(reply);
        Assert.Equal(0xDEADBEEFCAFEF00D, echo.Token);
        Assert.True(loopback.ServerClient.Connected);
    }

    /// <summary>
    ///     A retail frame behind an extension frame still delivers - the two framings coexist
    ///     on the stream without desync.
    /// </summary>
    [Fact]
    public async Task RetailFrameBehindAnExtensionFrame_StillDelivers()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1));
        await loopback.ServerSessionAsync();

        var ping = new ExtensionCodec().EncodeClient(new ClientEcho(1), Dialect.V1);
        await loopback.ClientStream.WriteAsync(ping).AsTask().WaitAsync(Timeout);

        var packet = Join();
        var delivered = await DeliverOverTlsAsync(loopback, packet);

        Assert.Equal(packet.ToBody(), delivered);
        Assert.True(loopback.ServerClient.Connected);
    }

    /// <summary>
    ///     An extension frame from a connection that resolved to retail-over-TLS is a protocol
    ///     violation: the mode is derived, not signaled, and both sides know it.
    /// </summary>
    [Fact]
    public async Task ExtensionFrame_OnARetailOverTlsConnection_Disconnects()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy((Dialect)0xB1));
        await loopback.ServerSessionAsync();

        var ping = new ExtensionCodec().EncodeClient(new ClientEcho(1), Dialect.V1);
        await loopback.ClientStream.WriteAsync(ping).AsTask().WaitAsync(Timeout);

        Assert.True(await loopback.ServerDisconnectedAsync(),
            "an extension frame on a retail-mode connection must disconnect");
    }

    [Fact]
    public async Task UnregisteredExtensionOpcode_Disconnects()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1));
        await loopback.ServerSessionAsync();

        var frame = ExtensionFrameCodec.WriteFrame(Dialect.V1, opcode: 0x0FFF, body: [1, 2, 3]);
        await loopback.ClientStream.WriteAsync(frame).AsTask().WaitAsync(Timeout);

        Assert.True(await loopback.ServerDisconnectedAsync(),
            "an opcode outside the shared registry must disconnect");
    }

    /// <summary>
    ///     A frame larger than the 65600-byte receive buffer, but under MaxFrameSize, grows the
    ///     buffer to hold it. Growth is asserted on the buffer itself because every v1 packet body
    ///     is a fixed 8 bytes, so no frame this size can also parse - the packet is rejected after
    ///     the grow, and the shrink lives in the pop that consequently never runs.
    /// </summary>
    /// <remarks>
    ///     The dispatch half of this path is untested as of 2026-08-12: it needs a registered
    ///     packet whose body may exceed the receive buffer, and none exists (HS-1628).
    /// </remarks>
    [Fact]
    public async Task ExtensionFrameLargerThanTheReceiveBuffer_GrowsTheReceiveBuffer()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1));
        await loopback.ServerSessionAsync();
        var state = loopback.ServerClient.ClientState;
        var initial = state.Buffer.Length;

        var frame = ExtensionFrameCodec.WriteFrame(Dialect.V1,
            Hybrasyl.Protocol.Wire.ExtensionOpcodes.ClientEcho, new byte[100_000]);

        // Withhold the tail. The pump cannot parse an incomplete frame, so it sits grown and
        // idle for as long as we like - which is what makes the observation deterministic
        // rather than a race against the teardown that follows the parse.
        await loopback.ClientStream.WriteAsync(frame.AsMemory(0, 70_000)).AsTask().WaitAsync(Timeout);
        await loopback.ClientStream.FlushAsync().WaitAsync(Timeout);

        var deadline = DateTime.UtcNow + Timeout;
        while (state.Buffer.Length < frame.Length && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(state.Buffer.Length >= frame.Length,
            $"receive buffer stayed at {initial}: the frame was never grown to hold it, so the " +
            "pump would reject it as buffer-full rather than exercising TryGrowForFrontFrame");
        Assert.True(loopback.ServerClient.Connected, "an incomplete frame must not disconnect");

        await loopback.ClientStream.WriteAsync(frame.AsMemory(70_000)).AsTask().WaitAsync(Timeout);
        Assert.True(await loopback.ServerDisconnectedAsync(),
            "once whole, the frame fails ClientEcho's fixed-8-byte body rule");
    }

    /// <summary>
    ///     The codec resolves shapes against the frame's own signature, so accepting a
    ///     signature other than the negotiated one would bypass negotiation the day a later
    ///     dialect exists. Stamped-wrong frames are fatal.
    /// </summary>
    [Fact]
    public async Task FrameStampedWithADifferentDialect_Disconnects()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1));
        await loopback.ServerSessionAsync();

        var frame = ExtensionFrameCodec.WriteFrame((Dialect)0xB1,
            Hybrasyl.Protocol.Wire.ExtensionOpcodes.ClientEcho, new byte[8]);
        await loopback.ClientStream.WriteAsync(frame).AsTask().WaitAsync(Timeout);

        Assert.True(await loopback.ServerDisconnectedAsync(),
            "a frame stamped with a non-negotiated dialect must disconnect");
    }

    /// <summary>
    ///     A peer that sends 0x16 and stops must not pin a client, socket, and task forever -
    ///     no reaper watches pre-TLS connections, so the handshake carries its own deadline.
    /// </summary>
    [Fact]
    public async Task SilentPeerAfterClientHelloByte_IsReapedByTheHandshakeTimeout()
    {
        var savedTimeout = TlsSession.HandshakeTimeout;
        TlsSession.HandshakeTimeout = TimeSpan.FromMilliseconds(500);
        Game.TlsCertificate = MakeCertificate();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var tcp = new TcpClient();
        Client? client = null;

        try
        {
            var connect = tcp.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            var accepted = await listener.AcceptSocketAsync().WaitAsync(Timeout);
            await connect.WaitAsync(Timeout);

            var proxy = new SocketProxy(accepted);
            client = new Client(proxy, Game.Login);
            var state = client.ClientState;
            proxy.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None,
                Game.Login.ReadCallback, state);

            await tcp.GetStream().WriteAsync(new byte[] { 0x16 }).AsTask().WaitAsync(Timeout);
            // ... and say nothing more.

            var deadline = DateTime.UtcNow + Timeout;
            while (client.Connected && DateTime.UtcNow < deadline)
                await Task.Delay(25);

            Assert.False(client.Connected, "the silent peer was never reaped");
        }
        finally
        {
            TlsSession.HandshakeTimeout = savedTimeout;
            Game.TlsCertificate = null;
            if (client is not null)
            {
                GlobalConnectionManifest.DeregisterClient(client);
                if (client.Connected) client.Disconnect();
            }
            tcp.Dispose();
            listener.Stop();
        }
    }

    /// <summary>
    ///     MaxFrameSize is total wire size: a frame of exactly the cap passes. The cap here is 16
    ///     rather than the config floor because every v1 packet body is a fixed 8 bytes, so 16 is
    ///     the only total an accepted frame can have - the boundary is what is under test, not the
    ///     magnitude.
    /// </summary>
    [Fact]
    public async Task FrameOfExactlyMaxFrameSize_Dispatches()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1),
            maxFrameSize: 16);
        await loopback.ServerSessionAsync();

        var frame = new ExtensionCodec().EncodeClient(new ClientEcho(42UL), Dialect.V1);
        Assert.Equal(16, frame.Length);
        await loopback.ClientStream.WriteAsync(frame).AsTask().WaitAsync(Timeout);

        var echo = Assert.IsType<ClientEcho>(await ReadExtensionPacketAsync(loopback.ClientStream));
        Assert.Equal(42UL, echo.Token);
    }

    [Fact]
    public async Task FrameOneByteOverMaxFrameSize_Disconnects()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1),
            maxFrameSize: 65546);
        await loopback.ServerSessionAsync();

        var frame = ExtensionFrameCodec.WriteFrame(Dialect.V1,
            Hybrasyl.Protocol.Wire.ExtensionOpcodes.ClientEcho, new byte[65546 - 8 + 1]);
        Assert.Equal(65547, frame.Length);
        await loopback.ClientStream.WriteAsync(frame).AsTask().WaitAsync(Timeout);

        Assert.True(await loopback.ServerDisconnectedAsync(),
            "a frame one byte over the cap must disconnect");
    }

    /// <summary>
    ///     Between the probe deciding TLS and the session publishing, a flush must not touch
    ///     the socket - a raw write would land plaintext inside the TLS record stream. The
    ///     queue holds and drains after the session appears.
    /// </summary>
    [Fact]
    public void EnqueueDuringTheHandshakeWindow_DoesNotTouchTheSocket()
    {
        var socket = new TestSocket();
        var client = new Client(socket, Game.Login);

        try
        {
            client.ClientState.Transport = TransportKind.Tls;
            client.Enqueue(new AcceptConnectionPacket { Message = "hold" }, flush: true);

            Assert.False(socket.TryTakeSent(TimeSpan.FromMilliseconds(300), out _),
                "a flush during the handshake window wrote raw bytes to the socket");

            // Once a transport owns the connection again, the held queue drains.
            client.ClientState.Transport = TransportKind.Plaintext;
            client.FlushSendBuffer();
            Assert.True(socket.TryTakeSent(Timeout, out _), "the held queue never drained");
        }
        finally
        {
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    /// <summary>
    ///     A flush scheduled with a transmit delay while the transport was still undecided
    ///     must not raw-write after the probe chooses TLS: the transport is re-read at
    ///     dispatch, and Tls means await-the-session-or-abandon, never the raw socket.
    /// </summary>
    [Fact]
    public void DelayedSendScheduledBeforeTlsDecision_DoesNotRawWriteAfterIt()
    {
        var socket = new TestSocket();
        var client = new Client(socket, Game.Login);

        try
        {
            // Undecided at enqueue: the flush passes the entry guard, dequeues and encodes,
            // and schedules the delayed dispatch.
            client.Enqueue(new AcceptConnectionPacket { Message = "raced" }, flush: true,
                transmitDelay: 250);

            // The probe decides TLS during the delay; the session is not yet published.
            client.ClientState.Transport = TransportKind.Tls;

            Assert.False(socket.TryTakeSent(TimeSpan.FromSeconds(1), out _),
                "a delayed dispatch raw-wrote plaintext after the transport became TLS");
        }
        finally
        {
            client.Disconnect();
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    [Fact]
    public async Task FrameClaimingMoreThanMaxFrameSize_Disconnects()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1),
            maxFrameSize: 65546);
        await loopback.ServerSessionAsync();

        var frame = ExtensionFrameCodec.WriteFrame(Dialect.V1,
            Hybrasyl.Protocol.Wire.ExtensionOpcodes.ClientEcho, new byte[100_000]);
        await loopback.ClientStream.WriteAsync(frame).AsTask().WaitAsync(Timeout);

        Assert.True(await loopback.ServerDisconnectedAsync(),
            "a frame claiming more than the configured MaxFrameSize must disconnect");
    }

    [Fact]
    public async Task ServerEnqueue_ReachesTheClientThroughTls()
    {
        using var loopback = await Loopback.ConnectAsync(new ClientDialectPolicy(Dialect.V1));
        await loopback.ServerSessionAsync();

        // Unencrypted opcode so the expected wire can be derived from a fresh CryptoState
        // without perturbing the connection's ordinal bookkeeping.
        var packet = new AcceptConnectionPacket { Message = "CONNECTED SERVER\n" };
        var expected = Client.Codec.EncodeServer(packet, new CryptoState()).ToArray();

        loopback.ServerClient.Enqueue(packet, flush: true);

        var received = new byte[expected.Length];
        await loopback.ClientStream.ReadExactlyAsync(received).AsTask().WaitAsync(Timeout);

        Assert.Equal(expected, received);
    }
}
