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
using log4net;
using MoonSharp.Interpreter;
using System;
using System.Collections;
using System.Collections.Specialized;

namespace Hybrasyl.Scripting
{

    [MoonSharpUserData]
    public class HybrasylDialogOptions
    {
        public OrderedDictionary Options;
        private static readonly ILog ScriptingLogger = LogManager.GetLogger("ScriptingLog");

        public HybrasylDialogOptions()
        {
            Options = new OrderedDictionary();
        }

        public void AddOption(string option, string luaExpr=null)
        {
            Options.Add(option, luaExpr);
        }

        public void AddOption(string option, HybrasylDialog nextDialog)
        {
            if (nextDialog.DialogType == typeof(JumpDialog))
                Options.Add(option, nextDialog);
            else
                ScriptingLogger.Error($"Dialog option {option}: unsupported dialog type {nextDialog.DialogType.Name}");
        }
    }

    [MoonSharpUserData]
    public class HybrasylWorld
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ILog ScriptingLogger = LogManager.GetLogger("ScriptingLog");

        internal World World { get; set; }

        public HybrasylWorld(World world)
        {
            World = world;
        }

        public void WriteLog(string message)
        {
            ScriptingLogger.Info(message);
        }

        public int CurrentInGameYear => HybrasylTime.CurrentYear;
        public string CurrentInGameAge => HybrasylTime.CurrentAge;

        public string InGameTimeFromDelta(int years=0, int months=0, int days=0)
        {
            var now = DateTime.Now;
            var elapsed = new TimeSpan(years * 365 + months * 30 + days, 0, 0, 0);
            var ht = HybrasylTime.ConvertToHybrasyl(now - elapsed);
            return $"{ht.Age} {ht.Year}";
        }


        public HybrasylDialogOptions NewDialogOptions() => new HybrasylDialogOptions();

        public HybrasylDialogSequence NewDialogSequence(string sequenceName, params object[] list)
        {
            var dialogSequence = new HybrasylDialogSequence(sequenceName);
            foreach (var entry in list)
            {
                Logger.InfoFormat("Type is {0}", entry.GetType().ToString());
                if (entry is HybrasylDialog)
                {
                    var newdialog = entry as HybrasylDialog;
                    dialogSequence.AddDialog(newdialog);
                    newdialog.Sequence = dialogSequence.Sequence;
                }
                else
                {
                    ScriptingLogger.Error($"Unknown parameter type {entry.GetType()} passed to NewDialogSequence, ignored");
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
                        ScriptingLogger.Error("Unknown dialog type {0} in NewOptionsDialog - only JumpDialog is allowed currently");
                }
                else if (entry.Value is null)
                    // This is JUST an option, with no callback or jump dialog. The dialog handler will process the option itself.
                    dialog.AddDialogOption(entry.Key as string);
                else
                    ScriptingLogger.Error($"Unknown type {entry.Value.GetType().Name} passed as argument to NewOptionsDialog call");
            }
            if (dialog.OptionCount == 0)
                ScriptingLogger.Warn($"OptionsDialog with no options created. This dialog WILL NOT render. DisplayText follows: {displayText}");
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
