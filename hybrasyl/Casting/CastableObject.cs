using System;
using System.Collections.Generic;
using Hybrasyl.Dialogs;
using Hybrasyl.Interfaces;
using Hybrasyl.Scripting;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using Script = Hybrasyl.Scripting.Script;

namespace Hybrasyl.Casting;

[MoonSharpUserData]
public class CastableObject : IInteractable
{
    public Guid Guid { get; set; }
    public Castable Template { get; set; }
    public HybrasylInteractable ScriptedDialogs { get; set; }
    public uint Id { get; set; }

    public ushort Sprite
    {
        get => ScriptedDialogs?.Sprite ?? 0;
        set => ScriptedDialogs.Sprite = value;
    }

    public ushort DialogSprite { get; set; }
    public string Name => Template.Name;
    public bool AllowDead => false;
    public Script Script { get; set; }

    public List<DialogSequence> DialogSequences
    {
        get => ScriptedDialogs.Sequences;
        set => throw new NotImplementedException();
    }

    public Dictionary<string, DialogSequence> SequenceIndex
    {
        get => ScriptedDialogs.Index;
        set => throw new NotImplementedException();
    }
}