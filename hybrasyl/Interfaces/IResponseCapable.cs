using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Interfaces;

public interface IResponseCapable : IWorldObject
{
    public Dictionary<string, string> Strings { get; set; }
    public Dictionary<string, string> Responses { get; set; }

    public string DefaultGetLocalString(string key) => Strings.ContainsKey(key) ? Strings[key] : World.GetLocalString(key);

    public string DefaultGetLocalString(string key, params (string Token, string Value)[] replacements)
    {
        var str = DefaultGetLocalString(key);

        return replacements.Aggregate(str, (current, repl) => current.Replace(repl.Token, repl.Value));
    }
}
