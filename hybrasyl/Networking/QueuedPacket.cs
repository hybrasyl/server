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
///     The delay is transport pacing, not a property of the packet: the same type takes different
///     delays at different call sites, so it belongs here rather than on the DALib record.
/// </remarks>
public readonly record struct QueuedPacket(IServerPacket Packet, int TransmitDelay = 0)
{
    /// <summary>The opcode, for the send loop's key check and logging.</summary>
    public byte Opcode => Packet.Opcode;
}
