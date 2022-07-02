using Hybrasyl.Interfaces;
using Hybrasyl.Objects;

namespace Hybrasyl.Dialogs;

public class SimpleDialog : Dialog
{
    public SimpleDialog(string displayText)
        : base(DialogTypes.SIMPLE_DIALOG, displayText)
    { }

    public override void ShowTo(User invoker, IInteractable origin)
    {
        var dialogPacket = base.GenerateBasePacket(invoker, origin);
        invoker.Enqueue(dialogPacket);
        GameLog.Debug("Sending packet to {Invoker}", invoker.Name);
        RunCallback(invoker, origin);
    }

}