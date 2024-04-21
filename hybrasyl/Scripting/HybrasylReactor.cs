using Hybrasyl.Objects;
using MoonSharp.Interpreter;
using System;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
public class HybrasylReactor : HybrasylWorldObject
{
    public HybrasylReactor(Reactor obj) : base(obj) { }
    internal Reactor Reactor => WorldObject as Reactor;
    public HybrasylUser Origin => Reactor.Origin is User u ? new HybrasylUser(u) : null;

    public bool Blocking => Reactor.Blocking;

    public int Uses
    {
        get => Reactor.Uses;
        set => Reactor.Uses = value;
    }

    public bool Expired => Reactor.Expired;
    public long Expiration => ((DateTimeOffset)Reactor.Expiration).ToUnixTimeSeconds();

    /// <summary>
    /// Make a reactor visible to a player if they have a specified cookie.
    /// </summary>
    /// <param name="cookieName">Name of the cookie to add to the list.</param>
    /// <param name="remove">If true, remove the cookie from the list.</param>
    public void VisibleToCookie(string cookieName, bool remove = false)
    {
        if (!remove)
            Reactor.VisibleToCookies.Add(cookieName);
        else
            Reactor.VisibleToCookies.Remove(cookieName);
    }

    /// <summary>
    /// Make a reactor invisible to a player if they have a specified cookie.
    /// </summary>
    /// <param name="cookieName">Name of the cookie to add to the list.</param>
    /// <param name="remove">If true, remove the cookie from the list.</param>
    public void InvisibleToCookie(string cookieName, bool remove = false)
    {
        if (!remove)
            Reactor.InvisibleToCookies.Add(cookieName);
        else
            Reactor.InvisibleToCookies.Remove(cookieName);

    }

}