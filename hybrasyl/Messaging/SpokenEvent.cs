using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Objects;

namespace Hybrasyl.Messaging
{
    public record SpokenEvent(VisibleObject Speaker, string Message, string From = null, bool Shout = false)
    {
        public string SanitizedMessage => Message.ToLower().Trim();
    }
}
