﻿namespace Hybrasyl.Dialogs;

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