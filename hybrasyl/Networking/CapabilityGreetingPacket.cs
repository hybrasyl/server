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
using DALib.Networking.Wire;
using Hybrasyl.Protocol.Negotiation;

namespace Hybrasyl.Networking;

/// <summary>
///     The lobby's 0x7E greeting with the extended-protocol capability marker appended. Retail
///     registers no 0x7E handler and never reads the body, so the marker is invisible to it; a
///     capable client detects the marker and opens login and world with a TLS ClientHello.
/// </summary>
/// <remarks>
///     Sent only when the stack has a TLS certificate - the same condition that lets the receive
///     seam act on a ClientHello. Advertising without it would invite an upgrade the server
///     would then refuse.
/// </remarks>
/// <param name="Greeting">The retail greeting the marker is appended to.</param>
public sealed record CapabilityGreetingPacket(AcceptConnectionPacket Greeting) : ServerPacket
{
    /// <inheritdoc />
    public override byte Opcode => Greeting.Opcode;

    /// <inheritdoc />
    public override void WriteBody(IPacketWriter writer) =>
        writer.WriteBytes(CapabilityMarker.Current.BuildGreetingBody(Greeting));
}
