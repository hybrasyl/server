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
using System.Collections;
using System.Collections.Specialized;

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

        public void AddSelection(string option, string jsexpr)
        {
            Options.Add(option, jsexpr);
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

        public HybrasylDialog NewTextDialog(string displayText, string topCaption, string bottomCaption, int inputLength = 254, string handler="", string callback="")
        {
            var dialog = new TextDialog(displayText, topCaption, bottomCaption, inputLength);
            dialog.SetInputHandler(handler);
            dialog.SetCallbackHandler(callback);
            return new HybrasylDialog(dialog);
        }

        public HybrasylDialog NewOptionsDialog(string displayText, HybrasylDialogOptions dialogOptions, string handler="", string callback="")
        {
            var dialog = new OptionsDialog(displayText);
            foreach (DictionaryEntry entry in dialogOptions.Options)
            {
                dialog.AddDialogOption(entry.Key as string, entry.Value as string);
            }
            dialog.SetInputHandler(handler);
            dialog.SetCallbackHandler(callback);
            return new HybrasylDialog(dialog);
        }

        public HybrasylSpawn NewSpawn(string creaturename, string spawnname)
        {
            var spawn = new HybrasylSpawn(creaturename, spawnname);
            return spawn;
        }
    }
}
