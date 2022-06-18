using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using Hybrasyl.Dialogs;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting;

public class HybrasylItemObject : HybrasylWorldObject
{
    internal IWorldObject Obj { get; set; }
    internal ItemObject Item => Obj as ItemObject;
    public static bool IsPlayer => false;
    public double Durability => Item.Durability; 
    public uint MaximumDurability => Item.MaximumDurability;
    public int Weight => Item.Weight;
    public int Value => (int)Item.Value;
    public StatInfo Stats => Item.Stats;
    public string Name => Item.Name;
    public List<string> Categories => Item.Categories;
    public int MinLevel => Item.Template.Properties?.Restrictions?.Level?.Min ?? 1;
    public int MaxLevel => Item.Template.Properties?.Restrictions?.Level?.Max ?? 1;
    public string Description => Item.Template.Properties?.Vendor?.Description ?? string.Empty;

    public HybrasylItemObject(ItemObject obj) : base(obj) { }

}