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
using System;
using System.Collections.Generic;

namespace Hybrasyl;

public class ManufactureState
{
    private const int NonInventorySlot = 60;

    public ManufactureState(User user)
        : this(user, NonInventorySlot, Array.Empty<ManufactureRecipe>()) { }

    public ManufactureState(User user, int slot)
        : this(user, slot, Array.Empty<ManufactureRecipe>()) { }

    public ManufactureState(User user, IEnumerable<ManufactureRecipe> recipes)
        : this(user, NonInventorySlot, recipes) { }

    public ManufactureState(User user, int slot, IEnumerable<ManufactureRecipe> recipes)
    {
        User = user;
        Slot = slot;
        Recipes = new List<ManufactureRecipe>(recipes);
    }

    public User User { get; }

    public ManufactureType Type { get; }

    public int Slot { get; }

    public List<ManufactureRecipe> Recipes { get; }

    public int SelectedIndex { get; private set; }

    public ManufactureRecipe SelectedRecipe => Recipes[SelectedIndex];

    public void ProcessManufacturePacket(ClientPacket packet)
    {
        var manufactureType = (ManufactureType)packet.ReadByte();
        var slotIndex = packet.ReadByte();

        if (manufactureType != Type || slotIndex != Slot) return;

        var manufacturePacketType = (ManufactureClientPacketType)packet.ReadByte();

        switch (manufacturePacketType)
        {
            case ManufactureClientPacketType.RequestPage:
                var pageIndex = packet.ReadByte();
                if (Math.Abs(SelectedIndex - pageIndex) > 1 || pageIndex >= Recipes.Count) return;
                ShowPage(pageIndex);
                break;
            case ManufactureClientPacketType.Make:
                var recipeName = packet.ReadString8();
                var addSlotIndex = packet.ReadByte();
                if (recipeName != SelectedRecipe.Name) return;
                SelectedRecipe.Make(User, addSlotIndex);
                ShowPage(SelectedIndex);
                break;
        }
    }

    public void ShowWindow()
    {
        var manufacturePacket = new ServerPacket(0x50);
        manufacturePacket.WriteByte((byte)Type);
        manufacturePacket.WriteByte((byte)Slot);
        manufacturePacket.WriteByte((byte)ManufactureServerPacketType.Open);
        manufacturePacket.WriteByte((byte)Recipes.Count);
        User.Enqueue(manufacturePacket);
    }

    public void ShowPage(int pageIndex)
    {
        SelectedIndex = pageIndex;

        var manufacturePacket = new ServerPacket(0x50);
        manufacturePacket.WriteByte((byte)Type);
        manufacturePacket.WriteByte((byte)Slot);
        manufacturePacket.WriteByte((byte)ManufactureServerPacketType.Page);
        manufacturePacket.WriteByte((byte)pageIndex);
        manufacturePacket.WriteUInt16((ushort)(SelectedRecipe.Tile + 0x8000));
        manufacturePacket.WriteString8(SelectedRecipe.Name);
        manufacturePacket.WriteString16(SelectedRecipe.Description);
        manufacturePacket.WriteString16(SelectedRecipe.HighlightedIngredientsText(User));
        manufacturePacket.WriteBoolean(SelectedRecipe.HasAddItem);
        User.Enqueue(manufacturePacket);
    }
}

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
                           user.Inventory[(byte)addSlotIndex]?.Name != AddItemName))
        {
            user.SendSystemMessage($"That recipe requires {AddItemName} to be added to the window.");
            return false;
        }

        user.RemoveItem((byte)addSlotIndex);
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

public class ManufactureIngredient
{
    public ManufactureIngredient(string name, int quantity = 1)
    {
        Name = name;
        Quantity = quantity;
    }

    public string Name { get; set; }

    public int Quantity { get; set; }

    public bool CheckFor(User user) => CheckFor(user, out var _);

    public bool CheckFor(User user, out int onHand)
    {
        onHand = 0;
        foreach (var item in user.Inventory)
            if (item.Name == Name)
                onHand += item.Count;
        return onHand >= Quantity;
    }

    public void TakeFrom(User user)
    {
        user.RemoveItem(Name, (ushort)Quantity);
    }

    public string HighlightedText(User user)
    {
        var colorCode = CheckFor(user, out var onHand) ? 'c' : 'a';
        return $"{{={colorCode}{Name} ({onHand}/{Quantity})";
    }

    public override string ToString() => $"{Name} ({Quantity})";
}

public enum ManufactureType { }

public enum ManufactureClientPacketType
{
    RequestPage = 0,
    Make = 1
}

public enum ManufactureServerPacketType
{
    Open = 0,
    Page = 1
}