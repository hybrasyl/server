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

using IServerPacket = DALib.Networking.Wire.IServerPacket;

namespace Hybrasyl.Networking;

/// <summary>
///     One queued outbound packet: the typed DALib record plus the transmit delay the send loop
///     uses to decide whether to batch it with its neighbours.
/// </summary>
/// <remarks>
///     Replaces the legacy <c>ServerPacket</c> as the send-queue element. That type carried a
///     hand-written body, an opcode, framing and a full positional write API; with every emit site
///     converted, all it still carried was a DALib record and a delay, so this is the shape it had
///     been reduced to. The <c>RawBodyServerPacket</c> bridge that let unconverted sites through
///     goes with it — there are no unconverted sites left.
/// </remarks>
public readonly record struct OutboundPacket(IServerPacket Packet, int TransmitDelay = 0)
{
    /// <summary>The opcode, for the send loop's key check and logging.</summary>
    public byte Opcode => Packet.Opcode;
}
