using System;
using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using Hybrasyl.ChatCommands;
using Hybrasyl.Dialogs;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.CoreLib;

namespace Hybrasyl.Scripting;

// We implement IInteractable here so that scripting can just work
[MoonSharpUserData]
public class HybrasylItemObject : HybrasylWorldObject, IInteractable
{
    internal ItemObject Item => WorldObject as ItemObject;
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

    public HybrasylItemObject(ItemObject obj) : base(obj) { }

}