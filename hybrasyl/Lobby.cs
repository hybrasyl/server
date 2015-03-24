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

using System.Threading;

namespace Hybrasyl
{
    public class Lobby : Server
    {
        public new LobbyPacketHandler[] PacketHandlers { get; private set; }

        public Lobby(int port)
            : base(port)
        {
            Logger.InfoFormat("LobbyConstructor: port is {0}", port);

            PacketHandlers = new LobbyPacketHandler[256];
            for (int i = 0; i < 256; ++i)
                PacketHandlers[i] = (c, p) => Logger.WarnFormat("Lobby: Unhandled opcode 0x{0:X2}", p.Opcode);
            PacketHandlers[0x00] = PacketHandler_0x00_ClientVersion;
            PacketHandlers[0x57] = PacketHandler_0x57_ServerTable;

        }

        public override void AcceptConnection()
        {
            if (TcpListener.Pending())
            {
                var socket = TcpListener.AcceptSocket();
                var client = new Client(socket, this);

                var x7E = new ServerPacket(0x7E);
                x7E.WriteByte(0x1B);
                x7E.WriteString("CONNECTED SERVER\n");
                client.Enqueue(x7E);

                var thread = new Thread(client.ClientLoop);
                thread.Start();
            }
        }

        private void PacketHandler_0x00_ClientVersion(Client client, ClientPacket packet)
        {
            var x00 = new ServerPacket(0x00);
            x00.WriteByte(0x00);
            x00.WriteUInt32(Game.ServerTableCrc);
            x00.WriteByte(client.EncryptionSeed);
            x00.WriteByte((byte)client.EncryptionKey.Length);
            x00.Write(client.EncryptionKey);
            client.Enqueue(x00);
        }
        private void PacketHandler_0x57_ServerTable(Client client, ClientPacket packet)
        {
            var mismatch = packet.ReadByte();

            if (mismatch == 1)
            {
                var x56 = new ServerPacket(0x56);
                x56.WriteUInt16((ushort)Game.ServerTable.Length);
                x56.Write(Game.ServerTable);
                client.Enqueue(x56);
            }
            else
            {
                var server = packet.ReadByte();
                var redirect = new Redirect(client, this, Game.Login, "socket", client.EncryptionSeed, client.EncryptionKey);
                client.Redirect(redirect);
            }
        }
    }
}
