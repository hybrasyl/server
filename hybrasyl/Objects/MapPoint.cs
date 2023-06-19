using Hybrasyl.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Objects;

public class MapPoint : IStateStorable
{
    public MapPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    public long Id
    {
        get
        {
            unchecked
            {
                return Name.GetHashCode() + X + Y + Parent.GetHashCode();
            }
        }
    }

    public string Parent { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Name { get; set; }
    public string DestinationMap { get; set; }
    public byte DestinationX { get; set; }
    public byte DestinationY { get; set; }

    public int XOffset => X % 255;
    public int YOffset => Y % 255;
    public int XQuadrant => (X - XOffset) / 255;
    public int YQuadrant => (Y - YOffset) / 255;

    public byte[] GetBytes()
    {
        var buffer = Encoding.ASCII.GetBytes(Name);
        GameLog.DebugFormat("buffer is {0} and Name is {1}", BitConverter.ToString(buffer), Name);

        // X quadrant, offset, Y quadrant, offset, length of the name, the name, plus a 64-bit(?!) ID
        var bytes = new List<byte>();

        GameLog.DebugFormat("{0}, {1}, {2}, {3}, {4}, mappoint ID is {5}", XQuadrant, XOffset, YQuadrant,
            YOffset, Name.Length, Id);

        bytes.Add((byte)XQuadrant);
        bytes.Add((byte)XOffset);
        bytes.Add((byte)YQuadrant);
        bytes.Add((byte)YOffset);
        bytes.Add((byte)Name.Length);
        bytes.AddRange(buffer);
        bytes.AddRange(BitConverter.GetBytes(Id));

        return bytes.ToArray();
    }
}
