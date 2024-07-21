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
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Manufacturing;

public class ManufactureRecipe
{
    public string Name { get; set; }

    public ushort Tile { get; set; }

    public string Description { get; set; }

    public List<ManufactureIngredient> Ingredients { get; set; }

    public string AddItemName { get; set; }

    public bool HasAddItem => AddItemName != null;

    public string IngredientsText
    {
        get
        {
            List<string> ingredientLines = new();
            if (HasAddItem) ingredientLines.Add($"{AddItemName} [add]");
            foreach (var ingredient in Ingredients) ingredientLines.Add(ingredient.ToString());
            return string.Join("\n", ingredientLines);
        }
    }

    public bool Make(User user, int addSlotIndex)
    {
        if (!CheckIngredientsFor(user))
        {
            user.SendSystemMessage("You do not have all the ingredients for that recipe.");
            return false;
        }

        if (HasAddItem && (addSlotIndex < 1 || addSlotIndex > user.Inventory.Size ||
                           user.Inventory[(byte) addSlotIndex]?.Name != AddItemName))
        {
            user.SendSystemMessage($"That recipe requires {AddItemName} to be added to the window.");
            return false;
        }

        user.RemoveItem((byte) addSlotIndex);
        TakeIngredientsFrom(user);
        GiveManufacturedItemTo(user);
        user.SendSystemMessage($"You create {Name}.");

        return true;
    }

    public string HighlightedIngredientsText(User user)
    {
        List<string> ingredientLines = new();
        if (HasAddItem)
        {
            var addItemColorCode = user.Inventory.ContainsName(AddItemName) ? 'c' : 'a';
            ingredientLines.Add($"{{={addItemColorCode}{AddItemName} {{=s[add]");
        }

        foreach (var ingredient in Ingredients) ingredientLines.Add(ingredient.HighlightedText(user));
        return string.Join("\n", ingredientLines);
    }

    public bool CheckIngredientsFor(User user)
    {
        if (HasAddItem && !user.Inventory.ContainsName(AddItemName)) return false;

        foreach (var ingredient in Ingredients)
            if (!ingredient.CheckFor(user))
                return false;

        return true;
    }

    public void TakeIngredientsFrom(User user)
    {
        foreach (var ingredient in Ingredients) ingredient.TakeFrom(user);
    }

    public void GiveManufacturedItemTo(User user)
    {
        user.AddItem(Name);
    }
}