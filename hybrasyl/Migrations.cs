using System.Collections.Generic;
using Newtonsoft.Json;

namespace Hybrasyl;

// Some simple classes we can use to deserialize migration json.
[JsonObject]
internal class RedisMigrations
{
    public List<string> Migrations { get; set; }
}

internal class RedisActiveMigrations
{
    public List<string> ActiveMigrations { get; set; }
}