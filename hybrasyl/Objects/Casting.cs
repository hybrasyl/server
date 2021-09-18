using Hybrasyl.Xml;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Objects
{
    public class NextCastingAction
    {
        public BookSlot Slot { get; set; } = null;
        public CreatureAttackPriority Target { get; set; } = CreatureAttackPriority.HighThreat;
        public static NextCastingAction DoNothing => new NextCastingAction() { Slot = null };
        public bool DoNotCast => Slot == null;    
    }
}
