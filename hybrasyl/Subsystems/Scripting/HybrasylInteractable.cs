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

using System.Collections.Generic;
using Hybrasyl.Interfaces;
using Hybrasyl.Subsystems.Dialogs;
using MoonSharp.Interpreter;

namespace Hybrasyl.Subsystems.Scripting;

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
        wrapped.Sequence.Id = (uint) (Sequences.Count + 1);
        Sequences.Add(wrapped.Sequence);
        Index.Add(wrapped.Sequence.Name, wrapped.Sequence);
    }

    public void SetItemSprite(ushort sprite)
    {
        Sprite = (ushort) (0x8000 + sprite);
    }

    public void SetCreatureSprite(ushort sprite)
    {
        Sprite = (ushort) (0x4000 + sprite);
    }
}