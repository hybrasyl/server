using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Objects
{
    
    class CreatureTemplate
    {
        Xml.CreatureBehaviorSet BehaviorSet { get; set; }
        Xml.LootTable Loot { get; set; }
        byte Level { get; set; }
        string Name { get; set; }

    }
}
