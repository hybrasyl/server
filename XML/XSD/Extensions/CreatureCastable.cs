using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Xml;

public partial class CreatureCastable
{

    public CreatureCastable(int interval, CreatureAttackPriority priority, string value) : this()
    {
        Interval = interval;
        Priority = priority;
        Value = value;
    }

    public bool ThresholdTriggered { get; set; } = false;
}
