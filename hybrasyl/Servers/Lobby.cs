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

using DALib.Networking.Packets.Client;
using DALib.Networking.Packets.Server;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Networking;
using System.Linq;
using System.Net;

namespace Hybrasyl.Servers;

public class Lobby : Server
{
    public Lobby(IPAddress bindAddress, int port, bool isDefault = false)
        : base(bindAddress, port, isDefault)
    {
        GameLog.InfoFormat("LobbyConstructor: port is {0}", port);

        PacketHandlers = new LobbyPacketHandler[256];
        for (var i = 0; i < 256; ++i)
            PacketHandlers[i] = (c, p) => GameLog.WarningFormat("Lobby: Unhandled opcode 0x{0:X2}", p.Opcode);
        PacketHandlers[0x00] = PacketHandler_0x00_ClientVersion;
        PacketHandlers[0x57] = PacketHandler_0x57_ServerTable;
    }

    public LobbyPacketHandler[] PacketHandlers { get; }

    private void PacketHandler_0x00_ClientVersion(IClient client, ClientPacket packet)
    {
        // Lobby clients get their key in the Client constructor; a null here is a server bug.
        if (client.EncryptionKey is not { } key)
        {
            GameLog.Error("Lobby: cid {ConnectionId} has no encryption key, disconnecting", client.ConnectionId);
            client.Disconnect();
            return;
        }

        // Throws on a missing 'LK' signature; the receive loop drops the packet.
        var version = VersionPacket.Parse(packet.PayloadData);
        GameLog.DebugFormat("Lobby: cid {0} client version {1}", client.ConnectionId, version.Version);

        client.Enqueue(new CryptoKeyPacket
        {
            ServerTableCrc = Game.ServerTableCrc,
            Seed = client.EncryptionSeed,
            Key = key
        });
    }

    private void PacketHandler_0x57_ServerTable(IClient client, ClientPacket packet)
    {
        switch (ServerTablePacket.Parse(packet.PayloadData))
        {
            case ServerTableRequestPacket:
                GameLog.InfoFormat("ServerTable: sent {0} entries", Game.ServerTableEntries.Count);
                client.Enqueue(new ServerTableDataPacket { Servers = Game.ServerTableEntries.ToList() });
                break;

            case ServerTableSelectPacket:
                // Lobby clients get their key in the Client constructor; a null here is a server bug.
                if (client.EncryptionKey is not { } key)
                {
                    GameLog.Error("Lobby: cid {ConnectionId} has no encryption key, disconnecting",
                        client.ConnectionId);
                    client.Disconnect();
                    return;
                }

                // Single-server deployment: the selected ServerId is irrelevant, all roads lead to Login.
                var redirect = new Redirect(client, this, Game.Login, "socket", client.EncryptionSeed, key);
                client.Redirect(redirect);
                break;
        }
    }
}
