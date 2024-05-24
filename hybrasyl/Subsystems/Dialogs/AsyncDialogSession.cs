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
using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Scripting;

namespace Hybrasyl.Subsystems.Dialogs;

/// <summary>
///     An AsyncDialogSession is a dialog sequence that is showed to a player based on asynchronous input
///     from another script, player, or event (such as a mentoring request).
/// </summary>
public class AsyncDialogSession : IInteractable, IStateStorable
{
    public IVisible Source;

    private bool sourceClosed;
    public User Target;
    private bool targetClosed;

    public AsyncDialogSession(string sequence, IInteractable origin, IVisible source, User target,
        bool requireLocal = true)
    {
        if (!origin.SequenceIndex.ContainsKey(sequence) && !Game.World.GlobalSequences.ContainsKey(sequence))
            throw new ArgumentException($"Sequence {sequence} does not exist");
        StartSequence = sequence;
        Origin = origin;
        Source = source;
        Target = target;
        RequireLocal = requireLocal;
    }

    public IInteractable Origin { get; set; }
    public Guid Guid { get; set; } = Guid.NewGuid();
    public bool RequireLocal { get; set; }

    public string StartSequence { get; set; }

    public bool Complete
    {
        get
        {
            if (Source is User)
                return sourceClosed && targetClosed;
            return targetClosed;
        }
    }

    public bool Ready => sourceReady && targetReady;

    private bool targetReady => (!Target.Condition.Comatose
                                 && !Target.Condition.Casting
                                 && !Target.Condition.InExchange
                                 && !Target.Condition.Flags.HasFlag(PlayerFlags.InBoard)
                                 && !(Target.DialogState?.InDialog ?? false)
                                 && Target.ActiveDialogSession == null) || Target.ActiveDialogSession.Guid == Guid;

    private bool sourceReady
    {
        get
        {
            if (Source is User user)
                return (!user.Condition.Comatose
                        && !user.Condition.Casting
                        && !user.Condition.InExchange
                        && !user.Condition.Flags.HasFlag(PlayerFlags.InBoard)
                        && user.ActiveDialogSession == null) || user.ActiveDialogSession.Guid == Guid;
            return true;
        }
    }

    public string Name => Origin.Name;
    public uint Id { get; set; }
    public Script Script => Origin.Script;
    public bool AllowDead => Origin.AllowDead;

    public ushort Sprite
    {
        get => Origin.Sprite;
        set => throw new NotImplementedException();
    }

    public List<DialogSequence> DialogSequences
    {
        get => Origin.DialogSequences;
        set => throw new NotImplementedException();
    }

    public Dictionary<string, DialogSequence> SequenceIndex
    {
        get => Origin.SequenceIndex;
        set => throw new NotImplementedException();
    }

    public ushort DialogSprite => Origin.DialogSprite;

    public bool IsParticipant(Guid guid) => Target.Guid == guid || Source.Guid == guid;

    private void InvokerError(string message)
    {
        if (Source is not User user) return;
        user.SendSystemMessage(message);
        user.ClearDialogState();
    }

    public bool Start()
    {
        if (Target == null || Source == null || !CheckRequest()) return false;
        Target.ActiveDialogSession = this;
        if (Source is User user)
            user.ActiveDialogSession = this;
        return true;
    }

    public void Close(Guid guid)
    {
        if (Target.Guid == guid)
        {
            targetClosed = true;
            Target.ActiveDialogSession = null;
            Target.ClearDialogState();
        }
        else if (Source.Guid == guid)
        {
            if (Source is not User user) return;
            sourceClosed = true;
            user.ActiveDialogSession = null;
            user.ClearDialogState();
        }
    }

    public bool CheckRequest()
    {
        // The sequence exists, now we do some checks.
        //
        // Do basic checks first.
        // In all cases, the invokee must not be in an existing dialog sequence, or trading, 
        // or reading a board

        if (!targetReady)
        {
            InvokerError("That person is busy.");
            return false;
        }

        if (!sourceReady)
        {
            InvokerError("You can't do that now.");
            return false;
        }

        // If the request is local, the invokee (dialog recipient) and the invoker must 
        // be on the same map and within ASYNC_DIALOG_DISTANCE of each other.
        //
        if ((!RequireLocal || Source.Location.Map.Id == Target.Location.Map.Id) &&
            Source.Distance(Target) <= Game.ActiveConfiguration.Constants.PlayerAsyncDialogDistance) return true;
        InvokerError("You need to be closer to do that.");
        return false;
        // all checks passed, do the thing
    }

    // Show the dialog to the recipient
    public bool ShowTo()
    {
        if (!SequenceIndex.TryGetValue(StartSequence, out var sequenceObj)) return false;
        // Do checks one last time, just to be safe

        if (!targetReady)
            InvokerError("That person is busy.");
        if (!sourceReady)
            InvokerError("You can't do that now.");

        if (!Ready) return false;

        var invocation = new DialogInvocation(this, Target, Source);
        Target.DialogState.StartDialog(this, sequenceObj);
        sequenceObj.ShowTo(invocation);
        return true;
    }
}