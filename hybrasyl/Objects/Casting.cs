using Hybrasyl.Xml;

namespace Hybrasyl.Objects;

public class NextCastingAction
{
    public BookSlot Slot { get; set; }
    public CreatureAttackPriority Target { get; set; } = CreatureAttackPriority.HighThreat;
    public static NextCastingAction DoNothing => new NextCastingAction() { Slot = null };
    public bool DoNotCast => Slot == null;    
}