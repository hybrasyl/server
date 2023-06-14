using Hybrasyl.Objects;
using Hybrasyl.Scripting;
using MoonSharp.Interpreter;

namespace Hybrasyl.Messaging;


[MoonSharpUserData]
public record SpokenEvent(VisibleObject Speaker, string Message, string From = null, bool Shout = false)
{
    public string SanitizedMessage => Message.ToLower().Trim();
    public HybrasylWorldObject Source => Speaker is User user ? new HybrasylUser(user) : new HybrasylWorldObject(Speaker);
}