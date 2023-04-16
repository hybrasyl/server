using System;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
/* Workaround to make Elements easily accessible in Lua via Moonsharp */
public class Element
{
    public static int None = 0;
    public static int Fire = 1;
    public static int Water = 2;
    public static int Wind = 3;
    public static int Earth = 4;
    public static int Light = 5;
    public static int Dark = 6;
    public static int Wood = 7;
    public static int Metal = 8;
    public static int Undead = 9;

    public static string ToString(int e) => Enum.GetName(typeof(ElementType), e);
}