using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using System;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
/* Workaround to make Elements easily accessible in Lua via Moonsharp,
   for unknown reasons it seemingly refuses to register an enum from an 
   outside assembly */

public static class Element
{
    public static ElementType None => ElementType.None;
    public static ElementType Fire => ElementType.Fire;
    public static ElementType Water => ElementType.Water;
    public static ElementType Wind => ElementType.Wind;
    public static ElementType Earth => ElementType.Earth;
    public static ElementType Light => ElementType.Light;    
    public static ElementType Dark => ElementType.Dark;     
    public static ElementType Wood => ElementType.Wood;
    public static ElementType Metal => ElementType.Metal;    
    public static ElementType Undead => ElementType.Undead;

    public static string ToString(int e) => Enum.GetName(typeof(ElementType), e);
}