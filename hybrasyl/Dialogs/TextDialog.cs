using Grpc.Core;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Scripting;
using MoonSharp.Interpreter;
using Serilog;

namespace Hybrasyl.Dialogs;

public class TextDialog : InputDialog
{
    protected string TopCaption;
    protected string BottomCaption;
    protected int InputLength;

    public TextDialog(string displayText, string topCaption, string bottomCaption, int inputLength)
        : base(DialogTypes.INPUT_DIALOG, displayText)
    {
        TopCaption = topCaption;
        BottomCaption = bottomCaption;
        InputLength = inputLength;
    }

    public override void ShowTo(DialogInvocation invocation)
    {
        Log.Debug("active for input dialog: {TopCaption}, {InputLength}, {BottomCaption}", TopCaption, InputLength, BottomCaption);
        var dialogPacket = base.GenerateBasePacket(invocation);
        dialogPacket.WriteString8(TopCaption);
        dialogPacket.WriteByte((byte)InputLength);
        dialogPacket.WriteString8(BottomCaption);
        invocation.Target.Enqueue(dialogPacket);
        RunCallback(invocation);
    }

    public bool HandleResponse(string response, DialogInvocation invocation)
    {
        Log.Debug("Response {Response} from player {Invoker}", response, invocation.Source.Name);

        if (Handler == string.Empty) return false;

        if (invocation.Script == null)
        {
            Log.Error("Invocation script is null, this should not happen");
            return false;
        }
        invocation.Environment.Add("player_response", response);
        invocation.Environment.DialogPath = DialogPath;
        LastScriptResult = invocation.ExecuteExpression(Handler);

        return Equals(LastScriptResult.Return, DynValue.True) || Equals(LastScriptResult.Return, DynValue.Nil);
    }
}
