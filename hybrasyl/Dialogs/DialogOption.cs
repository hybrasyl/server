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

namespace Hybrasyl.Dialogs;

public class DialogOption
{
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
        OverrideSequence = sequence;
    }

    public string OptionText { get; }
    private Dialog ParentDialog { get; }

    public string CallbackFunction { get; }

    public JumpDialog JumpDialog { get; set; }
    public DialogSequence OverrideSequence { get; set; }
}