namespace Hybrasyl.Dialogs;

public class DialogOption
{
    public string OptionText { get; private set; }
    private Dialog ParentDialog { get; set; }

    public string CallbackFunction { get; private set; }

    public JumpDialog JumpDialog { get; set; }
    public DialogSequence OverrideSequence { get; set; }

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
}
