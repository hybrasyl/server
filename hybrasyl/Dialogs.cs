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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *
 */

using Hybrasyl.Objects;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Scripting;
using MoonSharp.Interpreter;

namespace Hybrasyl
{
    namespace Dialogs
    {
        public class DialogSequence
        {
            static readonly ILog Logger = LogManager.GetLogger(typeof(DialogSequence));

            public List<Dialog> Dialogs { get; private set; }
            public string Name { get; private set; }
            public uint? Id { get; set; }
            public Scripting.Script Script { get; private set; }
            public WorldObject Associate { get; private set; }
            public string PreDisplayCallback { get; private set; }
            public bool CloseOnEnd { get; set; }

            public DialogSequence(string sequenceName, bool closeOnEnd = false)
            {
                Name = sequenceName;
                Dialogs = new List<Dialog>();
                Id = null;
                CloseOnEnd = closeOnEnd;

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
                    Logger.ErrorFormat("DialogSequence {0} has no known associate or script...?", Name);
                    // Need better error handling here
                    return;
                }
                if (PreDisplayCallback != null && runCheck)
                {
                    var ret = Script.ExecuteAndReturn(PreDisplayCallback, invoker);
                    if (ret == DynValue.True)
                        Dialogs.First().ShowTo(invoker, target);
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
        }

        public class Dialog
        {
            public static readonly ILog Logger = LogManager.GetLogger(typeof(Dialog));
            protected static readonly ILog ScriptingLogger = LogManager.GetLogger("ScriptingLog");

            protected ushort DialogType;
            public DialogSequence Sequence { get; private set; }
            public int Index;
            public string DisplayText { get; set; }
            public string CallbackExpression { get; set; }
            public ushort DisplaySprite { get; set; }

            public Dialog(int dialogType, string displayText = null, string callbackFunction = "")
            {
                DialogType = (ushort)dialogType;
                DisplayText = displayText;
                CallbackExpression = callbackFunction;
                DisplaySprite = 0;
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
                if (CallbackExpression != null)
                {
                    VisibleObject associate = associateOverride == null ? Sequence.Associate as VisibleObject : associateOverride;
                    associate.Script.Execute(CallbackExpression, target);
                }
            }

