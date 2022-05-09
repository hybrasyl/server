using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using Hybrasyl.Objects;

namespace Hybrasyl.Scripting;

public class HybrasylItemObject
{

    internal ItemObject Obj { get; set; }
    public static bool IsPlayer => false;
    public double Durability => Obj.Durability; 
    public uint MaximumDurability => Obj.MaximumDurability;
    public int Weight => Obj.Weight;
    public int Value => (int) Obj.Value;
    public StatInfo Stats => Obj.Stats;
    public string Name => Obj.Name;
    public List<string> Categories => Obj.Categories;
    public int MinLevel => Obj.Template.Properties?.Restrictions?.Level?.Min ?? 1;
    public int MaxLevel => Obj.Template.Properties?.Restrictions?.Level?.Max ?? 1;
    public string Description => Obj.Template.Properties?.Vendor?.Description ?? string.Empty;


}