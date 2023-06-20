using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

