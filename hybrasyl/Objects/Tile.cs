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
            return (y1 + 1 == Y || y1 - 1 == Y);
        if (Y == y1)
            return (x1 + 1 == X || x1 - 1 == X);
        return false;
    }

    public override string ToString()
    {
        string ret = string.Empty;
        Tile start = this;
        while (start.Parent != null)
        {
            ret += $"{start.X}, {start.Y} -> {start.Parent.X}, {start.Parent.Y}  ";
            start = start.Parent;
        }
        return ret;
    }
}