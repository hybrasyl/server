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
using Hybrasyl.Interfaces;
using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Subsystems.Scripting;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using Script = Hybrasyl.Subsystems.Scripting.Script;

namespace Hybrasyl.Casting;

[MoonSharpUserData]
public class CastableObject : IInteractable, IStateStorable
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