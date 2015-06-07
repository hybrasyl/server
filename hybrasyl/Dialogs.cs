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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using Hybrasyl.Objects;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl
{
    namespace Dialogs
    {
        public class DialogSequence
        {
            public List<Dialog> Dialogs { get; private set; }
            public String Name { get; private set; }
            public uint? Id { get; set; }
            public WorldObject Associate { get; private set; }
            public dynamic PreDisplayCallback { get; private set; }

            public DialogSequence(String sequenceName)
            {
                Name = sequenceName;
                Dialogs = new List<Dialog>();
                Id = null;
            }

            public void ShowTo(User invoker, VisibleObject target = null, bool runCheck = true)
            {
                Dialogs.First().ShowTo(invoker, target);
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

            public void AddPreDisplayCallback(dynamic check)
            {
                PreDisplayCallback = check;
            }
        }
        public class DialogOption
        {
            public String OptionText { get; private set; }
            private Dialog ParentDialog { get; set; }

            public dynamic CallbackFunction { get; private set; }

            public DialogOption(string option, dynamic callback = null, Dialog parentdialog = null)
            {
                OptionText = option;
                CallbackFunction = callback;
                ParentDialog = parentdialog;
            }
        }

        public class Dialog
        {
            public static readonly ILog Logger = LogManager.GetLogger(typeof(Dialog));

            protected ushort DialogType;
            public DialogSequence Sequence { get; private set; }
            public int Index;
            public String DisplayText { get; protected set; }
            public ushort DisplaySprite { get; set; }

            public Dialog(int dialogType, String displayText = null, dynamic callbackFunction = null)
            {
                DialogType = (ushort)dialogType;
                DisplayText = displayText;
                DisplaySprite = 0;
            }


            public bool HasPrevDialog()
            {
                Logger.DebugFormat("index {0}, count {1}", Index, Sequence.Dialogs.Count());
                if (Index != 0)
                {
                    return Sequence.Dialogs.Count() > 1 && Sequence.Dialogs[Index - 1].DialogType == DialogTypes.SIMPLE_DIALOG;
                }
                return false;
            }

            public bool HasNextDialog()
            {
                Logger.DebugFormat("index{0}, count{1}");
                return (Index + 1 < Sequence.Dialogs.Count() && DialogType == DialogTypes.SIMPLE_DIALOG);
            }

            public ServerPacket GenerateBasePacket(User invoker, VisibleObject invokee)
            {
                var color = 0;
                var sprite = 0;
                var objType = 0;

                var dialogPacket = new ServerPacket(0x30);
                dialogPacket.WriteByte((byte)(DialogType));

                if (invokee is Creature)
                {
                    var creature = (Creature)invokee;
                    sprite = (ushort)(0x4000 + creature.Sprite);
                    objType = 1;
                }
                else
                {
                    if (invokee is Item)
                    {
                        var item = (Item)invokee;
                        objType = 2;
                        sprite = (ushort)(0x8000 + item.Sprite);
                        color = item.Color;
                    }
                }
                if (DisplaySprite != 0)
                {
                    sprite = DisplaySprite;
                }
                dialogPacket.WriteByte((byte)objType);
                dialogPacket.WriteUInt32(invokee.Id);
                dialogPacket.WriteByte(0);
                Logger.DebugFormat("Sprite is {0}", sprite);
                dialogPacket.WriteUInt16((ushort)sprite);
                dialogPacket.WriteByte((byte)color);
                dialogPacket.WriteByte(0);
                dialogPacket.WriteUInt16((ushort)sprite);
                dialogPacket.WriteByte((byte)color);
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
        /// This is a derived class which allows a script to insert an arbitrary function (e.g. an effect display, teleport, etc) into a dialog sequence.
        /// Its ShowTo is responsible for carrying out the action. In this way, FunctionDialogs can be used exactly the same as all other dialog types.
        /// </summary>
        internal class FunctionDialog : Dialog
        {
            protected dynamic Function;

            public FunctionDialog(dynamic function)
                : base(DialogTypes.FUNCTION_DIALOG)
            {
                Function = function;
            }

            public override void ShowTo(User invoker, VisibleObject invokee)
            {
            }
        }

        internal class SimpleDialog : Dialog
        {
            public SimpleDialog(String displayText)
                : base(DialogTypes.SIMPLE_DIALOG, displayText)
            {
            }

            public override void ShowTo(User invoker, VisibleObject invokee)
            {
                var dialogPacket = GenerateBasePacket(invoker, invokee);
                invoker.Enqueue(dialogPacket);
            }
        }

        internal class InputDialog : Dialog
        {
            protected dynamic Handler { get; private set; }

            public InputDialog(int dialogType, String displayText)
                : base(dialogType, displayText)
            {
                Handler = null;
            }

            public void setInputHandler(dynamic handler)
            {
                Handler = handler;
            }
        }

        internal class OptionsDialog : InputDialog
        {
            protected List<DialogOption> Options { get; private set; }

            public OptionsDialog(String displayText)
                : base(DialogTypes.OPTIONS_DIALOG, displayText)
            {
                Options = new List<DialogOption>();
            }

            public override void ShowTo(User invoker, VisibleObject invokee)
            {
                var dialogPacket = GenerateBasePacket(invoker, invokee);
                if (Options.Count > 0)
                {
                    dialogPacket.WriteByte((byte)Options.Count);
                    foreach (var option in Options)
                    {
                        dialogPacket.WriteString8(option.OptionText);
                    }
                    invoker.Enqueue(dialogPacket);
                }
            }

            public void AddDialogOption(String option, dynamic callback = null)
            {
                Options.Add(new DialogOption(option, callback));
            }

            public void HandleResponse(WorldObject invoker, int optionSelected, WorldObject associateOverride = null)
            {
            }
        }

        internal class TextDialog : InputDialog
        {
            protected String TopCaption;
            protected String BottomCaption;
            protected int InputLength;

            public TextDialog(String displayText, String topCaption, String bottomCaption, int inputLength)
                : base(DialogTypes.INPUT_DIALOG, displayText)
            {
                TopCaption = topCaption;
                BottomCaption = bottomCaption;
                InputLength = inputLength;
            }

            public override void ShowTo(User invoker, VisibleObject invokee)
            {
                Logger.DebugFormat("active for input dialog: {0}, {1}, {2}", TopCaption, InputLength, BottomCaption);
                var dialogPacket = GenerateBasePacket(invoker, invokee);
                dialogPacket.WriteString8(TopCaption);
                dialogPacket.WriteByte((byte)InputLength);
                dialogPacket.WriteString8(BottomCaption);
                invoker.Enqueue(dialogPacket);
            }

            public void HandleResponse(WorldObject invoker, String response, WorldObject associateOverride = null)
            {
                Logger.DebugFormat("Response {0} from player {1}", response, invoker.Name);
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
                    {
                        return ActiveDialogSequence.Id ?? default(int);
                    }
                    else
                    {
                        return 0;
                    }
                }
            }

            public int CurrentPursuitIndex
            {
                get
                {
                    if (InDialog)
                    {
                        return ActiveDialog.Index;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }

            public int CurrentMerchantId
            {
                get
                {
                    if (InDialog)
                    {
                        return (int)Associate.Id;
                    }
                    else
                    {
                        return -1;
                    }
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
            /// <param name="target">The target merchant.</param>
            /// <param name="pursuitId">A dialog sequence (pursuit) ID.</param>
            /// <param name="newIndex">The index to which we are navigating.</param>
            /// <returns></returns>
            public bool SetDialogIndex(VisibleObject target, int pursuitId, int newIndex)
            {
                if (target != Associate || pursuitId != CurrentPursuitId || !InDialog)
                {
                    Logger.DebugFormat("{0}: Failed check", User.Name);
                    return false;
                }

                if (newIndex == (ActiveDialog.Index + 1) &&
                newIndex != ActiveDialogSequence.Dialogs.Count() &&
                newIndex < (ActiveDialogSequence.Dialogs.Count()))
                {
                    Logger.DebugFormat("Advancing one dialog");
                    ActiveDialog = ActiveDialogSequence.Dialogs[ActiveDialog.Index + 1];
                    Logger.DebugFormat("Active dialog is type {0}", ActiveDialog.GetType());
                    return true;
                }
                else
                {
                    if (newIndex == (ActiveDialog.Index - 1) &&
                    newIndex >= 0)
                    {
                        Logger.DebugFormat("Rewinding one dialog");
                        ActiveDialog = ActiveDialogSequence.Dialogs[ActiveDialog.Index - 1];
                        return true;
                    }
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
                if (!InDialog)
                {
                    return false;
                }
                else
                {
                    ActiveDialog = currentDialog;
                    return true;
                }
            }
        }
    }
}
