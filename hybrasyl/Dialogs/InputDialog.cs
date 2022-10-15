namespace Hybrasyl.Dialogs;

public class InputDialog : Dialog
{
    public InputDialog(int dialogType, string displayText)
        : base(dialogType, displayText)
    {
        Handler = null;
    }

    protected string Handler { get; private set; }

    public void SetInputHandler(string handler)
    {
        Handler = handler;
    }
}