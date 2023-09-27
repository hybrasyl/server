using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Scripting;
using Serilog;
using System.Linq;

namespace Hybrasyl.Dialogs;

public class DialogState
{
    public uint? PreviousPursuitId;

    public DialogState(User user)
    {
        Associate = null;
        ActiveDialog = null;
        ActiveDialogSequence = null;
        User = user;
        PreviousPursuitId = null;
    }

    internal IInteractable Associate { get; private set; }
    internal Dialog ActiveDialog { get; private set; }
    internal DialogSequence ActiveDialogSequence { get; private set; }
    internal User User { get; }

    public uint CurrentPursuitId
    {
        get
        {
            if (InDialog)
                return ActiveDialogSequence?.Id ?? default(int);
            return 0;
        }
    }

    public int CurrentPursuitIndex
    {
        get
        {
            if (InDialog)
                return ActiveDialog?.Index ?? default(int);
            return -1;
        }
    }

    public int CurrentMerchantId
    {
        get
        {
            if (InDialog)
                if (CurrentPursuitId == Game.ActiveConfiguration.Constants.DialogSequenceAsync)
                    // Async dialogs have a fixed merchant ID of 0xFFFFFFFF
                    return int.MaxValue;
                else
                    return (int)(Associate?.Id ?? default(int));
            return -1;
        }
    }

    public bool InDialog
    {
        get
        {
            if (ActiveDialog == null || ActiveDialogSequence == null) return false;
            if (ActiveDialogSequence.Id < Game.ActiveConfiguration.Constants.DialogSequenceShared)
                return true;
            return Associate != null;
        }
    }

    public void RunCallback(DialogInvocation invocation)
    {
        ActiveDialog.RunCallback(invocation);
    }

    /// <summary>
    ///     Start a dialog sequence. The player must not already be in a dialog or in
    ///     any other state.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="dialogStart"></param>
    /// <returns></returns>
    public bool StartDialog(IInteractable target, DialogSequence dialogStart)
    {
        if (dialogStart.Id == null)
        {
            Log.Error("Can't start a dialog with a null dialog ID: {DialogName}", dialogStart.Name);
            return false;
        }

        if (!InDialog)
        {
            Associate = target;
            ActiveDialogSequence = dialogStart;
            ActiveDialog = dialogStart.Dialogs.First();
            User.Condition.Flags |= PlayerFlags.InDialog;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Transition between two dialog sequences. This allows us to start a new sequence from an option or
    ///     a response handler, and validate where we've come from.
    /// </summary>
    /// <param name="target">A VisibleObject that is the target of the dialog sequence</param>
    /// <param name="dialogStart">The dialog sequence to which we will transition.</param>
    /// <returns></returns>
    public bool TransitionDialog(IInteractable target, DialogSequence dialogStart)
    {
        if (!InDialog)
        {
            Log.Error("Transition can only occur from an active dialog");
            return false;
        }

        Associate = target;
        PreviousPursuitId = CurrentPursuitId;
        ActiveDialogSequence = dialogStart;
        ActiveDialog = dialogStart.Dialogs.First();
        return true;
    }

    /// <summary>
    ///     Update the WorldObject associated with this dialog state to obj.
    /// </summary>
    /// <param name="obj">The world object which will now be associated with the dialog state.</param>
    internal void UpdateAssociate(IInteractable obj)
    {
        Associate = obj;
    }

    /// <summary>
    ///     Set the index of a current dialog session. This dialog should be either the previous or next dialog
    ///     from the last one (index wise) and the player must be in a dialog.
    /// </summary>
    /// <param name="target">The target merchant.</param>
    /// <param name="pursuitId">A dialog sequence (pursuit) ID.</param>
    /// <param name="newIndex">The index to which we are navigating.</param>
    /// <returns></returns>
    public bool SetDialogIndex(IInteractable target, int pursuitId, int newIndex)
    {
        switch (target)
        {
            // Sanity checking
            case Merchant:
            case Creature:
                {
                    if (target is Creature c && (target != Associate ||
                                                 pursuitId != CurrentPursuitId ||
                                                 c.Map.Id != User.Map.Id || !InDialog))
                    {
                        Log.Error(
                            $"{User.Name}: Failed dialog sanity check: target {c.Name}, current pursuit {CurrentPursuitId}, pursuit {pursuitId}, index {newIndex}, " +
                            $"target map {c.Map.Id}, user map {User.Map.Id}, indialog {InDialog}");
                        return false;
                    }

                    break;
                }
            case ItemObject io:
                {
                    if (User.ActiveDialogSession == null)
                        if (!User.Inventory.Contains(io))
                        {
                            Log.Error(
                                $"{User.Name}: Failed dialog sanity check: item {io.Name} no longer in inventory: current pursuit {CurrentPursuitId}, pursuit {pursuitId}, index {newIndex}, " +
                                $"user map {User.Map.Id}, indialog {InDialog}");
                            return false;
                        }
                }
                break;
        }

        if (target == null && pursuitId > Game.ActiveConfiguration.Constants.DialogSequenceShared)
        {
            Log.Error("{Username}: dialog associate is null but pursuitId is associate-specific");
            return false;
        }

        if (newIndex == ActiveDialog.Index + 1 &&
            newIndex != ActiveDialogSequence.Dialogs.Count &&
            newIndex < ActiveDialogSequence.Dialogs.Count)
        {
            // Next
            Log.Debug("Advancing one dialog");
            ActiveDialog = ActiveDialogSequence.Dialogs[ActiveDialog.Index + 1];
            Log.Debug("Active dialog is type {Type}", ActiveDialog.GetType());
            return true;
        }

        if (newIndex == ActiveDialog.Index - 1 &&
            newIndex >= 0)
        {
            // Previous
            Log.Debug("Rewinding one dialog");
            ActiveDialog = ActiveDialogSequence.Dialogs[ActiveDialog.Index - 1];
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Clear the dialog state, e.g. a user is done with a sequence or has cancelled and
    ///     returned to game.
    /// </summary>
    public void EndDialog()
    {
        Associate = null;
        ActiveDialog = null;
        ActiveDialogSequence = null;
        PreviousPursuitId = null;
        User.Condition.Flags &= ~PlayerFlags.InDialog;
    }

    /// <summary>
    ///     Set the current dialog. This dialog should be either the previous or next dialog
    ///     from the last one, and the player must be in a dialog.
    /// </summary>
    /// <param name="currentDialog">The dialog that is now current.</param>
    /// <returns></returns>
    public bool SetCurrentDialog(Dialog currentDialog)
    {
        // Sanity checking
        if (!InDialog) return false;

        ActiveDialog = currentDialog;
        return true;
    }
}