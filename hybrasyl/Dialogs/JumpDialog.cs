using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Serilog;

namespace Hybrasyl.Dialogs;

/// <summary>
/// A JumpDialog is a dialog that actually starts a new sequence. It's particularly useful for when you want a selected option to start a new
/// conversational fork without resorting to using a FunctionDialog.
/// </summary>
public class JumpDialog : Dialog
{
    protected string NextSequence;

    public JumpDialog(string nextSequence) : base(DialogTypes.JUMP_DIALOG)
    {
        NextSequence = nextSequence;
    }

    public override void ShowTo(User invoker, IInteractable origin)
    {
        // Start the sequence in question
        // Look for a local (tied to NPC/reactor) sequence first, then consult the global catalog
        DialogSequence sequence;
        // Depending on how this was registered, it may not have an associate; thankfully we always
        // get a hint of one from the 0x3A packet
        var associate = Sequence?.Associate ?? origin;
        // Consult dialog state as last attempt to find our associate

        // We assume that a callback expression for a jump dialog is a simple one to award xp, set
        // a value, etc. If you use this functionality to modify dialog sequences / change active 
        // sequence you're likely to have strange results.
        RunCallback(invoker, origin);
        if (NextSequence.ToLower().Trim() == "mainmenu")
        {
            // Return to main menu (pursuits)
            invoker.DialogState.EndDialog();
            if (associate is IPursuitable pursuitable)
                pursuitable.DisplayPursuits(invoker);
            return;
        }

        if (associate.SequenceIndex.TryGetValue(NextSequence, out sequence) ||
            Game.World.GlobalSequences.TryGetValue(NextSequence, out sequence))
        {
            // End previous sequence
            invoker.DialogState.EndDialog();
            invoker.DialogState.StartDialog(origin, sequence);
            sequence.ShowTo(invoker, origin);
        }
        else
        {
            // We terminate our dialog state if we encounter an error
            invoker.DialogState.EndDialog();
            invoker.SendSystemMessage($"{origin.Name} seems confused ((scripting error!))...");
            Log.Error("JumpDialog: sequence {NextSequence} not found!", NextSequence);
        }
    }
}
