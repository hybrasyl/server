// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

namespace Hybrasyl.Objects;

// Simple container class for A* structure
public class Tile
{
    public int X { get; set; }
    public int Y { get; set; }
    public int F { get; set; }
    public int G { get; set; }
    public int H { get; set; }
    public Tile Parent { get; set; }

    public (int X, int Y) Target { get; set; }

    public bool IsAdjacent(int x1, int y1)
    {
        if (X == x1)
            return y1 + 1 == Y || y1 - 1 == Y;
        if (Y == y1)
            return x1 + 1 == X || x1 - 1 == X;
        return false;
    }

    public override string ToString()
    {
        var ret = string.Empty;
        var start = this;
        while (start.Parent != null)
        {
            ret += $"{start.X}, {start.Y} -> {start.Parent.X}, {start.Parent.Y}  ";
            start = start.Parent;
        }

        return ret;
    }
}