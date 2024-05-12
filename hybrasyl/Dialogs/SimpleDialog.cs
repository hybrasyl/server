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