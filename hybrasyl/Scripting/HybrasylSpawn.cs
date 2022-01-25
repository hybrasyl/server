/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
public class HybrasylSpawn
{
    internal Xml.Spawn Spawn;

    // Expose fields that can be used by scripting

    public uint Exp
    {
        get => Spawn.Loot.Xp;
        set
        {
            Spawn.Loot.Xp = value;
        }
    }
    public uint Gold
    {
        get => Spawn.Loot.Gold.Max;
        set
        {
            Spawn.Loot.Gold.Min = value;
            Spawn.Loot.Gold.Max = value;
        }
    }
    // Loot tweaking. 
    public int LootTableRolls
    {
        get => Spawn.Loot.Table[0].Rolls;
        set => Spawn.Loot.Table[0].Rolls = value;
    }
    public double LootTableChance
    {
        get => Spawn.Loot.Table[0].Chance;
        set => Spawn.Loot.Table[0].Chance = value;
    }

    public void AddLootItem(string item, int max = 0)
    {
        if (Game.World.WorldData.TryGetValue<ItemObject>(item, out _))
        {
            if (Spawn.Loot.Table.Count >= 1)
            {
                // We only support editing the first loot table via scripting. If you don't like that,
                // please feel free to implement the functionality on your own and make a PR.
                var lootItem = new Xml.LootItem();
                lootItem.Value = item;
                lootItem.Max = max;
                var itemList = new Xml.LootTableItemList();
                itemList.Item.Add(lootItem);
                Spawn.Loot.Table[0].Items.Add(itemList);
            }
        }
    }
}