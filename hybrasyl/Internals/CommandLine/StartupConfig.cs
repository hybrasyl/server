using System;
using System.CommandLine;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Hybrasyl.Internals.CommandLine;

public record StartupConfig
{
    public string DataDir { get; set; } 
    public string WorldDataDir { get; set; }
    public string LogDir { get; set; } = null;
    public string ConfigName { get; set; }
    public string ConfigFile { get; set; }
    public string RedisHost { get; set; }
    public int RedisPort { get; set; }
    public int RedisDb { get; set; }
    public string RedisPassword { get; set; }

    public static StartupConfig FromParseResult(ParseResult result)
    {
        var config = new StartupConfig();
        var rcr = result.RootCommandResult;
        config.DataDir = rcr.GetValue<string>("--dataDir");
        config.WorldDataDir = rcr.GetValue<string>("--worldDataDir");
        config.LogDir = rcr.GetValue<string>("--logDir");
        config.ConfigName = rcr.GetValue<string>("--config");
        config.ConfigFile = rcr.GetValue<string>("--configFile");
        config.RedisHost = rcr.GetValue<string>("--redisHost");
        config.RedisPort = rcr.GetValue<int>("--redisPort");
        config.RedisDb = rcr.GetValue<int>("--redisDb");
        config.RedisPassword = rcr.GetValue<string>("--redisPassword");
        return config;
    }
}
