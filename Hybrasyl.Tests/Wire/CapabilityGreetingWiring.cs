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

using DALib.Networking.Packets.Server;
using Hybrasyl.Networking;
using Hybrasyl.Protocol.Negotiation;
using Hybrasyl.Servers;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The capability marker on the lobby's 0x7E greeting (HS-1621 slice 6) — the go-live
///     switch. Driven through the real <c>Server.AcceptConnection</c>, because the gate is one
///     expression at that call site: testing the packet type alone would leave deleting the
///     gate entirely unnoticed.
/// </summary>
[Collection("Hybrasyl")]
public class CapabilityGreetingWiring
{
    private static readonly TimeSpan SendWait = TimeSpan.FromSeconds(5);

    public CapabilityGreetingWiring(HybrasylFixture fixture) { }

    /// <summary>The greeting this lobby would have sent before HS-1621, as DALib encodes it.</summary>
    private static byte[] RetailGreetingBody =>
        new AcceptConnectionPacket { Message = "CONNECTED SERVER\n" }.ToBody();

    /// <summary>
    ///     A lobby that accepts without binding a socket. <c>AcceptConnection</c> returns early
    ///     unless the server is running, and <c>Active</c>'s setter is protected.
    /// </summary>
    private sealed class AcceptingLobby : Lobby
    {
        public AcceptingLobby() : base(IPAddress.Any, 1338) => Active = true;
    }

    /// <summary>
    ///     Accepts one connection on a fresh lobby and returns the greeting body it emitted —
    ///     everything after <c>[0xAA][u16 length][opcode]</c>, since 0x7E is unencrypted.
    /// </summary>
    private static byte[] AcceptAndTakeGreetingBody()
    {
        var lobby = new AcceptingLobby();
        var listener = new TestSocket();
        var handler = new TestSocket();

        // AcceptConnection reads the listener out of AsyncState and the accepted socket out of
        // the result itself, which is the shape TaskToAsyncResult produces.
        var accept = TaskToAsyncResult.Begin(Task.FromResult(handler), callback: null, listener);

        lobby.AcceptConnection(accept);

        var client = lobby.Clients.Values.Single();

        try
        {
            client.FlushSendBuffer();

            Assert.True(handler.TryTakeSent(SendWait, out var wire), "the lobby sent no greeting");
            Assert.Equal(0xAA, wire[0]);
            Assert.Equal(0x7E, wire[3]);

            return wire[4..];
        }
        finally
        {
            GlobalConnectionManifest.DeregisterClient(client);
        }
    }

    /// <summary>
    ///     With no certificate the stack cannot honor an upgrade, so the greeting must be exactly
    ///     what it was before this slice — a retail client's first bytes are unchanged.
    /// </summary>
    [Fact]
    public void LobbyGreeting_WithoutTlsCertificate_IsUnchangedFromRetail()
    {
        var previous = Game.TlsCertificate;
        Game.TlsCertificate = null;

        try
        {
            var body = AcceptAndTakeGreetingBody();

            Assert.Equal(RetailGreetingBody, body);
            Assert.False(CapabilityMarker.TryRead(body, out _),
                "an unconfigured stack must not advertise an upgrade it would refuse");
        }
        finally
        {
            Game.TlsCertificate = previous;
        }
    }

    /// <summary>
    ///     With a certificate the greeting carries the marker — read back through the same
    ///     detector a capable client uses, not through the builder that wrote it.
    /// </summary>
    [Fact]
    public void LobbyGreeting_WithTlsCertificate_CarriesTheCapabilityMarker()
    {
        var previous = Game.TlsCertificate;
        Game.TlsCertificate = TestCertificate.Create();

        try
        {
            var body = AcceptAndTakeGreetingBody();
            var retail = RetailGreetingBody;

            Assert.True(CapabilityMarker.TryRead(body, out var marker), "no marker in the greeting");
            Assert.Equal(CapabilityMarker.CurrentVersion, marker.Version);
            Assert.Equal(CapabilityFlags.None, marker.Flags);

            // The marker is a trailer: a retail client parsing the banner sees what it always saw.
            Assert.Equal(retail, body[..retail.Length]);
            Assert.Equal(retail.Length + CapabilityMarker.Length, body.Length);
        }
        finally
        {
            Game.TlsCertificate = previous;
        }
    }
}
