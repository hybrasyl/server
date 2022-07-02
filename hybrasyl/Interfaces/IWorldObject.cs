using System;
using System.Drawing;
using Hybrasyl;

namespace Hybrasyl.Interfaces;

public interface IWorldObject
{
    public Rectangle Rect { get; }
    public DateTime CreationTime { get; }
    public bool HasMoved { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
    public uint Id { get; set; }
    public string Name { get; set; }
    public Guid ServerGuid { get; set; }
    public World World { get; }
    public string Type { get; }
}