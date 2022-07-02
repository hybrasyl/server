using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Scripting;

namespace Hybrasyl.Dialogs;


/// <summary>
/// This is a derived class which allows a script to insert an arbitrary function (e.g. an effect display, teleport, etc) into a dialog sequence.
/// Its ShowTo is responsible for carrying out the action. In this way, FunctionDialogs can be used exactly the same as all other dialog types.
/// For the purposes of the client, the FunctionDialog is a "hidden" dialog; it runs its command and then calls the next dialog (if any) from its sequence.
/// </summary>
public class FunctionDialog : Dialog
{
    protected string Expression;

    public FunctionDialog(string luaExpr)
        : base(DialogTypes.FUNCTION_DIALOG)
    {
        Expression = luaExpr;
    }

    public override void ShowTo(User invoker, IInteractable origin)
    {
        var script = GetScript(origin);
        var env = ScriptEnvironment.Create(("invoker", invoker), ("origin", origin));
        env.DialogPath = DialogPath;
        LastScriptResult = script.ExecuteExpression(Expression, env);
        // Skip to next dialog in sequence
        Sequence.ShowByIndex(Index + 1, invoker, origin);
    }
}