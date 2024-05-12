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
using Hybrasyl.Dialogs;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting;

// We implement IInteractable here so that scripting can just work
[MoonSharpUserData]
public class HybrasylItemObject : HybrasylWorldObject, IInteractable
{
    public HybrasylItemObject(ItemObject obj) : base(obj) { }
    internal ItemObject Item => WorldObject as ItemObject;
    public double Durability => Item.Durability;
    public uint MaximumDurability => Item.MaximumDurability;
    public int Weight => Item.Weight;
    public int Value => (int) Item.Value;
    public StatInfo Stats => Item.Stats;
    public List<string> Categories => Item.Categories;
    public int MinLevel => Item.Template.Properties?.Restrictions?.Level?.Min ?? 1;
    public int MaxLevel => Item.Template.Properties?.Restrictions?.Level?.Max ?? 1;
    public string Description => Item.Template.Properties?.Vendor?.Description ?? string.Empty;

    // ItemFlags exposed here, to make it easier to use in scripting
    public bool Bound => Item.Template.Properties.Flags.HasFlag(ItemFlags.Bound);
    public bool Depositable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Depositable);
    public bool Enchantable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Enchantable);
    public bool Consecratable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Consecratable);
    public bool Tailorable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Tailorable);
    public bool Smithable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Smithable);
    public bool Exchangeable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Exchangeable);
    public bool Vendorable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Vendorable);
    public bool Perishable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Perishable);
    public bool UniqueInventory => Item.Template.Properties.Flags.HasFlag(ItemFlags.UniqueInventory);
    public bool MasterOnly => Item.Template.Properties.Flags.HasFlag(ItemFlags.MasterOnly);
    public bool UniqueEquipped => Item.Template.Properties.Flags.HasFlag(ItemFlags.UniqueEquipped);
    public bool Identifiable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Identifiable);
    public bool Undamageable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Undamageable);
    public bool Consumable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Consumable);

    public bool Stackable => Item.Template.Properties.Stackable.Max > 1;
    public byte StackableMax => Item.Template.Properties.Stackable.Max;


    public ushort Sprite
    {
        get => Item.Sprite;
        set => throw new NotImplementedException();
    }

    public Script Script => Item.Script;

    public List<DialogSequence> DialogSequences
    {
        get => Item.DialogSequences;
        set => throw new NotImplementedException();
    }

    public bool AllowDead => Item.AllowDead;
    public uint Id => Item.Id;

    public Dictionary<string, DialogSequence> SequenceIndex
    {
        get => Item.SequenceIndex;
        set => throw new NotImplementedException();
    }

    public ushort DialogSprite => Item.DialogSprite;
}