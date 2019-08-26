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
 * (C) 2015-2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Dialogs;
using MoonSharp.Interpreter;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;

namespace Hybrasyl.Scripting
{

    [MoonSharpUserData]
    public class HybrasylDialogOptions
    {
        public OrderedDictionary Options;

        public HybrasylDialogOptions()
        {
            Options = new OrderedDictionary();
        }

        public void AddOption(string option, string luaExpr = null) => Options.Add(option, luaExpr);

        public void AddOption(string option, HybrasylDialog nextDialog)
        {
            if (nextDialog.DialogType == typeof(JumpDialog))
                Options.Add(option, nextDialog);
            else
                GameLog.Error($"Dialog option {option}: unsupported dialog type {nextDialog.DialogType.Name}");
        }

        public void AddOption(string option, HybrasylDialogSequence sequence) => Options.Add(option, sequence);
    }

    [MoonSharpUserData]
    public class HybrasylWorld
    {

        internal World World { get; set; }

        public HybrasylWorld(World world)
        {
            World = world;
        }

        public void WriteLog(string message)
        {
            GameLog.Info(message);
        }

        public int CurrentInGameYear => HybrasylTime.CurrentYear;
        public string CurrentInGameAge => HybrasylTime.CurrentAgeName;

        public HybrasylTime CurrentTime()
        {
            var ht = HybrasylTime.Now;
            return ht;
        }

        public HybrasylDialogOptions NewDialogOptions() => new HybrasylDialogOptions();

        public HybrasylDialogSequence NewDialogSequence(string sequenceName, params object[] list)
        {
            var dialogSequence = new HybrasylDialogSequence(sequenceName);
            foreach (var entry in list)
            {
                GameLog.InfoFormat("Type is {0}", entry.GetType().ToString());
                if (entry is HybrasylDialog)
                {
                    var newdialog = entry as HybrasylDialog;
                    dialogSequence.AddDialog(newdialog);
                    newdialog.Sequence = dialogSequence.Sequence;
                }
                else
                {
                    GameLog.Error($"Unknown parameter type {entry.GetType()} passed to NewDialogSequence, ignored");
                }
            }
            return dialogSequence;
        }

        public HybrasylDialog NewDialog(string displayText, string callback = null)
        {
            var dialog = new SimpleDialog(displayText);
            dialog.SetCallbackHandler(callback);
            return new HybrasylDialog(dialog);
        }

        /// <summary>
        /// Create a new dialog sequence consisting of a bunch of simple text dialogs.
        /// </summary>
        /// <param name="sequenceName">The name of the constructed sequence.</param>
        /// <param name="textList">A string array of dialog lines that will be used to construct each dialog in the sequence.</param>
        /// <returns>The constructed dialog seqeunce</returns>
        public HybrasylDialogSequence NewSimpleDialogSequence(string sequenceName, params string[] textList)
        {
            var sequence = new DialogSequence(sequenceName);
            foreach (var entry in textList)
            {
                sequence.AddDialog(new SimpleDialog(entry));
            }
            return new HybrasylDialogSequence(sequence);
        }

        /// <summary>
        /// Create a new dialog sequence consisting of a simple dialog and a jump to a new sequence. Useful 
        /// for a lot of dialogs where you need to display one dialog and go back to the main menu.
        /// </summary>
        /// <param name="simpleDialog">Text for the simple dialog.</param>
        /// <param name="jumpTarget">The new sequence to start after the user hits next on the simple dialog.</param>
        /// <param name="callback">An optional Lua callback expression that will be attached to the simple dialog.</param>
        /// <param name="name">An optional name to give the dialog sequence.</param>
        /// <returns>The constructed dialog sequence</returns>
        public HybrasylDialogSequence NewTextAndJumpDialog(string simpleDialog, string jumpTarget, string callback = "", string name = null)
        {
            DialogSequence sequence;
            if (name == null)
                sequence = new DialogSequence(Guid.NewGuid().ToString());
            else
                sequence = new DialogSequence(name);
            var dialog = new SimpleDialog(simpleDialog);

            if (!string.IsNullOrEmpty(callback))
                dialog.CallbackExpression = callback;

            sequence.AddDialog(dialog);
            sequence.AddDialog(new JumpDialog(jumpTarget));
            return new HybrasylDialogSequence(sequence);
        }

