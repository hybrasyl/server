using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]
    public class Coordinate
    {
        public byte X;
        public byte Y;

        public Coordinate(byte x, byte y)
        {
            X = x;
            Y = y;
        }

        public static Coordinate FromInt(int x, int y) => new Coordinate((byte) Math.Clamp(x, byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp(y, byte.MinValue, byte.MaxValue));

    }
}
