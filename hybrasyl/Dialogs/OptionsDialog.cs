using System.Collections.Generic;
using Hybrasyl.Scripting;
using MoonSharp.Interpreter;
using Serilog;

namespace Hybrasyl.Dialogs;

public class OptionsDialog : InputDialog
{
    public OptionsDialog(string displayText)
        : base(DialogTypes.OPTIONS_DIALOG, displayText)
    {
        Options = new List<DialogOption>();
    }

    protected List<DialogOption> Options { get; }
    public int OptionCount => Options.Count;

    public override void ShowTo(DialogInvocation invocation)
    {
        var dialogPacket = GenerateBasePacket(invocation);
        if (Options.Count <= 0) return;
        dialogPacket.WriteByte((byte) Options.Count);
        foreach (var option in Options) dialogPacket.WriteString8(option.OptionText);
        invocation.Target.Enqueue(dialogPacket);
        RunCallback(invocation);
    }

    public void AddDialogOption(string option, string callback = null)
    {
        Options.Add(new DialogOption(option, callback));
    }

    public void AddDialogOption(string option, JumpDialog jumpTo)
    {
        Options.Add(new DialogOption(option, jumpTo));
    }

    public void AddDialogOption(string option, DialogSequence sequence)
    {
        Options.Add(new DialogOption(option, sequence));
    }

    public bool HandleResponse(int optionSelected, DialogInvocation invocation)
    {
        var Expression = string.Empty;
        // Quick sanity check
        if (optionSelected < 0 || optionSelected > Options.Count)
        {
            Log.Error("Option dialog response: invalid player selection {OptionSelected}, aborting", optionSelected);
            return false;
        }

        // Note that client is 1-indexed for responses
        // If we have a JumpDialog, handle that first

        if (Options[optionSelected - 1].JumpDialog != null)
        {
            // Use jump dialog first
            Options[optionSelected - 1].JumpDialog.ShowTo(invocation);
            return true;
        }

        // If the response is a sequence, start it
        if (Options[optionSelected - 1].OverrideSequence != null)
        {
            var sequence = Options[optionSelected - 1].OverrideSequence;
            invocation.Target.DialogState.TransitionDialog(invocation.Origin,
                Options[optionSelected - 1].OverrideSequence);
            sequence.ShowTo(invocation);
        }

        // If the individual options don't have callbacks, use the dialog callback instead.
        if (Handler != null && Options[optionSelected - 1].CallbackFunction == null)
            Expression = Handler;
        else if (Options[optionSelected - 1].CallbackFunction != null)
            Expression = Options[optionSelected - 1].CallbackFunction;
        // Regardless of what handler we use, make sure the script can see the value.
        // We pass everything as string to not make UserData barf, as it can't handle dynamics.
        // For option dialogs we pass both the "number" selected, and the actual text of the button pressed.
        if (invocation.Script == null) return false;
        invocation.Environment.Add("player_selection", optionSelected.ToString());
        invocation.Environment.Add("player_response", Options[optionSelected - 1].OptionText);
        invocation.Environment.DialogPath = DialogPath;
        LastScriptResult = invocation.ExecuteExpression(Expression);
        return Equals(LastScriptResult.Return, DynValue.True) || Equals(LastScriptResult.Return, DynValue.Nil);
    }
}