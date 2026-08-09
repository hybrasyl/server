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

namespace Hybrasyl.Networking.ClientPackets;

public class Login : PacketBase
{
    // Body built by the DALib record so the XOR'd integrity trailer is present and
    // CRC-valid — the typed handler parse rejects a bare name/password body.
    public Login(string name, string password)
    {
        var writer = new DALib.Networking.Wire.PacketWriter();
        new DALib.Networking.Packets.Client.LoginPacket { Name = name, Password = password }.WriteBody(writer);
        Data.Write(writer.WrittenSpan);
    }

    public override byte Opcode => 0x03;
}