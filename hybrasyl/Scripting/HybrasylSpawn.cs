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

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]
    public class HybrasylSpawn
    {
        internal Xml.Spawn Spawn;

        // Expose fields that can be used by scripting

        public byte Str
        {
            get => Spawn.Stats.Str;
            set => Spawn.Stats.Str = value;
        }
        public byte Int
        {
            get => Spawn.Stats.Int;
            set => Spawn.Stats.Int = value;
        }
        public byte Wis
        {
            get => Spawn.Stats.Wis;
            set => Spawn.Stats.Wis = value;
        }
        public byte Con
        {
            get => Spawn.Stats.Con;
            set => Spawn.Stats.Con = value;
        }
        public byte Dex
        {
            get => Spawn.Stats.Dex;
            set => Spawn.Stats.Dex = value;
        }
        public uint Hp
        {
            get => Spawn.Stats.Hp;
            set => Spawn.Stats.Hp = value;
        }
        public uint Mp
        {
            get => Spawn.Stats.Mp;
            set => Spawn.Stats.Mp = value;
        }
        public byte Level
        {
            get => Spawn.Stats.Level;
            set => Spawn.Stats.Level = value;
        }
        public byte Dmg
        {
            get => Spawn.Damage.Dmg;
            set => Spawn.Damage.Dmg = value;
        }
        public byte Hit
        {
            get => Spawn.Damage.Hit;
            set => Spawn.Damage.Hit = value;
        }
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

        public void AddCastable(string item, double chance = 0.50, int cooldown = 1, bool always = false)
        {
            if (Game.World.WorldData.TryGetValue(item, out Xml.Castable theCastable))
            {
                // Add a castable to our casting list
                var castInstruction = new Xml.SpawnCastable();
                castInstruction.Chance = (float)chance;
                castInstruction.Cooldown = cooldown;
                castInstruction.Value = item;
                castInstruction.Always = always;
                Spawn.Castables.Add(castInstruction);
            }
        }

        public HybrasylSpawn(string creature, string spawnName, byte level = 3, byte str = 3,
            byte intel = 3, byte wis = 3, byte con = 3, byte dex = 3)
        {
            Spawn = new Xml.Spawn();
            Level = level;
            Str = str;
            Int = intel;
            Wis = wis;
            Con = con;
            Dex = dex;
            // Populate a default, empty loot table, with default xp/gold settings
            Spawn.Loot.Table = new System.Collections.Generic.List<Xml.LootTable>();
            Spawn.Castables = new System.Collections.Generic.List<Xml.SpawnCastable>();
            Spawn.Loot.Table.Add(new Xml.LootTable());
            Spawn.Loot = new Xml.LootList();
            Spawn.Loot.Gold = new Xml.LootGold();
        }
    }
}

