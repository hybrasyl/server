using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl;

public class LootRecursionError : Exception
{
    public LootRecursionError() { }

    public LootRecursionError(string message)
        : base(message) { }

    public LootRecursionError(string message, Exception inner)
        : base(message, inner) { }
}

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

/// <summary>
///     Resolve loot tables, sets, droprates, etc etc into, you know. Loot.
/// </summary>
public static class LootBox
{
    /// <summary>
    ///     Generate a random number in a threadsafe manner.
    /// </summary>
    /// <returns>random double</returns>
    public static double Roll() => Random.Shared.NextDouble();

    /// <summary>
    ///     Given the specified number of rolls and the chance, calculate how many wins (if any) occurred for
    ///     looting purposes.
    /// </summary>
    /// <param name="numRolls">Number of rolls (chances) to win</param>
    /// <param name="chance">The chance (decimalized percentage) of a given roll winning</param>
    /// <returns>The number of wins (successful rolls)</returns>
    public static int CalculateSuccessfulRolls(int numRolls, double chance)
    {
        var wins = 0;
        for (var x = 0; x <= numRolls; x++)
            if (Roll() <= chance)
                wins++;

        return wins;
    }

    /// <summary>
    ///     Generate a random number between two unsigned ints in a threadsafe manner.
    /// </summary>
    /// <param name="a">Lower bound</param>
    /// <param name="b">Upper bound</param>
    /// <returns></returns>
    public static uint RollBetween(uint a, uint b) => (uint) Random.Shared.Next((int) a, (int) b + 1);

    public static Loot CalculateLoot(LootSet set, int rolls, float chance)
    {
        var loot = new Loot(0, 0);
        var tables = new List<LootTable>();

        // If rolls == 0, all tables fire
        if (rolls == 0)
        {
            tables.AddRange(set.Table);
            GameLog.SpawnInfo("Processing loot set {Name}: set rolls == 0, looting", set.Name);
        }

        for (var x = 0; x < rolls; x++)
            if (Roll() <= chance)
            {
                GameLog.SpawnInfo("Processing loot set {Name}: set hit, looting", set.Name);

                // Ok, the set fired. Check the subtables, which can have independent chances.
                // If no chance is present, we simply award something from each table in the set.
                // Note that we do table processing last! We just find the tables that fire here.
                foreach (var setTable in set.Table)
                {
                    // Rolls == 0 (default) means the table is automatically used.
                    if (setTable.Rolls == 0)
                    {
                        tables.Add(setTable);
                        GameLog.SpawnInfo("Processing loot set {Name}: setTable rolls == 0, looting", set.Name);
                        continue;
                    }

                    // Did the subtable hit?
                    for (var y = 0; y < setTable.Rolls; y++)
                    {
                        if (!(Roll() <= setTable.Chance)) continue;
                        tables.Add(setTable);
                        GameLog.SpawnInfo("Processing loot set {Name}: set subtable hit, looting ", set.Name);
                    }
                }
            }
            else
            {
                GameLog.SpawnInfo("Processing loot set {Name}: Set subtable missed", set.Name);
            }

        return tables.Aggregate(loot, func: (current, table) => current + CalculateTable(table));
    }

    /// <summary>
    ///     Calculate loot for a given loot list (can be defined at spawngroup / creature / spawn level)
    /// </summary>
    /// <param name="list"></param>
    /// <returns></returns>
    public static Loot CalculateLoot(LootList list)
    {
        var loot = new Loot(0, 0);
        var tables = new List<LootTable>();
        if (list == null || list.IsEmpty) return loot;

        foreach (var set in list.Set ?? new List<LootImport>())
        {
            // Is the set present?
            GameLog.SpawnInfo("Processing loot set {Name}", set.Name);
            if (Game.World.WorldData.TryGetValue(set.Name, out LootSet lootset))
                loot += CalculateLoot(lootset, set.Rolls, set.Chance);
            else
                GameLog.Warning("Loot set {name} referenced in list, but could not be loaded", set.Name);
        }

        // Now, calculate loot for any tables attached to the spawn
        foreach (var table in list.Table)
        {
            if (table.Rolls == 0)
            {
                tables.Add(table);
                continue;
            }

            for (var z = 0; z <= table.Rolls; z++)
                if (Roll() <= table.Chance)
                    tables.Add(table);
        }

        // Now that we have all tables that fired, we need to calculate actual loot

        foreach (var table in tables)
            loot += CalculateTable(table);

        return loot;
    }

