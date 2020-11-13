using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Xml
{
    public partial class LootList
    {
        public static LootList operator +(LootList l1, LootList l2)
        {
            var ret = new LootList();
            ret.Gold = new LootGold()
            {
                Min = l1.Gold?.Min ?? 0 + l2.Gold?.Min ?? 0,
                Max = l1.Gold?.Max ?? 1 + l2.Gold?.Max ?? 0
            };
            ret.Set = new List<LootImport>();
            ret.Set.AddRange(l1.Set);
            ret.Set.AddRange(l2.Set);
            ret.Table = new List<LootTable>();
            ret.Table.AddRange(l1.Table);
            ret.Table.AddRange(l2.Table);
            ret.Xp = l1.Xp + l2.Xp;
            return ret;
        }
    }
}
