using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Xml
{
    public partial class LocalizedStringGroup
    {
        private Dictionary<string, string> Index = new();

        public void Reindex()
        {
            Index.Clear();
            // TODO: clean up this xml structure

            foreach (var str in Common)
            {
                Index.Add(str.Key, str.Value);
            }
            foreach (var str in Merchant)
            {
                Index.Add(str.Key, str.Value);
            }
            foreach (var str in MonsterSpeak)
            {
                Index.Add(str.Key, str.Value);
            }
            foreach (var str in NpcSpeak)
            {
                Index.Add(str.Key, str.Value);
            }

        }

        /// <summary>
        /// Fetch a localized string from a given key
        /// </summary>
        /// <param name="key">The key for the string (eg item_equip_too_heavy)</param>
        /// <returns>The localized string, or the key itself, if it is missing</returns>
        public string GetString(string key) => Index.TryGetValue(key, out var str) ? str : key;
    }
}