    /// <summary>
    ///     Calculate drops from a specific loot table.
    /// </summary>
    /// <param name="table">The table to be evaluated.</param>
    /// <returns>Loot structure containing Xp/Gold/List of items to be awarded.</returns>
    public static Loot CalculateTable(LootTable table)
    {
        var tableLoot = new Loot(0, 0);
        if (table.Gold != null)
        {
            if (table.Gold.Max != 0)
                tableLoot.Gold += RollBetween(table.Gold.Min, table.Gold.Max);
            else
                tableLoot.Gold += table.Gold.Min;
            GameLog.SpawnInfo("Processing loot: added {Gold} gp", tableLoot.Gold);
        }

        if (table.Xp != null)
        {
            if (table.Xp.Max != 0)
                tableLoot.Xp += RollBetween(table.Xp.Min, table.Xp.Max);
            else
                tableLoot.Xp += table.Xp.Min;
            GameLog.SpawnInfo("Processing loot: added {Xp} xp", tableLoot.Xp);
        }

        // Handle items now
        if (table.Items != null)
            foreach (var itemlist in table.Items)
                tableLoot.Items.AddRange(CalculateItems(itemlist));
        else
            GameLog.SpawnWarning("Loot table is null!");
        return tableLoot;
    }

    /// <summary>
    ///     Given a list of items in a loot table, return the items awarded.
    /// </summary>
    /// <param name="list">LootTableItemList containing items</param>
    /// <returns>List of items</returns>
    public static List<string> CalculateItems(LootTableItemList list)
    {
        // Ordinarily, return one item from the list.
        var rolls = CalculateSuccessfulRolls(list.Rolls, list.Chance);
        var loot = new List<LootItem>();
        var itemList = new List<ItemObject>();

        // First, process any "always" items, which always drop when the container fires
        foreach (var item in list.Item.Where(predicate: i => i.Always))
        {
            GameLog.SpawnInfo("Processing loot: added always item {item}", item.Value);
            loot.Add(item);
        }

        var totalRolls = 0;

        // Process the rest of the rolls now
        do
        {
            // Get a random item from the list
            var item = list.Item.Where(predicate: i => !i.Always).PickRandom();
            // As soon as we get an item from our table, we've "rolled"; we'll add another roll below if needed
            rolls--;
            totalRolls++;
            // As a check against something incredibly stupid in XML, we only allow a maximum of
            // 100 rolls
            if (totalRolls > 100)
                throw new LootRecursionError("Maximum number of rolls (100) exceeded!");
            // Check uniqueness. 

            if (item.Unique && loot.Contains(item))
                continue;

            // Check max quantity.
            if (item.Max > 0 && loot.Count(predicate: i => i.Value == item.Value) >= item.Max)
                continue;

            // If quantity and uniqueness are good, add the item
            loot.Add(item);
        } while (rolls > 0);

        // Now we have the canonical droplist, which needs resolving into Items

        foreach (var lootitem in loot)
        {
            // Does the base item exist?
            var xmlItemList = Game.World.WorldData.FindItem(lootitem.Value).ToList();
            // Don't handle the edge case of multiple genders .... yet
            if (xmlItemList.Count != 0)
            {
                var xmlItem = xmlItemList.First();
                // Handle variants.
                // If multiple variants are specified, we pick one at random
                if (lootitem.Variants.Any())
                {
                    // Determine overlap between available variants and specified variants
                    var lootedVariant = lootitem.Variants.PickRandom();
                    if (xmlItem.Variants?.TryGetValue(lootedVariant, out var variantItems) ?? false)
                        itemList.Add(Game.World.CreateItem(variantItems.PickRandom().Id));
                    else
                        GameLog.SpawnError("Loot: variant group {name} specified for {xmlItem.Name} but that item does not have the specified variant", lootedVariant);
                }
                else
                {
                    itemList.Add(Game.World.CreateItem(xmlItem.Id));
                }
            }
            else
            {
                GameLog.SpawnError("Spawn loot calculation: item {name} not found!", lootitem.Value);
            }
        }

        // We store loot as strings inside mobs to avoid having tens or hundreds of thousands of ItemObjects or
        // Items lying around - they're made into real objects at the time of mob death
        return itemList.Count > 0
            ? itemList.Where(predicate: x => x != null).Select(selector: y => y.Name).ToList()
            : new List<string>();
    }
}