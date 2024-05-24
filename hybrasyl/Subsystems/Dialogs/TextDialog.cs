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
using MoonSharp.Interpreter;
using Serilog;

namespace Hybrasyl.Subsystems.Dialogs;

public class TextDialog(string displayText, string topCaption, string bottomCaption, int inputLength)
    : InputDialog(DialogTypes.INPUT_DIALOG, displayText)
{
    protected string BottomCaption = bottomCaption;
    protected int InputLength = inputLength;
    protected string TopCaption = topCaption;

    public override void ShowTo(DialogInvocation invocation)
    {
        Log.Debug("active for input dialog: {TopCaption}, {InputLength}, {BottomCaption}", TopCaption, InputLength,
            BottomCaption);
        var dialogPacket = GenerateBasePacket(invocation);
        dialogPacket.WriteString8(TopCaption);
        dialogPacket.WriteByte((byte) InputLength);
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