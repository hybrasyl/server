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

using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Servers
{
    private static HybrasylFixture Fixture;

    public Servers(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void PacketHandlerTable_CoversAllOpcodes()
    {
        // Regression: the default-handler loop stopped at 0xFE, leaving 0xFF unhandled.
        for (var i = 0; i < 256; i++)
            Assert.True(Game.World.WorldPacketHandlers.ContainsKey((byte)i),
                $"missing handler for opcode 0x{i:X2}");
    }
}
