using System;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
public class HybrasylReactor : HybrasylWorldObject
{
    public HybrasylReactor(Reactor obj) : base(obj) { }
    internal Reactor Reactor => WorldObject as Reactor;
    public static bool IsPlayer => false;
    public HybrasylUser Origin => Reactor.Origin is User u ? new HybrasylUser(u) : null;

    public bool Blocking => Reactor.Blocking;

    public int Uses
    {
        get => Reactor.Uses;
        set => Reactor.Uses = value;
    }

    public bool Expired => Reactor.Expired;
    public long Expiration => ((DateTimeOffset) Reactor.Expiration).ToUnixTimeSeconds();
}