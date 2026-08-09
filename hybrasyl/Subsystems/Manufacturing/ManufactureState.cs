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

using DALib.Networking.Packets.Server;
using System;
using System.Collections.Generic;
using Hybrasyl.Networking;
using Hybrasyl.Objects;
using DALib.Networking.Packets.Client;
using Hybrasyl.Networking;

namespace Hybrasyl.Subsystems.Manufacturing;

public class ManufactureState
{
    private const int NonInventorySlot = 60;

    public ManufactureState(User user)
        : this(user, NonInventorySlot, []) { }

    public ManufactureState(User user, int slot)
        : this(user, slot, []) { }

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

    public void ProcessManufacturePacket(InboundPacket packet)
    {
        // ManufacturePacket.Parse self-dispatches on the subtype byte, so the variant carries
        // its own tail; the window tokens are echoed back from the 0x50 that opened it.
        var request = ManufacturePacket.Parse(packet.Body.Span);

        if ((ManufactureType) request.ManufactureType != Type || request.Slot != Slot) return;

        switch (request)
        {
            case RequestManufacturePagePacket page:
                if (Math.Abs(SelectedIndex - page.PageIndex) > 1 || page.PageIndex >= Recipes.Count) return;
                ShowPage(page.PageIndex);
                break;
            case MakeManufacturePacket make:
                if (make.RecipeName != SelectedRecipe.Name) return;
                SelectedRecipe.Make(User, make.AddSlotIndex);
                ShowPage(SelectedIndex);
                break;
        }
    }

    public void ShowWindow()
    {
        // Type/Slot are the session token the client echoes back on every C→S 0x55.
        User.Enqueue(new OpenManufacturePacket
        {
            ManufactureType = (byte) Type,
            Slot = (byte) Slot,
            RecipeCount = (byte) Recipes.Count
        });
    }

    public void ShowPage(int pageIndex)
    {
        SelectedIndex = pageIndex;

        User.Enqueue(new ManufacturePagePacket
        {
            ManufactureType = (byte) Type,
            Slot = (byte) Slot,
            PageIndex = (byte) pageIndex,
            Sprite = (ushort) (SelectedRecipe.Tile + 0x8000),
            RecipeName = SelectedRecipe.Name,
            Description = SelectedRecipe.Description,
            Ingredients = SelectedRecipe.HighlightedIngredientsText(User),
            HasAddItem = SelectedRecipe.HasAddItem
        });
    }
}