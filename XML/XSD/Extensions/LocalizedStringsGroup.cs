using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Xml;

public partial class LocalizedStringGroup
{
    private Dictionary<string, string> Index = new();
    private Dictionary<string, string> ResponsesIndex = new();

    public void Reindex()
    {
        Index.Clear();
        // TODO: clean up this xml structure

        foreach (var str in Common.Where(predicate: str =>
                     !string.IsNullOrEmpty(str.Key) && !string.IsNullOrEmpty(str.Value))) Index.Add(str.Key, str.Value);
        foreach (var str in Merchant.Where(predicate: str =>
                     !string.IsNullOrEmpty(str.Key) && !string.IsNullOrEmpty(str.Value))) Index.Add(str.Key, str.Value);
        foreach (var str in MonsterSpeak.Where(predicate: str =>
                     !string.IsNullOrEmpty(str.Key) && !string.IsNullOrEmpty(str.Value))) Index.Add(str.Key, str.Value);
        foreach (var str in NpcSpeak.Where(predicate: str =>
                     !string.IsNullOrEmpty(str.Key) && !string.IsNullOrEmpty(str.Value))) Index.Add(str.Key, str.Value);

        foreach (var resp in NpcResponses)
        {
            var key = resp.Call.ToLower().Trim();
            ResponsesIndex.Add(key, resp.Value);
        }
    }


    /// <summary>
    ///     Fetch a localized string from a given key
    /// </summary>
    /// <param name="key">The key for the string (eg item_equip_too_heavy)</param>
    /// <returns>The localized string, or the key itself, if it is missing</returns>
    public string GetString(string key) => Index.TryGetValue(key, out var str) ? str : key;

    public string GetResponse(string key) => ResponsesIndex.TryGetValue(key, out var str) ? str : null;
}