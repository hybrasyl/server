using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hybrasyl.Casting;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Scripting;
using Serilog;

namespace Hybrasyl.Dialogs;

public class Dialog
{

    private static string _tokenRegex = @"\{\{(?<token>[A-Za-z0-9_-]+)\}\}";
    private Regex _regex;

    protected ushort DialogType;
    public DialogSequence Sequence { get; private set; }
    public int Index;

    public string CallbackExpression { get; set; }
    private ushort _sprite { get; set; }

    private string _displayText { get; set; }

    protected string DialogPath => $"{Sequence.Name}:{GetType().Name}:{Index}";

    public ScriptExecutionResult LastScriptResult { get; set; } = null;

    /// <summary>
    /// Gets the script responsible for handling events. This can be either an associate (e.g. an NPC),
    /// an override, or a global script (set by a dialog's sequence).
    /// Associate override first, then associate, and lastly, a global script.
    /// </summary>
    /// <returns></returns>
    protected Script GetScript(IInteractable AssociateOverride = null)
    {
        if (AssociateOverride is IInteractable io)
            return io.Script;

        var associate = AssociateOverride == null ? Sequence.Associate : AssociateOverride;

        if (associate != null)
            return associate.Script;
        return Sequence.Script ?? null;
    }

    /// <summary>
    /// Using the sequence associate or an override, evaluate the display text and replace
    /// {{foo}} tokens (dialog template variables) with values from the ephemeral store.
    /// </summary>
    /// <param name="target">The user who is receiving the dialog</param>
    /// <param name="AssociateOverride">The associate override, if any, to use as a source for token data</param>
    /// <returns>An evaluated string</returns>
    public string EvaluateDisplayText(User target, IInteractable AssociateOverride = null)
    {
        var matches = _regex.Matches(_displayText);

        if (matches.Count == 0)
            return _displayText;

        var associate = AssociateOverride == null ? Sequence.Associate : AssociateOverride;

        // Handle a few special cases here

        var ret = _displayText;

        if ((Sequence?.Id ?? uint.MaxValue) < Constants.DIALOG_SEQUENCE_SHARED)
        {
            // Global sequence, check for async
            var dialog = Game.World.ActiveAsyncDialogs.Keys.Where(key => key.Item1 == target.Id || key.Item2 == target.Id).First();
            if (dialog != null)
            {
                WorldObject invoker = null;
                if (dialog.Item1 == target.Id)
                    Game.World.Objects.TryGetValue(dialog.Item2, out invoker);
                if (dialog.Item2 == target.Id)
                    Game.World.Objects.TryGetValue(dialog.Item1, out invoker);
                ret = ret.Replace("{{invoker}}", invoker?.Name ?? "System");
            }
        }

        if (associate == null)
            return ret;

        if (associate is IEphemeral ephemeral)
        {
            foreach (Match match in matches)
            {
                GroupCollection groups = match.Groups;
                if (ephemeral.TryGetEphemeral(groups["token"].Value, out dynamic value))
                {
                    ret = ret.Replace("{{" + groups["token"] + "}}", value.ToString());
                    GameLog.ScriptingInfo("{Function}: {Name}: token {Token} replaced with {String}",
                        MethodInfo.GetCurrentMethod().Name, associate.Name, groups["token"], value);
                }
                else
                {
                    GameLog.ScriptingError(
                        "{Function}: {Name}: template script references {Token} which could not be evaluated",
                        MethodInfo.GetCurrentMethod().Name, associate.Name, groups["token"]);
                    continue;
                }
            }
        }

        return ret;
    }

    public ushort Sprite
    {
        get
        {
            if (_sprite == ushort.MaxValue)
                return Sequence?.Associate?.DialogSprite ?? Sequence?.Sprite ?? 0;
            return _sprite;
        }
        set
        {
            _sprite = value;
        }
    }

    public Dialog(int dialogType, string displayText = null, string callbackFunction = "")
    {
        DialogType = (ushort)dialogType;
        _displayText = displayText;
        CallbackExpression = callbackFunction;
        _sprite = ushort.MaxValue; // Client only uses about ~1000 of these values
        _regex = new Regex(_tokenRegex, RegexOptions.Compiled);
    }

    // Any Dialog can have a callback function which can be used to process dialog responses, or
    // take some action after a dialog fires.
    // response. Normally this is set to a resolvable function inside a Hybrasyl script.
    public void SetCallbackHandler(string callback)
    {
        CallbackExpression = callback;
    }

