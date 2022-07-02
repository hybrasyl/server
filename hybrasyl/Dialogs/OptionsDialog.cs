using System.Collections.Generic;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Scripting;
using MoonSharp.Interpreter;
using Serilog;

namespace Hybrasyl.Dialogs;

public class OptionsDialog : InputDialog
{
    protected List<DialogOption> Options { get; private set; }
    public int OptionCount => Options.Count;

    public OptionsDialog(string displayText)
        : base(DialogTypes.OPTIONS_DIALOG, displayText)
    {
        Options = new List<DialogOption>();
    }

    public override void ShowTo(User invoker, IInteractable origin)
    {
        var dialogPacket = base.GenerateBasePacket(invoker, origin);
        if (Options.Count > 0)
        {
            dialogPacket.WriteByte((byte)Options.Count);
            foreach (var option in Options)
            {
                dialogPacket.WriteString8(option.OptionText);
            }
            invoker.Enqueue(dialogPacket);
            RunCallback(invoker, origin);
        }
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

    public bool HandleResponse(User invoker, int optionSelected, IInteractable associateOverride = null, User origin = null)
    {
        IInteractable associate;
        string Expression = string.Empty;
        // Quick sanity check
        if (optionSelected < 0 || optionSelected > Options.Count)
        {
            Log.Error("Option dialog response: invalid player selection {OptionSelected}, aborting", optionSelected);
            return false;
        }

        associate = Sequence.Associate ?? associateOverride;
        // Note that client is 1-indexed for responses
        // If we have a JumpDialog, handle that first

        if (Options[optionSelected - 1].JumpDialog != null)
        {
            // Use jump dialog first
            Options[optionSelected - 1].JumpDialog.ShowTo(invoker, associate);
            return true;
        }

        // If the response is a sequence, start it
        if (Options[optionSelected - 1].OverrideSequence != null)
        {
            var sequence = Options[optionSelected - 1].OverrideSequence;
            invoker.DialogState.TransitionDialog(associate, Options[optionSelected - 1].OverrideSequence);
            sequence.ShowTo(invoker, associate);
        }

        // If the individual options don't have callbacks, use the dialog callback instead.
        if (Handler != null && Options[optionSelected - 1].CallbackFunction == null)
        {
            Expression = Handler;
        }
        else if (Options[optionSelected - 1].CallbackFunction != null)
        {
            Expression = Options[optionSelected - 1].CallbackFunction;
        }
        // Regardless of what handler we use, make sure the script can see the value.
        // We pass everything as string to not make UserData barf, as it can't handle dynamics.
        // For option dialogs we pass both the "number" selected, and the actual text of the button pressed.
        var script = GetScript(associateOverride);
        if (script == null) return false;
        var env = new ScriptEnvironment();
        env.Add("player_selection", optionSelected.ToString());
        env.Add("player_response", Options[optionSelected - 1].OptionText);
        env.Add("invoker", invoker);
        env.Add("origin", origin);
        env.DialogPath = DialogPath;
        LastScriptResult = script.ExecuteExpression(Expression, env);
        return Equals(LastScriptResult.Return, DynValue.True) || Equals(LastScriptResult.Return, DynValue.Nil);
    }
}

