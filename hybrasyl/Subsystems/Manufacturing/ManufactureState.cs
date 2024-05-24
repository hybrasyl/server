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

using System;
using System.Collections.Generic;
using Hybrasyl.Networking;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Manufacturing;

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
        var manufactureType = (ManufactureType) packet.ReadByte();
        var slotIndex = packet.ReadByte();

        if (manufactureType != Type || slotIndex != Slot) return;

        var manufacturePacketType = (ManufactureClientPacketType) packet.ReadByte();

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
        manufacturePacket.WriteByte((byte) Type);
        manufacturePacket.WriteByte((byte) Slot);
        manufacturePacket.WriteByte((byte) ManufactureServerPacketType.Open);
        manufacturePacket.WriteByte((byte) Recipes.Count);
        User.Enqueue(manufacturePacket);
    }

    public void ShowPage(int pageIndex)
    {
        SelectedIndex = pageIndex;

        var manufacturePacket = new ServerPacket(0x50);
        manufacturePacket.WriteByte((byte) Type);
        manufacturePacket.WriteByte((byte) Slot);
        manufacturePacket.WriteByte((byte) ManufactureServerPacketType.Page);
        manufacturePacket.WriteByte((byte) pageIndex);
        manufacturePacket.WriteUInt16((ushort) (SelectedRecipe.Tile + 0x8000));
        manufacturePacket.WriteString8(SelectedRecipe.Name);
        manufacturePacket.WriteString16(SelectedRecipe.Description);
        manufacturePacket.WriteString16(SelectedRecipe.HighlightedIngredientsText(User));
        manufacturePacket.WriteBoolean(SelectedRecipe.HasAddItem);
        User.Enqueue(manufacturePacket);
    }
}