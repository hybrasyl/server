using System;
using System.Drawing;

namespace Hybrasyl.Interfaces;

public interface IWorldObject
{
    public Rectangle Rect { get; }
    public DateTime CreationTime { get; }
    public bool HasMoved { get; set; }
    public string Name { get; set; }
    public Guid ServerGuid { get; set; }
    public Guid Guid { get; set; }
    public World World { get; }
    public uint Id { get; set; }
    public string Type { get; }
}