/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Objects;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using Serilog;
using System;
using System.Text.RegularExpressions;
using System.Reflection;
using Hybrasyl.Enums;

namespace Hybrasyl
{
    namespace Dialogs
    {
        public class DialogSequence
        {
            public List<Dialog> Dialogs { get; private set; }
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public uint? Id { get; set; }

            private Scripting.Script _script;
            public string ScriptName;

            private Dictionary<string, string> Tokens { get; set; }

            public Scripting.Script Script
            {
                // This allows a form of lazy evaluation to prevent chicken in egg problems with registering
                // dialogs associated with a running script which is in the process of registering said dialogs
                get
                {
                    if (_script == null && !string.IsNullOrEmpty(ScriptName))
                    {
                        if (Game.World.ScriptProcessor.TryGetScript(ScriptName, out Scripting.Script _script))
                            return _script;
                        else
                        {
                            GameLog.Error($"DialogSequence {Name}: script associate {ScriptName} is missing");
                            return null;
                        }
                    }
                    if (_script != null)
                        return _script;
                    return null;
                }
                set { _script = value; }
            }
            
            public WorldObject Associate { get; private set; }
            public string PreDisplayCallback { get; private set; }
            public string MenuCheckExpression { get; private set; }
            public bool CloseOnEnd { get; set; }

            public ushort Sprite { get; set; }

            public DialogSequence(string sequenceName, bool closeOnEnd = false)
            {
                Name = sequenceName;
                Dialogs = new List<Dialog>();
                Id = null;
                CloseOnEnd = closeOnEnd;
                PreDisplayCallback = string.Empty;
                MenuCheckExpression = string.Empty;
                Sprite = ushort.MaxValue;
                ScriptName = string.Empty;
                _script = null;
                DisplayName = string.Empty;
            }

            /// <summary>
            /// Show a dialog sequence to a user.
            /// </summary>
            /// <param name="invoker">The user who will receive the dialog.</param>
            /// <param name="target">A target of the dialog; generally an associate (NPC/reactor tile)</param>
            /// <param name="runCheck">Whether or not to run any pre display checks before displaying the sequence</param>
            public void ShowTo(User invoker, VisibleObject target = null, bool runCheck = true)
            {
                // Either we must have an associate already known to us, one must be passed, or we must have a script defined
                if (Associate == null && target == null && Script == null)
                {
                    Log.Error("DialogSequence {0} has no known associate or script...?", Name);
                    // Need better error handling here
                    return;
                }
                if (!string.IsNullOrEmpty(PreDisplayCallback) && runCheck)
                {
                    var ret = Script.ExecuteAndReturn(PreDisplayCallback, invoker);
                    if (ret == DynValue.True)
                        Dialogs.First().ShowTo(invoker, target);
                    else
                        // Error, generally speaking
                        invoker.ClearDialogState();
                }
                else
                {
                    Dialogs.First().ShowTo(invoker, target);
                }
            }
            /// <summary>
            /// Associate a dialog with an object in the world.
            /// </summary>
            /// <param name="obj"></param>
            public void AssociateSequence(WorldObject obj)
            {
                Associate = obj;
            }

            public void AddDialog(Dialog dialog)
            {
                dialog.Index = Dialogs.Count();
                dialog.AssociateWithSequence(this);
                Dialogs.Add(dialog);
            }

            public void AddPreDisplayCallback(string check)
            {
                PreDisplayCallback = check;
            }

            public void AddMenuCheckExpression(string check)
            {
                MenuCheckExpression = check;
            }

            /// <summary>
            /// Skip to the specified index in a dialog sequence.
            /// </summary>
            /// <param name="index"></param>
            /// <param name="invoker"></param>
            /// <param name="target"></param>
            public void ShowByIndex(int index, User invoker, VisibleObject target = null)
            {
                if (index >= Dialogs.Count)
                    return;
                Dialogs[index].ShowTo(invoker, target);
            }
        }

        public class DialogOption
        {
            public string OptionText { get; private set; }
            private Dialog ParentDialog { get; set; }

            public string CallbackFunction { get; private set; }

            public JumpDialog JumpDialog { get; set; }
            public DialogSequence overrideSequence { get; set; }

            public DialogOption(string option, string callback, Dialog parentdialog = null)
            {
                OptionText = option;
                CallbackFunction = callback;
                ParentDialog = parentdialog;
            }

