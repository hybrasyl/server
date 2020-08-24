using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Plugins
{
    // TODO: interface
    public class Message
    {
        public string Sender = string.Empty;
        public string Recipient = string.Empty;
        public Xml.MessageType Type { get; set; }
        public string Text = string.Empty;
        public string Subject = string.Empty;
    }
}
