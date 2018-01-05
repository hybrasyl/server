/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Kyle Speck    <kojasou@hybrasyl.com>
 *
 */

using System;

namespace Hybrasyl
{
    public partial class Lobby : Server
    {
        public new LobbyPacketHandler[] PacketHandlers { get; private set; }

        public Lobby(int port)
            : base(port)
        {
            Logger.InfoFormat("LobbyConstructor: port is {0}", port);

            PacketHandlers = new LobbyPacketHandler[256];
            for (int i = 0; i < 256; ++i)
                PacketHandlers[i] = (c, p) => Logger.WarnFormat("Lobby: Unhandled opcode 0x{0:X2}", p.Opcode);

            SetPacketHandlers();

        }
    }
}