            public DialogOption(string option, JumpDialog jumpTo, Dialog parentdialog = null)
            {
                OptionText = option;
                JumpDialog = jumpTo;
                ParentDialog = parentdialog;
            }

            public DialogOption(string option, DialogSequence sequence)
            {
                OptionText = option;
                overrideSequence = sequence;
            }


        }

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

            /// <summary>
            /// Gets the script responsible for handling events. This can be either an associate (e.g. an NPC),
            /// an override, or a global script (set by a dialog's sequence).
            /// Associate override first, then associate, and lastly, a global script.
            /// </summary>
            /// <returns></returns>
            protected Scripting.Script GetScript(WorldObject AssociateOverride = null)
            {
                var associate = AssociateOverride == null ? Sequence.Associate : AssociateOverride;

                if (associate != null)
                    return AssociateOverride.Script;
                else if (Sequence.Script != null)
                    return Sequence.Script;

                return null;

            }

            /// <summary>
            /// Using the sequence associate or an override, evaluate the display text and replace
            /// {{foo}} tokens (dialog template variables) with values from the ephemeral store.
            /// </summary>
            /// <param name="target">The user who is receiving the dialog</param>
            /// <param name="AssociateOverride">The associate override, if any, to use as a source for token data</param>
            /// <returns>An evaluated string</returns>
            public string EvaluateDisplayText(User target, WorldObject AssociateOverride = null)
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
                
