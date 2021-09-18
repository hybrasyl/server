using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Xml
{
    public partial class VariantGroup
    {
        public Variant RandomVariant() => Variant.PickRandom();
    }
}