            public bool HasPrevDialog()
            {
                Logger.DebugFormat("index {0}, count {1}", Index, Sequence.Dialogs.Count());
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
                Logger.DebugFormat("index{0}, count{1}");
                return (DialogType == DialogTypes.SIMPLE_DIALOG) && (Index <= Sequence.Dialogs.Count());
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

                if (DisplaySprite != 0)
                    sprite = DisplaySprite;

                dialogPacket.WriteByte(objType);
                dialogPacket.WriteUInt32(invokee.Id);
                dialogPacket.WriteByte(0); // Unknown value
                Logger.DebugFormat("Sprite is {0}", sprite);
                dialogPacket.WriteUInt16(sprite);
                dialogPacket.WriteByte(color);
                dialogPacket.WriteByte(0); // Unknown value
                dialogPacket.WriteUInt16(sprite);
                dialogPacket.WriteByte(color);
                Logger.DebugFormat("Dialog group id {0}, index {1}", Sequence.Id, Index);
                dialogPacket.WriteUInt16((ushort)Sequence.Id);
                dialogPacket.WriteUInt16((ushort)Index);

                dialogPacket.WriteBoolean(HasPrevDialog());
                dialogPacket.WriteBoolean(HasNextDialog());

                dialogPacket.WriteByte(0);
                dialogPacket.WriteString8(invokee.Name);
                if (DisplayText != null)
                {
                    dialogPacket.WriteString16(DisplayText);
                }
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
                if (associate.SequenceCatalog.TryGetValue(NextSequence, out sequence) || Game.World.GlobalSequencesCatalog.TryGetValue(NextSequence, out sequence))
                {
                    sequence.ShowTo(invoker, invokee);
                    // End previous sequence
                    invoker.DialogState.EndDialog();
                    invoker.DialogState.StartDialog(invokee, sequence);
                }
                else
                {
                    // We terminate our dialog state if we encounter an error
                    invoker.DialogState.EndDialog();
                    invoker.SendSystemMessage($"{invokee.Name} seems confused ((scripting error!))...");
                    ScriptingLogger.Error($"JumpDialog: sequence {NextSequence} not found!");
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
                    VisibleObject associate = invokee == null ? Sequence.Associate as VisibleObject : invokee;
                    associate.Script.Execute(Expression, invoker);
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

            public void HandleResponse(WorldObject invoker, int optionSelected, WorldObject associateOverride = null)
            {
                WorldObject Associate;
                string Expression = string.Empty;

                // Quick sanity check
                if (optionSelected < 0 || optionSelected > Options.Count)
                {
                    Logger.Error($"Option dialog response: invalid player selection {optionSelected}, aborting");
                    if (invoker is User)
                    {
                        var user = invoker as User;
                        user.DialogState.EndDialog();
                        user.SendCloseDialog();
                    }
                    return;
                }

                if (Sequence.Associate != null)
                    Associate = Sequence.Associate;
                else
                    Associate = associateOverride;

                // Note that client is 1-indexed for responses
                if (Options[optionSelected - 1].JumpDialog != null)
                {
                    // Use jump dialog first
                    Options[optionSelected - 1].JumpDialog.ShowTo(invoker as User, Associate as VisibleObject);
                    return;
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
                Associate.Script.SetGlobalValue("player_selection", optionSelected.ToString());
                Associate.Script.SetGlobalValue("player_response", Options[optionSelected - 1].OptionText);
                Associate.Script.Execute(Expression, invoker);
                
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
                Logger.DebugFormat("active for input dialog: {0}, {1}, {2}", TopCaption, InputLength, BottomCaption);
                var dialogPacket = base.GenerateBasePacket(invoker, invokee);
                dialogPacket.WriteString8(TopCaption);
                dialogPacket.WriteByte((byte)InputLength);
                dialogPacket.WriteString8(BottomCaption);
                invoker.Enqueue(dialogPacket);
                RunCallback(invoker, invokee);
            }

            public void HandleResponse(WorldObject invoker, string response, WorldObject associateOverride = null)
            {
                Logger.DebugFormat("Response {0} from player {1}", response, invoker.Name);

                if (Handler != string.Empty)
                {
                    // Either we must have an associate already known to us, one must be passed, or we must have a script defined
                    if (Sequence.Associate == null && associateOverride == null && Sequence.Script == null)
                    {
                        Logger.ErrorFormat("InputDialog has no known associate or script...?");
                        // Need better error handling here
                        return;
                    }

                    var Associate = associateOverride == null ? Sequence.Associate : associateOverride;
                    // Make sure the script can see the response
                    Associate.Script.SetGlobalValue("player_response", response);
                    Associate.Script.Execute(Handler, invoker);

                }
            }
        }


        public class DialogState
        {
            public static readonly ILog Logger = LogManager.GetLogger(typeof(DialogState));

            internal WorldObject Associate { get; private set; }
            internal Dialog ActiveDialog { get; private set; }
            internal DialogSequence ActiveDialogSequence { get; private set; }
            internal User User { get; private set; }

            public uint CurrentPursuitId
            {
                get
                {
                    if (InDialog)
                        return ActiveDialogSequence.Id ?? default(int);
                    else
                        return 0;
                }
            }

            public int CurrentPursuitIndex
            {
                get
                {
                    if (InDialog)
                        return ActiveDialog.Index;
                    else
                        return -1;
                }
            }

            public int CurrentMerchantId
            {
                get
                {
                    if (InDialog)
                        return (int)Associate.Id;
                    else
                        return -1;
                }

            }

            public bool InDialog
            {
                get
                {
                    return (Associate != null &&
                        ActiveDialog != null && ActiveDialogSequence != null);
                }
            }

            public DialogState(User user)
            {
                Associate = null;
                ActiveDialog = null;
                ActiveDialogSequence = null;
                User = user;
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
                    Logger.ErrorFormat("Can't start a dialog with a null dialog ID: {0}", dialogStart.Name);
                    return false;
                }
                if (!InDialog)
                {
                    Associate = target;
                    ActiveDialogSequence = dialogStart;
                    ActiveDialog = dialogStart.Dialogs.First();
                    return true;
                }
                return false;
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
                if (target != Associate || pursuitId != CurrentPursuitId ||
                    target.Map.Id != User.Map.Id || !InDialog)
                {
                    Logger.DebugFormat("{0}: Failed check", User.Name);
                    return false;
                }

                if (newIndex == (ActiveDialog.Index + 1) &&
                    newIndex != ActiveDialogSequence.Dialogs.Count() &&
                    newIndex < (ActiveDialogSequence.Dialogs.Count()))
                {
                    // Next
                    Logger.DebugFormat("Advancing one dialog");
                    ActiveDialog = ActiveDialogSequence.Dialogs[ActiveDialog.Index + 1];
                    Logger.DebugFormat("Active dialog is type {0}", ActiveDialog.GetType());
                    return true;
                }
                else if (newIndex == (ActiveDialog.Index - 1) &&
                    newIndex >= 0)
                {
                    // Previous
                    Logger.DebugFormat("Rewinding one dialog");
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
