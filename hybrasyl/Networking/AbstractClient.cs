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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Servers;
using System.Text;

namespace Hybrasyl.Networking;

public abstract class AbstractClient
{
    protected byte[] EncryptionKeyTable { get; set; } = new byte[1024];
    protected Server Server { get; init; }
    public string LastMessage { get; set; } = string.Empty;

    /// <summary>
    ///     Return the ServerType of a connection, corresponding with Hybrasyl.Utility.ServerTypes
    /// </summary>
    public int ServerType
    {
        get
        {
            if (Server is Lobby) return ServerTypes.Lobby;
            if (Server is Login) return ServerTypes.Login;
            return ServerTypes.World;
        }
    }

    public byte[] GenerateKey(ushort bRand, byte sRand)
    {
        var key = new byte[9];

        for (var i = 0; i < 9; ++i) key[i] = EncryptionKeyTable[(i * (9 * i + sRand * sRand) + bRand) % 1024];

        return key;
    }

    public void GenerateKeyTable(string seed)
    {
        var table = Crypto.Md5HashString(seed);
        table = Crypto.Md5HashString(table);
        for (var i = 0; i < 31; i++) table += Crypto.Md5HashString(table);
        EncryptionKeyTable = Encoding.ASCII.GetBytes(table);
    }
}