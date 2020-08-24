using Hybrasyl.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Plugins
{

    public interface IHandlerConfiguration
    {
        public bool StoreValue(string key, string value);
        public bool TryGetValue(string key, out string value);
    }

    /// <summary>
    /// An interface for a message handler plugin that can process messages without transforming the message.
    /// </summary>
    public interface IMessageHandler
    {
        public bool Disabled { get; set; }
        public bool Initialize(IHandlerConfiguration config);
        public void Process(Message message);
    }

    /// <summary>
    /// An interface for a handler that operates as a passthrough, potentially transforming a message.
    /// </summary>
    public interface ITransformingMessageHandler
    { 
        public bool Disabled { get; set; }
        public bool Initialize(IHandlerConfiguration config);
        public Message Process(Message inbound);
    }

}
