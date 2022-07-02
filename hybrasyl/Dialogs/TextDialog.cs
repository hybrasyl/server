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

    public override void ShowTo(User invoker, IInteractable origin)
    {
        Log.Debug("active for input dialog: {TopCaption}, {InputLength}, {BottomCaption}", TopCaption, InputLength, BottomCaption);
        var dialogPacket = base.GenerateBasePacket(invoker, origin);
        dialogPacket.WriteString8(TopCaption);
        dialogPacket.WriteByte((byte)InputLength);
        dialogPacket.WriteString8(BottomCaption);
        invoker.Enqueue(dialogPacket);
        RunCallback(invoker, origin);
    }

    public bool HandleResponse(User invoker, string response, IInteractable associateOverride = null, IInteractable origin = null)
    {
        Log.Debug("Response {Response} from player {Invoker}", response, invoker.Name);

        if (Handler != string.Empty)
        {
            // Either we must have an associate already known to us, one must be passed, or we must have a script defined
            if (Sequence.Associate == null && associateOverride == null && Sequence.Script == null)
            {
                Log.Error("InputDialog has no known associate or script...?");
                return false;
            }

            var scriptTarget = GetScript(associateOverride);
            if (scriptTarget == null)
            {
                Log.Error("scriptTarget is null, this should not happen");
                return false;
            }
            var env = new ScriptEnvironment();
            env.Add("player_response", response);
            env.Add("invoker", invoker);
            env.Add("source", origin);
            env.DialogPath = DialogPath;
            LastScriptResult = scriptTarget.ExecuteExpression(Handler, env);

            return Equals(LastScriptResult.Return, DynValue.True) || Equals(LastScriptResult.Return, DynValue.Nil);
        }
        return false;
    }
}
