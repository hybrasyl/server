using System;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Serilog;

namespace Hybrasyl.Dialogs;

/// <summary>
/// An AsyncDialogRequest is a dialog sequence that is showed to a player based on asynchronous input 
/// from another script, player, or event (such as a mentoring request). 
/// </summary>
public class AsyncDialogRequest
{
    protected string Sequence = string.Empty;
    private DialogSequence _sequence;
    public IInteractable Source;
    public User Target;
    private bool _requireLocal;

    private bool _sourceClosed;
    private bool _targetClosed;

    public bool Complete
    {
        get
        {
            if (Source is User)
                return _targetClosed && _sourceClosed;
            return _targetClosed;
        }
    }

    public bool Ready => _invokeeReady && _invokerReady;

    private bool _invokeeReady => !(Target.Condition.Comatose || Target.Condition.Casting ||
        Target.Condition.InExchange || Target.Condition.Flags.HasFlag(PlayerFlags.InBoard) ||
        (Target.DialogState?.InDialog ?? false));

    private bool _invokerReady
    {
        get
        {
            if (Source is User)
            {
                var user = Source as User;
                return user.Condition.Comatose || user.Condition.Casting ||
                   user.Condition.InExchange || user.Condition.Flags.HasFlag(PlayerFlags.InBoard) ||
                   (user.DialogState?.InDialog ?? false);
            }
            return true;
        }
    }

    public void Close(UInt32 Id)
    {
        if (Source.Id == Id && Source is User)
            _sourceClosed = true;
        if (Target.Id == Id)
            _targetClosed = true;

        if (Complete)
            if (!Game.World.ActiveAsyncDialogs.TryRemove(new Tuple<UInt32, UInt32>(Source.Id, Target.Id), out _))
                GameLog.Error("Warning: couldn't close async dialog between {Invoker} and {Invokee}",
                    Source.Name, Target.Name);

    }

    public AsyncDialogRequest(string sequence, IInteractable source, User target, bool requireLocal = true)
    {
        Sequence = sequence;
        Source = source;
        Target = target;
        _requireLocal = requireLocal;
        _sequence = null;
    }

    public AsyncDialogRequest(DialogSequence sequence, IInteractable source, User target, bool requireLocal = true)
    {
        _sequence = sequence;
        Source = source;
        Target = target;
        _requireLocal = requireLocal;
    }

    private void InvokerError(string message)
    {
        if (Source is not User user) return;
        user.SendSystemMessage(message);
        user.ClearDialogState();
    }

    public void End()
    {
        _sourceClosed = true;
        _targetClosed = true;
        Target.ClearDialogState();
        (Source as User)?.ClearDialogState();
    }

    private bool GetDialogSequence()
    {
        if (_sequence is null)
        {
            if (Source.SequenceIndex.TryGetValue(Sequence, out _sequence) || Game.World.GlobalSequences.TryGetValue(Sequence, out _sequence))
                return true;
            return false;
        }
        return true;
    }

    public bool CheckRequest()
    {
        if (!GetDialogSequence())
        {
            Log.Error("AsyncDialogSequence: requested sequence {sequence} not found");
            return false;
        }

        // The sequence exists, now we do some checks.
        //
        // Do basic checks first.
        // In all cases, the invokee must not be in an existing dialog sequence, or trading, 
        // or reading a board

        if (!_invokeeReady)
        {
            InvokerError("That person is busy.");
            return false;
        }

        if (!_invokerReady)
        {
            InvokerError("You can't do that now.");
            return false;
        }

        // If the request is local, the invokee (dialog recipient) and the invoker must 
        // be on the same map and within ASYNC_DIALOG_DISTANCE of each other.
        //
        if (_requireLocal && Source is VisibleObject)
        {
            var invoker = Source as VisibleObject;
            if (invoker.Map.Id != Target.Map.Id || invoker.Distance(Target) > Constants.ASYNC_DIALOG_DISTANCE)
            {
                InvokerError("You need to be closer to do that.");
                return false;
            }
        }
        // all checks passed, do the thing
        return true;
    }

    // Show the dialog to the recipient
    public bool ShowTo()
    {
        // Do checks one last time, just to be safe
        if (Ready)
        {
            Target.DialogState.StartDialog(Source, _sequence);
            _sequence.ShowTo(Target);
            return true;
        }

        if (!_invokeeReady)
            InvokerError("That person is busy.");
        if (!_invokerReady)
            InvokerError("You can't do that now.");

        return false;
    }
}


