using Hybrasyl.Scripting;

namespace Hybrasyl.Dialogs;

/// <summary>
///     Function dialogs allow a script to insert an arbitrary function (e.g. an effect display, teleport, etc) into a
///     dialog sequence.
///     Its ShowTo is responsible for carrying out the action. In this way, FunctionDialogs can be used exactly the same as
///     all other dialog types.
///     For the purposes of the client, the FunctionDialog is a "hidden" dialog; it runs its command and then calls the
///     next dialog (if any) from its sequence.
/// </summary>
public class FunctionDialog : Dialog
{
    protected string Expression;

    public FunctionDialog(string luaExpr)
        : base(DialogTypes.FUNCTION_DIALOG)
    {
        Expression = luaExpr;
    }

    public override void ShowTo(DialogInvocation invocation)
    {
        invocation.Environment.DialogPath = DialogPath;
        LastScriptResult = invocation.ExecuteExpression(Expression);
        // Skip to next dialog in sequence
        Sequence.ShowByIndex(Index + 1, invocation);
    }
}