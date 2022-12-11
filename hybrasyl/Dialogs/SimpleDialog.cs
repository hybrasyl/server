using Hybrasyl.Scripting;

namespace Hybrasyl.Dialogs;

public class SimpleDialog : Dialog
{
    public SimpleDialog(string displayText)
        : base(DialogTypes.SIMPLE_DIALOG, displayText) { }

    public override void ShowTo(DialogInvocation invocation)
    {
        var dialogPacket = GenerateBasePacket(invocation);
        invocation.Target.Enqueue(dialogPacket);
        GameLog.Debug("Sending packet to {Invoker}", invocation.Target.Name);
        RunCallback(invocation);
    }
}