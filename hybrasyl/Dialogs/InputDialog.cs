namespace Hybrasyl.Dialogs;

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
