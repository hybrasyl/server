using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Xml;

public partial class CreatureCastingSet
{
    public List<string> CategoryList
    {
        get
        {
            if (string.IsNullOrEmpty(Categories))
                return new List<string>();
            else
                return Categories.Trim().ToLower().Split(" ").ToList();
        }
    }
}