using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl;

// Some simple classes we can use to deserialize migration json.
[JsonObject]
class RedisMigrations
{
    public List<string> Migrations { get; set; }
}

class RedisActiveMigrations
{
    public List<string> ActiveMigrations { get; set; }
}