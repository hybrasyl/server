using System;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting;

[MoonSharpUserData] 
public class HybrasylReactor
{
    internal Reactor Obj { get; set; }
    public static bool IsPlayer => false;
    public HybrasylUser Origin => Obj.Origin is User u ? new HybrasylUser(u) : null;
    public byte X => Obj.X;
    public byte Y => Obj.Y;
    public bool Blocking => Obj.Blocking;

    public int Uses
    {
        get => Obj.Uses;
        set => Obj.Uses = value;
    }
    public bool Expired => Obj.Expired;
    public long Expiration => ((DateTimeOffset)Obj.Expiration).ToUnixTimeSeconds();

    public HybrasylReactor(Reactor obj)
    {
        Obj = obj;
    }

}