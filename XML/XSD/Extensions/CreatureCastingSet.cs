using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Xml;

public partial class CreatureCastingSet
{
    public bool Active { get; set; } = false;
    public List<string> CategoryList
    {
        get
        {
            if (string.IsNullOrEmpty(Categories))
                return new List<string>();
            else
                return Categories.Trim().Split(" ").ToList();
        }
    }
}