        /// <summary>
        /// Another convenience function to generate an "end" sequence where the user must hit close (e.g. a dialog end). 
        /// This is useful to make a jumpable end to a previous dialog option.
        /// </summary>
        /// <param name="simpleDialog">The text of the simple dialog.</param>
        /// <param name="callback">An optional Lua callback expression that will be attached to the simple dialog.</param>
        /// <param name="name">An optional name to give the dialog sequence.</param>
        /// <returns>The constructed dialog sequence</returns>
        public HybrasylDialogSequence NewEndSequence(string simpleDialog, string callback = "", string name = null)
        {
            DialogSequence sequence;
            if (name == null)
                sequence = new DialogSequence(Guid.NewGuid().ToString());
            else
                sequence = new DialogSequence(name);

            var dialog = new SimpleDialog(simpleDialog);

            if (!string.IsNullOrEmpty(callback))
                dialog.CallbackExpression = callback;

            sequence.AddDialog(dialog);
            return new HybrasylDialogSequence(sequence);
        }
    
        public HybrasylDialog NewTextDialog(string displayText, string topCaption, string bottomCaption, int inputLength = 254, string callback="", string handler="")
        {
            var dialog = new TextDialog(displayText, topCaption, bottomCaption, inputLength);
            dialog.SetInputHandler(handler);
            dialog.SetCallbackHandler(callback);
            return new HybrasylDialog(dialog);
        }

        public HybrasylDialog NewOptionsDialog(string displayText, HybrasylDialogOptions dialogOptions, string callback="", string handler = "")
        {
            var dialog = new OptionsDialog(displayText);
            foreach (DictionaryEntry entry in dialogOptions.Options)
            {
                if (entry.Value is string)
                    // Callback
                    dialog.AddDialogOption(entry.Key as string, entry.Value as string);
                else if (entry.Value is HybrasylDialog)
                {
                    var hd = entry.Value as HybrasylDialog;
                    if (hd.DialogType == typeof(JumpDialog))
                        // Dialog jump
                        dialog.AddDialogOption(entry.Key as string, hd.Dialog as JumpDialog);
                    else
                        GameLog.Error("Unknown dialog type {0} in NewOptionsDialog - only JumpDialog is allowed currently");
                }
                else if (entry.Value is null)
                    // This is JUST an option, with no callback or jump dialog. The dialog handler will process the option itself.
                    dialog.AddDialogOption(entry.Key as string);
                else if (entry.Value is HybrasylDialogSequence)
                {
                    var hds = entry.Value as HybrasylDialogSequence;
                    dialog.AddDialogOption(entry.Key as string, hds.Sequence);
                }
                else
                    GameLog.Error($"Unknown type {entry.Value.GetType().Name} passed as argument to NewOptionsDialog call");
            }
            if (dialog.OptionCount == 0)
                GameLog.Warning($"OptionsDialog with no options created. This dialog WILL NOT render. DisplayText follows: {displayText}");
            dialog.SetInputHandler(handler);
            dialog.SetCallbackHandler(callback);
            return new HybrasylDialog(dialog);
        }

        public HybrasylDialog NewFunctionDialog(string luaExpr)
        {
            return new HybrasylDialog(new FunctionDialog(luaExpr));
        }

        public HybrasylDialog NewJumpDialog(string targetSequence)
        {
            var dialog = new JumpDialog(targetSequence);
            return new HybrasylDialog(dialog);
        }

        public HybrasylSpawn NewSpawn(string creaturename, string spawnname)
        {
            var spawn = new HybrasylSpawn(creaturename, spawnname);
            return spawn;
        }

        public void RegisterGlobalSequence(HybrasylDialogSequence globalSequence)
        {
            Game.World.RegisterGlobalSequence(globalSequence.Sequence);
        }
    }
}
