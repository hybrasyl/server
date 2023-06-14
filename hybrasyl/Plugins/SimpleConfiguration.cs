using System;
using System.Collections.Generic;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Plugins;

internal class SimpleConfiguration : IHandlerConfiguration
{
    private Dictionary<string, string> Config;
    private object Lock;

    public SimpleConfiguration()
    {
        Init();
    }

    public SimpleConfiguration(List<PluginConfig> configuration)
    {
        Init();
        LoadXmlConfig(configuration);
    }

    public void LoadXmlConfig(List<PluginConfig> configuration)
    {
        lock (Lock)
        {
            foreach (var kvp in configuration)
                if (!StoreValue(kvp.Key, kvp.Value))
                {
                    GameLog.Error("SimpleConfiguration: XML configuration processing failure, couldn't store key");
                    throw new ArgumentException("Configuration could not be stored");
                }
        }
    }

    public bool StoreValue(string key, string value)
    {
        try
        {
            lock (Lock)
            {
                Config.Add(key, value);
                return true;
            }
        }
        catch (Exception e)
        {
            GameLog.Error("SimpleConfiguration: store value failed: {e}", e);
        }

        return false;
    }

    public bool TryGetValue(string key, out string val)
    {
        val = null;
        if (Config.TryGetValue(key, out var v))
        {
            val = v;
            return true;
        }

        return false;
    }

    private void Init()
    {
        Lock = new object();
        Config = new Dictionary<string, string>();
    }
}