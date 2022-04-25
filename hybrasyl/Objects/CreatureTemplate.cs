namespace Hybrasyl.Objects;

public class CreatureTemplate
{
    public Xml.CreatureBehaviorSet BehaviorSet { get; set; }
    public Xml.LootTable Loot { get; set; }
    public byte Level { get; set; } = 1;
    public string Name { get; set; }
}