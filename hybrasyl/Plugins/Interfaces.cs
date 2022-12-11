using System.Collections.Generic;
using Hybrasyl.Xml;

namespace Hybrasyl.Plugins;

public interface IPluginResponse
{
    public bool Success { get; set; }
    public string PluginResponse { get; set; }
}

public interface IMessagePluginResponse : IPluginResponse
{
    public Message Message { get; set; }
    public bool Transformed { get; }
}

public interface IHandlerConfiguration
{
    public void LoadXmlConfig(List<PluginConfig> config);
    public bool StoreValue(string key, string value);
    public bool TryGetValue(string key, out string value);
}

/// <summary>
///     A base interface for message handlers.
/// </summary>
public interface IMessageHandler
{
    public bool Disabled { get; set; }
    public bool Passthrough { get; set; }
    public bool Initialize(IHandlerConfiguration config);
    public void SetTargets(List<string> targets);
    public bool WillHandle(string target);
}

public interface IProcessingMessageHandler : IMessageHandler
{
    public IMessagePluginResponse Process(Message inbound);
}