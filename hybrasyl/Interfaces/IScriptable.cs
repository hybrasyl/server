namespace Hybrasyl.Interfaces;

public interface IScriptable
{
    public IWorldObject WorldObject { get; set; }
    public string Name { get; }
    public string Type { get; }
    public byte X { get; }
    public byte Y { get; }
}