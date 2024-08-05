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

using Hybrasyl.Internals.Logging;
using Hybrasyl.Subsystems.Scripting;
using MoonSharp.Interpreter;
using System.Collections.Generic;
using System.Linq;
using Script = Hybrasyl.Subsystems.Scripting.Script;

namespace Hybrasyl.Subsystems.Dialogs;

public class DialogSequence(string sequenceName, bool closeOnEnd = false)
{
    private Script _script = null;
    public string ScriptName = string.Empty;

    public List<Dialog> Dialogs { get; } = new();
    public string Name { get; set; } = sequenceName;
    public string DisplayName { get; set; } = string.Empty;
    public uint? Id { get; set; } = null;

    private Dictionary<string, string> Tokens { get; set; }

    public Script Script
    {
        // This allows a form of lazy evaluation to prevent chicken in egg problems with registering
        // dialogs associated with a running script which is in the process of registering said dialogs
        get
        {
            if (_script == null && !string.IsNullOrEmpty(ScriptName))
            {
                if (Game.World.ScriptProcessor.TryGetScript(ScriptName, out var _script)) return _script;

                GameLog.Error($"DialogSequence {Name}: script associate {ScriptName} is missing");
                return null;
            }

            if (_script != null)
                return _script;
            return null;
        }
        set => _script = value;
    }

    public string PreDisplayCallback { get; private set; } = string.Empty;
    public string MenuCheckExpression { get; private set; } = string.Empty;
    public bool CloseOnEnd { get; set; } = closeOnEnd;

    public ushort Sprite { get; set; } = ushort.MinValue;

    /// <summary>
    ///     Show a dialog sequence to a user.
    /// </summary>
    /// <param name="invocation">The DialogInvocation data associated with this current dialog</param>
    public void ShowTo(DialogInvocation invocation, bool runCheck = true)
    {
        if (!string.IsNullOrEmpty(PreDisplayCallback) && runCheck)
        {
            invocation.Environment.DialogPath = Name;
            var ret = invocation.ExecuteExpression(PreDisplayCallback);
            if (ret.Return.Equals(DynValue.True))
                Dialogs.First().ShowTo(invocation);
            else
                // Error, generally speaking
                invocation.Target.ClearDialogState();
        }
        else
        {
            Dialogs.First().ShowTo(invocation);
        }
    }

    public void AddDialog(Dialog dialog)
    {
        dialog.Index = Dialogs.Count();
        dialog.AssociateWithSequence(this);
        Dialogs.Add(dialog);
    }

    public void AddPreDisplayCallback(string check)
    {
        PreDisplayCallback = check;
    }

    public void AddMenuCheckExpression(string check)
    {
        MenuCheckExpression = check;
    }

    /// <summary>
    ///     Skip to the specified index in a dialog sequence.
    /// </summary>
    /// <param name="invocation">The DialogInvocation state associated with the current dialog session</param>
    public void ShowByIndex(int index, DialogInvocation invocation)
    {
        if (index >= Dialogs.Count)
            return;
        Dialogs[index].ShowTo(invocation);
    }
}