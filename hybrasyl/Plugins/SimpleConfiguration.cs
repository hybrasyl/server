using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Plugins
{
    class SimpleConfiguration : IHandlerConfiguration
    {
        private Dictionary<string, string> Config;
        private object Lock;

        public SimpleConfiguration(Dictionary<string, string> config)
        {
            Config = config;
            Lock = new object();
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
}
