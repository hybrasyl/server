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
using IronPython.Runtime;
using log4net;

namespace Hybrasyl.Scripting
{

    public class HybrasylWorld
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        internal World World { get; set; }

        public HybrasylWorld(World world)
        {
            World = world;
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
                else if (entry is PythonFunction)
                {
                    var action = entry as PythonFunction;
                }

            }
            return dialogSequence;
        }

        public HybrasylDialog NewDialog(string displayText, dynamic callback = null)
        {
            var dialog = new SimpleDialog(displayText);
            dialog.SetCallbackHandler(callback);
            return new HybrasylDialog(dialog);
        }

        public HybrasylDialog NewTextDialog(string displayText, string topCaption, string bottomCaption, int inputLength = 254, dynamic handler = null, dynamic callback = null)
        {
            var dialog = new TextDialog(displayText, topCaption, bottomCaption, inputLength);
            dialog.setInputHandler(handler);
            dialog.SetCallbackHandler(callback);
            return new HybrasylDialog(dialog);
        }

        public HybrasylDialog NewOptionsDialog(string displayText, dynamic optionsStructure, dynamic handler = null, dynamic callback = null)
        {
            var dialog = new OptionsDialog(displayText);
            dialog.SetCallbackHandler(callback);

            if (optionsStructure is IronPython.Runtime.List)
            {
                // A simple options dialog with a callback handler for the response
                var optionlist = optionsStructure as IronPython.Runtime.List;
                foreach (var option in optionsStructure)
                {
                    if (option is string)
                    {
                        dialog.AddDialogOption(option as string);
                    }
                }
                if (handler != null)
                {
                    dialog.setInputHandler(handler);
                    Logger.InfoFormat("Input handler associated with dialog");
                }
            }
            else if (optionsStructure is IronPython.Runtime.PythonDictionary)
            {
                var hash = optionsStructure as IronPython.Runtime.PythonDictionary;
                foreach (var key in hash.Keys)
                {
                    if (key is string)
                    {
                        dialog.AddDialogOption(key as string, hash[key]);
                    }
                }

            }
            return new HybrasylDialog(dialog);
        }
    }
}
