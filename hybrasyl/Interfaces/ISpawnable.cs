using Hybrasyl.Subsystems.Scripting;

namespace Hybrasyl.Interfaces;

public interface ISpawnable
{
    public string Name { get; set; }
    public Script Script { get; set; }
    public void OnSpawn();
}