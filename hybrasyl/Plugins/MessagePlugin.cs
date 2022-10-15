using System.Collections.Generic;

namespace Hybrasyl.Plugins;

public abstract class MessagePlugin : IMessageHandler
{
    private readonly HashSet<string> Targets = new();
    public bool Disabled { get; set; }
    public bool Passthrough { get; set; }

    public virtual bool Initialize(IHandlerConfiguration config) => true;

    public void SetTargets(List<string> targets)
    {
        targets.ForEach(action: x => Targets.Add(x.ToLower()));
    }

    public bool WillHandle(string target) => Targets.Contains(target.ToLower());
}