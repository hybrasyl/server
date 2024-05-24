// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Loot;

public class Loot
{
    public uint Gold;
    public List<string> Items;
    public uint Xp;

    public Loot(uint xp, uint gold, List<string> items = null)
    {
        Xp = xp;
        Gold = gold;
        Items = items ?? new List<string>();
    }

    public static Loot operator +(Loot a) => a;

    public static Loot operator +(Loot a, Item b)
    {
        var ret = new Loot(a.Xp, a.Gold);
        ret.Items.AddRange(a.Items);
        ret.Items.Add(b.Name);
        return ret;
    }

    public static Loot operator +(Loot a, Loot b) =>
        new(a.Xp + b.Xp, a.Gold + b.Gold, a.Items.Concat(b.Items).ToList());

    public override string ToString() => $"XP: {Xp}\nGold: {Gold}\nItems: " + string.Join(",", Items);
}