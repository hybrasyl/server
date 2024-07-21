// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Internals.Enums;
using Hybrasyl.Subsystems.Scripting;

namespace Hybrasyl.Subsystems.Dialogs;

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