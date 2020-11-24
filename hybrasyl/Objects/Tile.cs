using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Objects
{
    // Simple container class for A* structure
    public class Tile
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int F { get; set; }
        public int G { get; set; }
        public int H { get; set; }
        public Tile Parent { get; set; }
    }
}
