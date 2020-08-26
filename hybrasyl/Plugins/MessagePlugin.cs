using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Plugins
{
    public abstract class MessagePlugin : IMessageHandler
    {
        private HashSet<string> Targets = new HashSet<string>();
        public bool Disabled { get; set; }
        public bool Passthrough { get; set; }

        public virtual bool Initialize(IHandlerConfiguration config) { return true; }
        public void SetTargets(List<string> targets) => targets.ForEach(x => Targets.Add(x.ToLower()));
        public bool WillHandle(string target) => Targets.Contains(target.ToLower());

    }
}
