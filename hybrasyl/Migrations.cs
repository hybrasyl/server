using Newtonsoft.Json;
using System.Collections.Generic;

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