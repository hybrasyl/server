using System;
using System.Collections.Generic;

namespace Hybrasyl.Plugins;

class SimpleConfiguration : IHandlerConfiguration
{
    private Dictionary<string, string> Config;
    private object Lock;

    private void Init()
    {
        Lock = new object();
        Config = new Dictionary<string, string>();
    }
    public SimpleConfiguration() => Init();

    public SimpleConfiguration(List<Xml.PluginConfig> configuration)
    {
        Init();
        LoadXmlConfig(configuration);
    }

    public void LoadXmlConfig(List<Xml.PluginConfig> configuration)
    {
        lock (Lock)
        {
            foreach (var kvp in configuration)
            {
                if (!StoreValue(kvp.Key, kvp.Value))
                {
                    GameLog.Error("SimpleConfiguration: XML configuration processing failure, couldn't store key");
                    throw new ArgumentException("Configuration could not be stored");
                }
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
        if (Config.TryGetValue(key, out string v))
        {
            val = v;
            return true;
        }
        return false;
    }
}