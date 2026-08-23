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

using DALib.Networking.Wire;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

/// <summary>
///     An S-&gt;C packet whose body is the caller's bytes, verbatim.
/// </summary>
/// <remarks>
///     Overriding only <c>Opcode</c> and <c>WriteBody</c> leaves <c>EncryptMethod</c>
///     opcode-derived by <see cref="ServerPacket" />, so the frame on the wire is exactly what the
///     real opcode carries while no typed model for that opcode is involved. That is the property
///     the probe commands need: when the layout is the thing under test, an instrument that knows
///     the layout produces bytes that agree with it by construction.
/// </remarks>
internal sealed record RawServerPacket(byte RawOpcode, byte[] Body) : ServerPacket
{
    public override byte Opcode => RawOpcode;

    public override void WriteBody(IPacketWriter writer) => writer.WriteBytes(Body);
}
