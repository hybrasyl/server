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

using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Manufacturing;

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
        user.RemoveItem(Name, (ushort) Quantity);
    }

    public string HighlightedText(User user)
    {
        var colorCode = CheckFor(user, out var onHand) ? 'c' : 'a';
        return $"{{={colorCode}{Name} ({onHand}/{Quantity})";
    }

    public override string ToString() => $"{Name} ({Quantity})";
}