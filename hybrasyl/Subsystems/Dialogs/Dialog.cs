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

using Hybrasyl.Casting;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Scripting;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Hybrasyl.Subsystems.Dialogs;

public class Dialog
{
    private static readonly string _tokenRegex = @"\{\{(?<token>[A-Za-z0-9_-]+)\}\}";
    private readonly Regex _regex;

    protected ushort DialogType;
    public int Index;

    public Dialog(int dialogType, string displayText = null, string callbackFunction = "")
    {
        DialogType = (ushort)dialogType;
        _displayText = displayText;
        CallbackExpression = callbackFunction;
        _regex = new Regex(_tokenRegex, RegexOptions.Compiled);
        Sprite = ushort.MinValue;
    }

    public DialogSequence Sequence { get; private set; }

    public string CallbackExpression { get; set; }

    public ushort Sprite { get; set; }

    private string _displayText { get; }

    protected string DialogPath => $"{Sequence.Name}:{GetType().Name}:{Index}";

    public ScriptExecutionResult LastScriptResult { get; set; }

    /// <summary>
    ///     Using the sequence associate or an override, evaluate the display text and replace
    ///     {{foo}} tokens (dialog template variables) with values from the ephemeral store.
    /// </summary>
    /// <param name="invocation">The current DialogInvocation representing the ongoing dialog session</param>
    /// <returns>An evaluated string</returns>
    public string EvaluateDisplayText(DialogInvocation invocation)
    {
        var matches = _regex.Matches(_displayText);

        if (matches.Count == 0)
            return _displayText;

        // Handle a few special cases here

        var ret = _displayText;

        ret = ret.Replace("{{target}}", invocation.Target.Name);
        ret = ret.Replace("{{source}}", invocation.Source.Name);
        ret = ret.Replace("{{origin}}", invocation.Origin.Name);

        if (invocation.Origin is not IEphemeral ephemeral) return ret;

        foreach (Match match in matches)
        {
            var groups = match.Groups;
            if (ephemeral.TryGetEphemeral(groups["token"].Value, out var value))
            {
                ret = ret.Replace("{{" + groups["token"] + "}}", value.ToString());
                GameLog.ScriptingInfo("{Function}: {Name}: token {Token} replaced with {String}",
                    MethodBase.GetCurrentMethod().Name, invocation.Origin.Name, groups["token"], value);
            }
            else
            {
                GameLog.ScriptingError(
                    "{Function}: {Name}: template script references {Token} which could not be evaluated",
                    MethodBase.GetCurrentMethod().Name, invocation.Origin.Name, groups["token"]);
            }
        }

        return ret;
    }

    // Any Dialog can have a callback function which can be used to process dialog responses, or
    // take some action after a dialog fires.
    // response. Normally this is set to a resolvable function inside a Hybrasyl script.
    public void SetCallbackHandler(string callback)
    {
        CallbackExpression = callback;
    }

    public ScriptExecutionResult RunCallback(DialogInvocation invocation)
    {
        if (string.IsNullOrEmpty(CallbackExpression)) return ScriptExecutionResult.NoExecution;
        if (invocation.Script == null) return ScriptExecutionResult.NotFound;
        invocation.Environment.DialogPath = DialogPath;
        LastScriptResult = invocation.ExecuteExpression(CallbackExpression);
        return LastScriptResult;
    }

    public bool HasPrevDialog()
    {
        Log.Debug("Dialog index {Index}, count {Count}", Index, Sequence.Dialogs.Count);
        // Don't allow prev buttons after either input or options dialogs
        if (Index != 0)
            return Sequence.Dialogs.Count() > 1 && Sequence.Dialogs[Index - 1].DialogType == DialogTypes.SIMPLE_DIALOG;
        return false;
    }

    public bool HasNextDialog()
    {
        // Only simple dialogs have next buttons; everything else requires alternate input.
        // In addition, if we are in the last dialog in a sequence, Next should return to 
        // the main menu.
        Log.Debug("Dialog index {Index}, count {Count}", Index, Sequence.Dialogs.Count);
        return DialogType == DialogTypes.SIMPLE_DIALOG && Index + 1 < Sequence.Dialogs.Count;
    }

    public ServerPacket GenerateBasePacket(DialogInvocation invocation)
    {
        if (invocation.Origin == null)
            throw new ArgumentNullException(nameof(invocation), "Invocations must have origin");

        byte color = 0;
        ushort sprite = 0;
        DialogObjectType objType = 0;

        var dialogPacket = new ServerPacket(0x30);
        dialogPacket.WriteByte((byte)DialogType);

        switch (invocation.Origin)
        {
            case Creature creature:
                sprite = (ushort)(0x4000 + creature.Sprite);
                objType = DialogObjectType.Creature;
                break;
            case ItemObject itemObject:
                objType = DialogObjectType.ItemObject;
                sprite = (ushort)(0x8000 + itemObject.Sprite);
                color = itemObject.Color;
                break;
            case Reactor r:
                objType = DialogObjectType.Reactor;
                sprite = r.DialogSprite;
                break;
            case CastableObject co:
                objType = DialogObjectType.CastableObject;
                sprite = co.Sprite;
                break;
            case AsyncDialogSession ads:
                objType = DialogObjectType.Asynchronous;
                sprite = ads.DialogSprite;
                break;
        }

        if (sprite == 0)
            sprite = Sprite > 0 ? Sprite : Sequence?.Sprite ?? invocation.Target.DialogState.Associate.DialogSprite;

        dialogPacket.WriteByte((byte)objType);
        dialogPacket.WriteUInt32(invocation.Origin.Id);
        dialogPacket.WriteByte(0); // Unknown value
        GameLog.Debug("Sprite is {Sprite}", sprite);
        GameLog.Debug($"Object type is {objType}");
        dialogPacket.WriteUInt16(sprite);
        dialogPacket.WriteByte(color);
        dialogPacket.WriteByte(0); // Unknown value
        dialogPacket.WriteUInt16(sprite);
        dialogPacket.WriteByte(color);
        GameLog.Debug("Dialog group id {SequenceId}, index {Index}", Sequence.Id, Index);
        dialogPacket.WriteUInt16((ushort)Sequence.Id);
        dialogPacket.WriteUInt16((ushort)Index);

        dialogPacket.WriteBoolean(HasPrevDialog());
        dialogPacket.WriteBoolean(HasNextDialog());

        dialogPacket.WriteByte(0);

        if (invocation.Origin == null)
            dialogPacket.WriteString8(Sequence.DisplayName);
        else 
            dialogPacket.WriteString8(string.IsNullOrWhiteSpace(invocation.Origin.DisplayName) ? invocation.Origin.Name ??
                                  invocation.Target.DialogState?.Associate?.Name ?? Sequence.DisplayName : invocation.Origin.DisplayName);
        var displayText = EvaluateDisplayText(invocation);

        if (!string.IsNullOrEmpty(displayText))
            dialogPacket.WriteString16(displayText);

        return dialogPacket;
    }

    public void AssociateWithSequence(DialogSequence dialogSequence)
    {
        Sequence = dialogSequence;
    }

    public virtual void ShowTo(DialogInvocation invocation) { }
}