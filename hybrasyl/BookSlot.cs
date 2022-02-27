using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Xml;

namespace Hybrasyl
{
    public interface IBookSlot
    {
        public Xml.Castable Castable { get; set; }
        public uint UseCount { get; set; }
        public uint MasteryLevel { get; set; }
        public DateTime LastCast { get; set; }
        public bool OnCooldown { get; }
        public bool HasBeenUsed { get; }
        public double SecondsSinceLastUse { get; }
    }

    public class BookSlot : IBookSlot
    {
        public Xml.Castable Castable { get; set; }
        public uint UseCount { get; set; }
        public uint MasteryLevel { get; set; }
        public DateTime LastCast { get; set; }
        public bool OnCooldown => (Castable.Cooldown > 0) && ((DateTime.Now - LastCast).TotalSeconds < Castable.Cooldown);
        public bool HasBeenUsed => LastCast != default;
        public double SecondsSinceLastUse => (DateTime.Now - LastCast).TotalSeconds;

    }

    public class MonsterBookSlot : BookSlot
    {
        public MonsterBookSlot(Castable castable)
        {
            Castable = castable;
        }

        public Xml.RotationType Type { get; set; }
        public CreatureCastable Directive { get; set; }
       
    }

}
