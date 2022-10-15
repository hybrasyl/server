using Hybrasyl.Xml;

namespace Hybrasyl.Objects;

public class CreatureTemplate
{
    public CreatureBehaviorSet BehaviorSet { get; set; }
    public LootTable Loot { get; set; }
    public byte Level { get; set; } = 1;
    public string Name { get; set; }
}