    public ScriptExecutionResult RunCallback(User target, IInteractable associateOverride = null)
    {
        if (string.IsNullOrEmpty(CallbackExpression)) return ScriptExecutionResult.NoExecution;

        var script = GetScript(associateOverride);
        if (script == null) return ScriptExecutionResult.NotFound;
        var env = new ScriptEnvironment();
        env.DialogPath = DialogPath;
        env.Add("invoker", target);
        LastScriptResult = script.ExecuteExpression(CallbackExpression);
        return LastScriptResult;
    }

    public bool HasPrevDialog()
    {
        Log.Debug("Dialog index {Index}, count {Count}", Index, Sequence.Dialogs.Count);
        // Don't allow prev buttons after either input or options dialogs
        if (Index != 0)
        {
            return Sequence.Dialogs.Count() > 1 && Sequence.Dialogs[Index - 1].DialogType == DialogTypes.SIMPLE_DIALOG;
        }
        return false;
    }

    public bool HasNextDialog()
    {
        // Only simple dialogs have next buttons; everything else requires alternate input.
        // In addition, if we are in the last dialog in a sequence, Next should return to 
        // the main menu.
        Log.Debug("Dialog index {Index}, count {Count}", Index, Sequence.Dialogs.Count);
        return (DialogType == DialogTypes.SIMPLE_DIALOG) && (Index + 1 < Sequence.Dialogs.Count);
    }

    public ServerPacket GenerateBasePacket(User invoker, IInteractable source)
    {
        byte color = 0;
        ushort sprite = 0;
        DialogObjectType objType = 0;

        var dialogPacket = new ServerPacket(0x30);
        dialogPacket.WriteByte((byte)(DialogType));

        switch (source)
        {
            case Creature creature:
                sprite = (ushort)(0x4000 + creature.Sprite);
                objType = DialogObjectType.Creature;
                break;
            case ItemObject itemObject:
                {
                    objType = DialogObjectType.ItemObject;
                    sprite = (ushort)(0x8000 + itemObject.Sprite);
                    color = itemObject.Color;
                    break;
                }
            case Reactor r:
                objType = DialogObjectType.Reactor;
                sprite = r.Sprite;
                break;
            case CastableObject co:
                objType = DialogObjectType.CastableObject;
                sprite = co.Sprite;
                break;
        }

        if (sprite == 0)
            // If dialog sprite is unset, try using invokee's sprite; 
            // then try user dialog state (global sequence),
            // and lastly try the sprite for the active sequence itself
            sprite = source?.DialogSprite ?? invoker.DialogState?.Associate?.DialogSprite ??
                invoker.DialogState?.ActiveDialogSequence?.Sprite ?? 0;

        dialogPacket.WriteByte((byte)objType);
        // If no invokee ID, we use 0xFFFFFFFF; 99.9% of the time this is an async dialog request
        dialogPacket.WriteUInt32(source?.Id ?? uint.MaxValue);
        dialogPacket.WriteByte(0); // Unknown value
        GameLog.Info("Sprite is {Sprite}", sprite);
        dialogPacket.WriteUInt16(sprite);
        dialogPacket.WriteByte(color);
        dialogPacket.WriteByte(0); // Unknown value
        dialogPacket.WriteUInt16(sprite);
        dialogPacket.WriteByte(color);
        Log.Debug("Dialog group id {SequenceId}, index {Index}", Sequence.Id, Index);
        dialogPacket.WriteUInt16((ushort)Sequence.Id);
        dialogPacket.WriteUInt16((ushort)Index);

        dialogPacket.WriteBoolean(HasPrevDialog());
        dialogPacket.WriteBoolean(HasNextDialog());

        dialogPacket.WriteByte(0);
        // TODO: Allow override here from DialogSequence
        dialogPacket.WriteString8(source?.Name ?? invoker.DialogState?.Associate?.Name ?? Sequence.DisplayName);
        var displayText = EvaluateDisplayText(invoker, source);

        if (!string.IsNullOrEmpty(displayText))
            dialogPacket.WriteString16(displayText);

        return dialogPacket;
    }

    public void AssociateWithSequence(DialogSequence dialogSequence)
    {
        Sequence = dialogSequence;
    }

    public virtual void ShowTo(User invoker, IInteractable origin)
    {
    }

}