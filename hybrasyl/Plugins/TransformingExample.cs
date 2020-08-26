using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Plugins
{
    /// <summary>
    /// An example of a transforming message handler.
    /// </summary>
    class TransformingExample : MessagePlugin, IProcessingMessageHandler
    {
        public IMessagePluginResponse Process(Message inbound)
        {
            inbound.Text.Replace("lol", "Amusing", StringComparison.InvariantCultureIgnoreCase);
            return new MessagePluginResponse()
            {
                Message = inbound,
                Success = true,
                PluginResponse = string.Empty
            };
        }
    }
}
