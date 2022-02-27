using Hybrasyl.Xml;

namespace Hybrasyl.Objects;

public class NextCastingAction
{
    public MonsterBookSlot Slot { get; set; }
    public CreatureTargetPriority TargetPriority { get; set; } = CreatureTargetPriority.None;
    public static NextCastingAction DoNothing => new() { Slot = null };
    public bool DoNotCast => Slot == null;    
}