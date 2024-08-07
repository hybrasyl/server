﻿// This file is part of Project Hybrasyl.
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

using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Subsystems.Scripting;
using Serilog;

namespace Hybrasyl.Subsystems.Dialogs;

/// <summary>
///     A JumpDialog is a dialog that actually starts a new sequence. It's particularly useful for when you want a selected
///     option to start a new
///     conversational fork without resorting to using a FunctionDialog.
/// </summary>
public class JumpDialog : Dialog
{
    protected string NextSequence;

    public JumpDialog(string nextSequence) : base(DialogTypes.JUMP_DIALOG)
    {
        NextSequence = nextSequence;
    }

    public override void ShowTo(DialogInvocation invocation)
    {
        // We assume that a callback expression for a jump dialog is a simple one to award xp, set
        // a value, etc. If you use this functionality to modify dialog sequences / change active 
        // sequence you're likely to have strange results.
        RunCallback(invocation);
        if (NextSequence.ToLower().Trim() == "mainmenu")
        {
            // Return to main menu (pursuits)
            invocation.Target.DialogState.EndDialog();
            if (invocation.Origin is IPursuitable pursuitable)
                pursuitable.DisplayPursuits(invocation.Target);
            return;
        }

        DialogSequence sequence;

        if (invocation.Origin.SequenceIndex.TryGetValue(NextSequence, out sequence) ||
            Game.World.GlobalSequences.TryGetValue(NextSequence, out sequence))
        {
            // End previous sequence
            invocation.Target.DialogState.EndDialog();
            invocation.Target.DialogState.StartDialog(invocation.Origin, sequence);
            sequence.ShowTo(invocation);
        }
        else
        {
            // We terminate our dialog state if we encounter an error
            invocation.Target.DialogState.EndDialog();
            invocation.Target.SendSystemMessage($"{invocation.Origin.Name} seems confused ((scripting error!))...");
            Log.Error("JumpDialog: sequence {NextSequence} not found!", NextSequence);
        }
    }
}