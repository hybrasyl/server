using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Xml;

public partial class CreatureCastable
{

    public CreatureCastable(int interval, CreatureTargetPriority priority, string value) : this()
    {
        Interval = interval;
        TargetPriority = priority;
        Value = value;
    }

}
