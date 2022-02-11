using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Plugins;

public class MessagePluginResponse : IMessagePluginResponse
{
    public Message Message { get; set; }
    public bool Success { get; set; }
    public string PluginResponse { get; set; }
    public bool Transformed => Message != null;
}