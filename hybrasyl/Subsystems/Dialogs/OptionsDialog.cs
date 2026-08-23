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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Subsystems.Scripting;
using MoonSharp.Interpreter;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Subsystems.Dialogs;

public class OptionsDialog(string displayText) : InputDialog(DialogTypes.OPTIONS_DIALOG, displayText)
{
    protected List<DialogOption> Options { get; } = new();
    public int OptionCount => Options.Count;

    public override void ShowTo(DialogInvocation invocation)
    {
        // A dialog with no options at all is a content error; nothing to show.
        if (Options.Count <= 0) return;

        // Handle check expressions on individual options
        var displayedOptions = Options.Where(x => RunCheckExpression(x, invocation)).ToList();
        if (displayedOptions.Count <= 0)
        {
            // Options exist but every one was hidden, so there is nothing for the client to answer
            // -- and StartDialog has already put the player in a dialog. Returning here would leave
            // them holding a session with no packet from which to close it.
            Log.Warning(
                "Options dialog {DialogPath}: no option passed its check expression, ending the dialog",
                DialogPath);
            invocation.Target.ClearDialogState();
            return;
        }

        var body = new DALib.Networking.Packets.Server.OptionsDialog
        {
            Options = displayedOptions.Select(option => option.OptionText ?? string.Empty).ToList()
        };
        invocation.Target.Enqueue(GenerateBasePacket(invocation, body));
        RunCallback(invocation);
    }

    public bool RunCheckExpression(DialogOption option, DialogInvocation invocation)
    {
        if (string.IsNullOrEmpty(option.CheckExpression)) return true;
        var env = ScriptEnvironment.CreateWithTargetAndSource(invocation.Target, invocation.Source);
        env.DialogPath = $"{invocation.Source.Name}:DisplayPursuits:MenuCheckExpression";
        var ret = invocation.Script.ExecuteExpression(option.CheckExpression, env);
        return ret.Return.CastToBool();
    }

    public void AddDialogOption(DialogOption option) => Options.Add(option);

    public bool HandleResponse(int optionSelected, DialogInvocation invocation)
    {
        var expression = string.Empty;

        // Resolve against the options the client was shown, not the ones this dialog holds. An
        // option its check expression hid is absent from this list, which is what makes the check an
        // authorization rather than a display filter -- and it keeps the client's index meaning the
        // same list in both directions, which it did not when ShowTo filtered and this did not.
        var displayed = Options.Where(x => RunCheckExpression(x, invocation)).ToList();

        // The client is 1-indexed, so zero is out of range rather than the first option.
        if (optionSelected < 1 || optionSelected > displayed.Count)
        {
            Log.Error("Option dialog response: invalid player selection {OptionSelected}, aborting", optionSelected);
            return false;
        }

        var selected = displayed[optionSelected - 1];

        // If we have a JumpDialog, handle that first
        if (selected.JumpDialog != null)
        {
            // Use jump dialog first
            selected.JumpDialog!.ShowTo(invocation);
            return true;
        }

        // If the response is a sequence, start it
        if (selected.OverrideSequence != null)
        {
            var sequence = selected.OverrideSequence!;
            invocation.Target.DialogState.TransitionDialog(invocation.Origin, sequence);
            sequence.ShowTo(invocation);
        }

        // If the individual options don't have callbacks, use the dialog callback instead.
        if (Handler != null && selected.CallbackFunction == null)
            expression = Handler;
        else if (selected.CallbackFunction != null)
            expression = selected.CallbackFunction!;
        // Regardless of what handler we use, make sure the script can see the value.
        // We pass everything as string to not make UserData barf, as it can't handle dynamics.
        // For option dialogs we pass both the "number" selected, and the actual text of the button pressed.
        if (invocation.Script == null) return false;
        invocation.Environment.Add("player_selection", optionSelected.ToString());
        invocation.Environment.Add("player_response", selected.OptionText);
        invocation.Environment.DialogPath = DialogPath;
        LastScriptResult = invocation.ExecuteExpression(expression);
        return Equals(LastScriptResult.Return, DynValue.True) || Equals(LastScriptResult.Return, DynValue.Nil);
    }
}