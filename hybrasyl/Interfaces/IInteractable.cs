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
using Hybrasyl.Dialogs;
using Hybrasyl.Scripting;

namespace Hybrasyl.Interfaces;

public interface IInteractable : ISprite
{
    public Script Script { get; }
    public string Name { get; }
    public uint Id { get; }
    public bool AllowDead { get; }

    public List<DialogSequence> DialogSequences { get; set; }
    public Dictionary<string, DialogSequence> SequenceIndex { get; set; }
    public ushort DialogSprite { get; }

    public  void RegisterDialogSequence(DialogSequence sequence)
    {
        sequence.Id = (uint) (Game.ActiveConfiguration.Constants.DialogSequencePursuits + DialogSequences.Count);
        //sequence.AssociateSequence(this);
        DialogSequences.Add(sequence);

        if (SequenceIndex.ContainsKey(sequence.Name))
        {
            GameLog.WarningFormat("Dialog sequence {0} is being overwritten", sequence.Name);
            SequenceIndex.Remove(sequence.Name);
        }

        SequenceIndex.Add(sequence.Name, sequence);
    }
}