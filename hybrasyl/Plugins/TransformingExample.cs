using System;

namespace Hybrasyl.Plugins;

/// <summary>
///     An example of a transforming message handler.
/// </summary>
internal class TransformingExample : MessagePlugin, IProcessingMessageHandler
{
    public IMessagePluginResponse Process(Message inbound)
    {
        inbound.Text.Replace("lol", "Amusing", StringComparison.InvariantCultureIgnoreCase);
        return new MessagePluginResponse
        {
            Message = inbound,
            Success = true,
            PluginResponse = string.Empty
        };
    }
}