                foreach (Match match in matches)
                {
                    GroupCollection groups = match.Groups;
                    if (associate.TryGetEphemeral(groups["token"].Value, out dynamic value))
                    {
                        ret = ret.Replace("{{" + groups["token"] + "}}", value.ToString());
                        GameLog.ScriptingInfo("{Function}: {Name}: token {Token} replaced with {String}",
                            MethodInfo.GetCurrentMethod().Name, associate.Name, groups["token"], value);
                    }
                    else
                    {
                        GameLog.ScriptingError("{Function}: {Name}: template script references {Token} which could not be evaluated",
                            MethodInfo.GetCurrentMethod().Name, associate.Name, groups["token"]);
                        continue;
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

            public void RunCallback(User target, VisibleObject associateOverride = null)
            {
                if (!string.IsNullOrEmpty(CallbackExpression))
                {
                    try
                    {
                        if (!GetScript(associateOverride).Execute(CallbackExpression, target))
                            target.ClearDialogState();
                    }
                    catch (Exception ex)
                    {
                        Game.ReportException(ex);
                        GameLog.ScriptingError(ex, "{Function}: callback unhandled exception", MethodInfo.GetCurrentMethod().Name);
                        target.ClearDialogState();
                    }

                }
            }

            public bool HasPrevDialog()
            {
                Log.Debug("Dialog index {Index}, count {Count}", Index, Sequence.Dialogs.Count());
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
                Log.Debug("Dialog index {Index}, count {Count}", Index, Sequence.Dialogs.Count());
                return (DialogType == DialogTypes.SIMPLE_DIALOG) && (Index + 1 < Sequence.Dialogs.Count());
            }

            public ServerPacket GenerateBasePacket(User invoker, VisibleObject invokee)
            {
                byte color = 0;
                ushort sprite = 0;
                byte objType = 0;

                var dialogPacket = new ServerPacket(0x30);
                dialogPacket.WriteByte((byte)(DialogType));

                if (invokee is Creature)
                {
                    var creature = (Creature)invokee;
                    sprite = (ushort)(0x4000 + creature.Sprite);
                    objType = 1;
                }
                else if (invokee is ItemObject)
                {
                    var item = (ItemObject)invokee;
                    objType = 2;
                    sprite = (ushort)(0x8000 + item.Sprite);
                    color = item.Color;
                }
                else if (invokee is Reactor)
                {
                    objType = 4;
                }

                if (Sprite != 0)
                    sprite = Sprite;
                else
                {
                    // If dialog sprite is unset, try using invokee's sprite; 
                    // then try user dialog state (global sequence)
                    sprite = invokee?.DialogSprite ?? invoker.DialogState?.Associate?.DialogSprite ?? 0;
                }

                dialogPacket.WriteByte(objType);
                // If no invokee ID, we use 0xFFFFFFFF; 99.9% of the time this is an async dialog request
                dialogPacket.WriteUInt32(invokee?.Id ?? UInt32.MaxValue);
                dialogPacket.WriteByte(0); // Unknown value
                Log.Debug("Sprite is {Sprite}", sprite);
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
                dialogPacket.WriteString8(invokee?.Name ?? invoker.DialogState?.Associate?.Name ?? Sequence.DisplayName);
                var displayText = EvaluateDisplayText(invoker, invokee);

                if (!string.IsNullOrEmpty(displayText))
                    dialogPacket.WriteString16(displayText);

                return dialogPacket;
            }

            public void AssociateWithSequence(DialogSequence dialogSequence)
            {
                Sequence = dialogSequence;
            }

            public virtual void ShowTo(User invoker, VisibleObject invokee)
            {
            }

        }

        /// <summary>
        /// An AsyncDialogRequest is a dialog sequence that is showed to a player based on asynchronous input 
        /// from another script, player, or event (such as a mentoring request). 
        /// </summary>
        public class AsyncDialogRequest
        {
            protected string Sequence = string.Empty;
            private DialogSequence _sequence;
            public VisibleObject Invoker;
            public User Invokee;
            private bool _requireLocal;

            private bool _invokerClosed;
            private bool _invokeeClosed;

            public bool Complete
            {
                get
                {
                    if (Invoker is User)
                        return _invokeeClosed && _invokerClosed;
                    return _invokeeClosed;
                }
            }

            public bool Ready => _invokeeReady && _invokerReady;

            private bool _invokeeReady => !(Invokee.Condition.Comatose || Invokee.Condition.Casting ||
                Invokee.Condition.InExchange || Invokee.Condition.Flags.HasFlag(PlayerFlags.InBoard) ||
                (Invokee.DialogState?.InDialog ?? false));

            private bool _invokerReady
            {
                get
                {
                    if (Invoker is User)
                    {
                        var user = Invoker as User;
                        return user.Condition.Comatose || user.Condition.Casting ||
                           user.Condition.InExchange || user.Condition.Flags.HasFlag(PlayerFlags.InBoard) ||
                           (user.DialogState?.InDialog ?? false);
                    }
                    return true;
                }
            }

            public void Close(UInt32 Id)
            {
                if (Invoker.Id == Id && Invoker is User)
                    _invokerClosed = true;
                if (Invokee.Id == Id)
                    _invokeeClosed = true;

                if (Complete)
                    if (!Game.World.ActiveAsyncDialogs.TryRemove(new Tuple<UInt32, UInt32>(Invoker.Id, Invokee.Id), out _))
                        GameLog.Error("Warning: couldn't close async dialog between {Invoker} and {Invokee}",
                            Invoker.Name, Invokee.Name);
                      
            }

            public AsyncDialogRequest(string sequence, VisibleObject invoker, User invokee, bool requireLocal = true)
            {
                Sequence = sequence;
                Invoker = invoker;
                Invokee = invokee;
                _requireLocal = requireLocal;
                _sequence = null;              
            }

            public AsyncDialogRequest(DialogSequence sequence, VisibleObject invoker, User invokee, bool requireLocal=true)
            {
                _sequence = sequence;
                Invoker = invoker;
                Invokee = invokee;
                _requireLocal = requireLocal;
            }

            private void InvokerError(string message)
            {
                if (Invoker is User)
                {
                    (Invoker as User).SendSystemMessage(message);
                    (Invoker as User).ClearDialogState();
                }
            }

            public void End()
            {
                _invokerClosed = true;
                _invokeeClosed = true;
                Invokee.ClearDialogState();
                (Invoker as User)?.ClearDialogState();
            }
            
            private bool GetDialogSequence()
            {
                if (_sequence is null)
                {
                    if (Invoker.SequenceCatalog.TryGetValue(Sequence, out _sequence) || Game.World.GlobalSequences.TryGetValue(Sequence, out _sequence))
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
                if (_requireLocal && Invoker is VisibleObject)
                {
                    var invoker = Invoker as VisibleObject;
                    if (invoker.Map.Id != Invokee.Map.Id || invoker.Distance(Invokee) > Constants.ASYNC_DIALOG_DISTANCE)
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
                    Invokee.DialogState.StartDialog(Invoker, _sequence);
                    _sequence.ShowTo(Invokee);
                    return true;
                }

                if (!_invokeeReady)
                    InvokerError("That person is busy.");
                if (!_invokerReady)
                    InvokerError("You can't do that now.");

                return false;
            }
        }


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

            public override void ShowTo(User invoker, VisibleObject invokee)
            {
                // Start the sequence in question
                // Look for a local (tied to NPC/reactor) sequence first, then consult the global catalog
                DialogSequence sequence;
                // Depending on how this was registered, it may not have an associate; thankfully we always
                // get a hint of one from the 0x3A packet
                var associate = Sequence?.Associate is null ? invokee : Sequence.Associate;
                // We assume that a callback expression for a jump dialog is a simple one to award xp, set
                // a value, etc. If you use this functionality to modify dialog sequences / change active 
                // sequence you're likely to have strange results.
                RunCallback(invoker, invokee);
                if (NextSequence.ToLower().Trim() == "mainmenu")
                {
                    // Return to main menu (pursuits)
                    invoker.DialogState.EndDialog();
                    if (associate is VisibleObject)
                        (associate as VisibleObject).DisplayPursuits(invoker);
                    return; 
                }
               if (associate.SequenceCatalog.TryGetValue(NextSequence, out sequence) || Game.World.GlobalSequences.TryGetValue(NextSequence, out sequence))
               {
                    // End previous sequence
                    invoker.DialogState.EndDialog();
                    invoker.DialogState.StartDialog(invokee, sequence);
                    sequence.ShowTo(invoker, invokee);
                }
                else
                {
                    // We terminate our dialog state if we encounter an error
                    invoker.DialogState.EndDialog();
                    invoker.SendSystemMessage($"{invokee.Name} seems confused ((scripting error!))...");
                    Log.Error("JumpDialog: sequence {NextSequence} not found!", NextSequence);
                }
            }


        }
        /// <summary>
        /// This is a derived class which allows a script to insert an arbitrary function (e.g. an effect display, teleport, etc) into a dialog sequence.
        /// Its ShowTo is responsible for carrying out the action. In this way, FunctionDialogs can be used exactly the same as all other dialog types.
        /// For the purposes of the client, the FunctionDialog is a "hidden" dialog; it runs its command and then calls the next dialog (if any) from its sequence.
        /// </summary>
        public class FunctionDialog : Dialog
        {
            protected string Expression;

            public FunctionDialog(string luaExpr)
                : base(DialogTypes.FUNCTION_DIALOG)
            {
                Expression = luaExpr;
            }

            public override void ShowTo(User invoker, VisibleObject invokee)
            {
                if (Expression != null)
                {
                    GetScript(invokee).Execute(Expression, invoker);
                }
                // Skip to next dialog in sequence
                Sequence.ShowByIndex(Index + 1, invoker, invokee);               
            }
        }

        public class SimpleDialog : Dialog
        {
            public SimpleDialog(string displayText)
                : base(DialogTypes.SIMPLE_DIALOG, displayText)
            { }

            public override void ShowTo(User invoker, VisibleObject invokee)
            {
                var dialogPacket = base.GenerateBasePacket(invoker, invokee);
                invoker.Enqueue(dialogPacket);
                GameLog.Debug("Sending packet to {Invoker}", invoker.Name);
                RunCallback(invoker, invokee);
            }

        }

        public class InputDialog : Dialog
        {
            protected string Handler { get; private set; }

            public InputDialog(int dialogType, string displayText)
                : base(dialogType, displayText)
            {
                Handler = null;
            }

            public void SetInputHandler(string handler)
            {
                Handler = handler;
            }
        }

        public class OptionsDialog : InputDialog
        {
            protected List<DialogOption> Options { get; private set; }
            public int OptionCount => Options.Count;

            public OptionsDialog(string displayText)
                : base(DialogTypes.OPTIONS_DIALOG, displayText)
            {
                Options = new List<DialogOption>();
            }

            public override void ShowTo(User invoker, VisibleObject invokee)
            {
                var dialogPacket = base.GenerateBasePacket(invoker, invokee);
                if (Options.Count > 0)
                {
                    dialogPacket.WriteByte((byte)Options.Count);
                    foreach (var option in Options)
                    {
                        dialogPacket.WriteString8(option.OptionText);
                    }
                    invoker.Enqueue(dialogPacket);
                    RunCallback(invoker, invokee);
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

            public bool HandleResponse(WorldObject invoker, int optionSelected, WorldObject associateOverride = null, WorldObject source = null)
            {
                WorldObject Associate;
                string Expression = string.Empty;
                // Quick sanity check
                if (optionSelected < 0 || optionSelected > Options.Count)
                {
                    Log.Error("Option dialog response: invalid player selection {OptionSelected}, aborting", optionSelected);
                    return false;
                }

                if (Sequence.Associate != null)
                    Associate = Sequence.Associate;
                else
                    Associate = associateOverride;
                // Note that client is 1-indexed for responses
                // If we have a JumpDialog, handle that first

                if (Options[optionSelected - 1].JumpDialog != null)
                {
                    // Use jump dialog first
                    Options[optionSelected - 1].JumpDialog.ShowTo(invoker as User, Associate as VisibleObject);
                    return true;
                }

                // If the response is a sequence, start it
                if (Options[optionSelected - 1].overrideSequence != null)
                {
                    var sequence = Options[optionSelected - 1].overrideSequence;
                    if (invoker is User)
                    {
                        var user = invoker as User;
                        // We lazily set this because an option / dialog can be constructed in a variety of places
                        if (sequence.Associate == null)
                            Associate.RegisterDialogSequence(sequence);
                        user.DialogState.TransitionDialog(Associate as VisibleObject, Options[optionSelected - 1].overrideSequence);
                        sequence.ShowTo(user, Associate as VisibleObject);
                    }
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
                script.SetGlobalValue("player_selection", optionSelected.ToString());
                script.SetGlobalValue("player_response", Options[optionSelected - 1].OptionText);
                return script.Execute(Expression, invoker, source);
            }
        }

        class TextDialog : InputDialog
        {
            protected string TopCaption;
            protected string BottomCaption;
            protected int InputLength;

            public TextDialog(string displayText, string topCaption, string bottomCaption, int inputLength)
                : base(DialogTypes.INPUT_DIALOG, displayText)
            {
                TopCaption = topCaption;
                BottomCaption = bottomCaption;
                InputLength = inputLength;
            }

            public override void ShowTo(User invoker, VisibleObject invokee)
            {
                Log.Debug("active for input dialog: {TopCaption}, {InputLength}, {BottomCaption}", TopCaption, InputLength, BottomCaption);
                var dialogPacket = base.GenerateBasePacket(invoker, invokee);
                dialogPacket.WriteString8(TopCaption);
                dialogPacket.WriteByte((byte)InputLength);
                dialogPacket.WriteString8(BottomCaption);
                invoker.Enqueue(dialogPacket);
                RunCallback(invoker, invokee);
            }

            public bool HandleResponse(WorldObject invoker, string response, WorldObject associateOverride = null, WorldObject source = null)
            {
                Log.Debug("Response {Response} from player {Invoker}", response, invoker.Name);

                if (Handler != string.Empty)
                {
                    // Either we must have an associate already known to us, one must be passed, or we must have a script defined
                    if (Sequence.Associate == null && associateOverride == null && Sequence.Script == null)
                    {
                        Log.Error("InputDialog has no known associate or script...?");
                        return false;
                    }

                    var scriptTarget = GetScript(associateOverride);
                    if (scriptTarget == null)
                    {
                        Log.Error("scriptTarget is null, this should not happen");
                        return false;
                    }
                    scriptTarget.SetGlobalValue("player_response", response);
                    return scriptTarget.Execute(Handler, invoker, source);
                }
                return false;
            }
        }


        public class DialogState
        {
            internal WorldObject Associate { get; private set; }
            internal Dialog ActiveDialog { get; private set; }
            internal DialogSequence ActiveDialogSequence { get; private set; }
            internal User User { get; private set; }

            public uint CurrentPursuitId
            {
                get
                {
                    if (InDialog)
                        return ActiveDialogSequence?.Id ?? default(int);
                    else
                        return 0;
                }
            }

            public uint? PreviousPursuitId;

            public int CurrentPursuitIndex
            {
                get
                {
                    if (InDialog)
                        return ActiveDialog?.Index ?? default(int);
                    else
                        return -1;
                }
            }

            public int CurrentMerchantId
            {
                get
                {
                    if (InDialog)
                        if (CurrentPursuitId == Constants.DIALOG_SEQUENCE_ASYNC)
                            // Async dialogs have a fixed merchant ID of 0xFFFFFFFF
                            return int.MaxValue;
                        else
                            return (int) (Associate?.Id ?? default(int));
                    else
                        return -1;
                }

            }

            public bool InDialog
            {
                get
                {
                    if (ActiveDialog != null && ActiveDialogSequence != null)
                        if (ActiveDialogSequence.Id < Constants.DIALOG_SEQUENCE_SHARED)
                            return true;
                        else
                            return Associate != null;
                    return false;
                }
            }

            public DialogState(User user)
            {
                Associate = null;
                ActiveDialog = null;
                ActiveDialogSequence = null;
                User = user;
                PreviousPursuitId = null;
            }

            public void RunCallback(User target, VisibleObject associate)
            {
                ActiveDialog.RunCallback(target, associate);
            }

            /// <summary>
            /// Start a dialog sequence. The player must not already be in a dialog or in
            /// any other state.
            /// </summary>
            /// <param name="target"></param>
            /// <param name="dialogStart"></param>
            /// <returns></returns>
            public bool StartDialog(VisibleObject target, DialogSequence dialogStart)
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
            /// Transition between two dialog sequences. This allows us to start a new sequence from an option or
            /// a response handler, and validate where we've come from.
            /// </summary>
            /// <param name="target">A VisibleObject that is the target of the dialog sequence</param>
            /// <param name="dialogStart">The dialog sequence to which we will transition.</param>
            /// <returns></returns>
            public bool TransitionDialog(VisibleObject target, DialogSequence dialogStart)
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
            /// Update the WorldObject associated with this dialog state to obj.
            /// </summary>
            /// <param name="obj">The world object which will now be associated with the dialog state.</param>
            internal void UpdateAssociate(WorldObject obj)
            {
                Associate = obj;
            }

            /// <summary>
            /// Set the index of a current dialog session. This dialog should be either the previous or next dialog
            /// from the last one (index wise) and the player must be in a dialog.
            /// </summary>
            ///
            /// <param name="target">The target merchant.</param>
            /// <param name="pursuitId">A dialog sequence (pursuit) ID.</param>
            /// <param name="newIndex">The index to which we are navigating.</param>
            /// <returns></returns>

            public bool SetDialogIndex(VisibleObject target, int pursuitId, int newIndex)
            {
                // Sanity checking
                if (target != null && (target != Associate || pursuitId != CurrentPursuitId ||
                    target.Map.Id != User.Map.Id || !InDialog))
                {
                    Log.Error("{Username}: Failed dialog sanity check: target {target}, current pursuit {cpid}, pursuit {pid}, index {index}, target map {targetmap}, user map {usermap}, indialog {dialog}", 
                        User.Name, target.Name, CurrentPursuitId, pursuitId, newIndex, target.Map.Id, User.Map.Id, InDialog);
                    return false;
                }

                if (target == null && pursuitId > Constants.DIALOG_SEQUENCE_SHARED)
                {
                    Log.Error("{Username}: dialog associate is null but pursuitId is associate-specific");
                    return false;
                }

                if (newIndex == (ActiveDialog.Index + 1) &&
                    newIndex != ActiveDialogSequence.Dialogs.Count() &&
                    newIndex < (ActiveDialogSequence.Dialogs.Count()))
                {
                    // Next
                    Log.Debug("Advancing one dialog");
                    ActiveDialog = ActiveDialogSequence.Dialogs[ActiveDialog.Index + 1];
                    Log.Debug("Active dialog is type {Type}", ActiveDialog.GetType());
                    return true;
                }
                else if (newIndex == (ActiveDialog.Index - 1) &&
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
            /// Clear the dialog state, e.g. a user is done with a sequence or has cancelled and
            /// returned to game.
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
            /// Set the current dialog. This dialog should be either the previous or next dialog
            /// from the last one, and the player must be in a dialog.
            /// </summary>
            /// <param name="currentDialog">The dialog that is now current.</param>
            /// <returns></returns>
            public bool SetCurrentDialog(Dialog currentDialog)
            {
                // Sanity checking
                if (!InDialog)
                    return false;
                else
                {
                    ActiveDialog = currentDialog;
                    return true;
                }
            }

        }
    }
}
