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