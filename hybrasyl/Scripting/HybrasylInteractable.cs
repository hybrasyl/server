using Hybrasyl.Dialogs;
using Hybrasyl.Interfaces;
using MoonSharp.Interpreter;
using System.Collections.Generic;

namespace Hybrasyl.Scripting;

/// <summary>
///     A scriptable class that can be used to evaluate OnLoad requests to create dialog sequences, which can then
///     be evaluated later (used specifically for items)
/// </summary>
[MoonSharpUserData]
public class HybrasylInteractable : IStateStorable
{
    internal Dictionary<string, DialogSequence> Index = new();
    internal List<DialogSequence> Sequences = new();
    internal ushort Sprite { get; set; }

    public void RegisterDialogSequence(HybrasylDialogSequence wrapped)
    {
        wrapped.Sequence.Id = (uint)(Sequences.Count + 1);
        Sequences.Add(wrapped.Sequence);
        Index.Add(wrapped.Sequence.Name, wrapped.Sequence);
    }

    public void SetItemSprite(ushort sprite)
    {
        Sprite = (ushort)(0x8000 + sprite);
    }

    public void SetCreatureSprite(ushort sprite)
    {
        Sprite = (ushort)(0x4000 + sprite);
    }
}