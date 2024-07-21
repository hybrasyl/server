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

namespace Hybrasyl.Internals;

public struct RedisConnection
{
    public string Host { get; set; }
    public int Port { get; set; }
    public int Database { get; set; }
    public string Password { get; set; }

    public RedisConnection(string host, int port = -1, int database = -1, string password = null)
    {
        Host = host ?? "127.0.0.1";
        Port = port == -1 ? 6379 : port;
        Database = database == -1 ? 0 : database;
        Password = password;
    }
}