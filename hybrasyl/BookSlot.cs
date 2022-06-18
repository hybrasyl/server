using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Objects;
using Hybrasyl.Xml;

namespace Hybrasyl
{
    public class BookSlot 
    {
        public Xml.Castable Castable { get; set; }
        public uint UseCount { get; set; }
        public uint MasteryLevel { get; set; }
        public DateTime LastCast { get; set; }
        public bool OnCooldown => (Castable.Cooldown > 0) && ((DateTime.Now - LastCast).TotalSeconds < Castable.Cooldown);
        public bool HasBeenUsed => LastCast != default;
        public double SecondsSinceLastUse => (DateTime.Now - LastCast).TotalSeconds;

    }


}
