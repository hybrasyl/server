using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Xml;

public partial class LootList
{
    public static LootList operator +(LootList l1, LootList l2)
    {
        var ret = new LootList();
        ret.Gold = new LootGold()
        {
            Min = l1?.Gold?.Min ?? 0 + l2?.Gold?.Min ?? 0,
            Max = l1?.Gold?.Max ?? 1 + l2?.Gold?.Max ?? 0
        };
        ret.Set = new List<LootImport>();
        if (l1?.Set is not null)
            ret.Set.AddRange(l1?.Set);
        if (l2?.Set is not null)
            ret.Set.AddRange(l2.Set);
        ret.Table = new List<LootTable>();
        if (l1?.Table is not null)  
            ret.Table.AddRange(l1.Table);
        if (l2?.Table is not null)
            ret.Table.AddRange(l2.Table);
        ret.Xp = l1?.Xp ?? 0 + l2?.Xp ?? 0;
        return ret;
    }
}