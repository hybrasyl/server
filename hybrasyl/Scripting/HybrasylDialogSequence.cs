/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Dialogs;
using MoonSharp.Interpreter;
using System;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
public class HybrasylDialogSequence
{
    internal DialogSequence Sequence { get; private set; }

    public HybrasylDialogSequence(string sequenceName, bool closeOnEnd = false) =>
        Sequence = new DialogSequence(sequenceName, closeOnEnd);

    internal HybrasylDialogSequence(DialogSequence sequence) =>
        Sequence = sequence;

    /// <summary>
    /// Add a dialog to this sequence (at the end of the sequence).
    /// </summary>
    /// <param name="scriptDialog">A dialog that will be added to the end of the sequence.</param>
    public void AddDialog(HybrasylDialog scriptDialog)
    {
        if (scriptDialog is null)
        {
            GameLog.ScriptingError("AddDialog: script dialog (first argument) was null");
            return;
        }
        scriptDialog.AssociateDialogWithSequence(Sequence);
    }
    /// <summary>
    /// Add a display callback to the dialog sequence that will be evaluated before it is displayed.
    /// </summary>
    /// <param name="check">A lua scripting expression to be evaluated</param>
    public void AddDisplayCallback(string check)
    {
        if (string.IsNullOrEmpty(check))
        {
            GameLog.ScriptingError("AddDisplayCallback: lua expression (first argument) is null or empty, ignoring");
            return;
        }
        Sequence.AddPreDisplayCallback(check);
    }
    /// <summary>
    /// Add a menu check to the given dialog sequence. If this is a pursuit, the (boolean) expression will be evaluated before the
    /// dialog is added to the list of pursuits (NPC main menu). 
    /// </summary>
    /// <param name="check">The Lua expression to be evaluated as a check, which should return true or false.</param>
    public void AddMenuCheckExpression(string check)
    {
        if (string.IsNullOrEmpty(check))
        {
            GameLog.ScriptingError("AddMenuCheckExpression: lua expression (first argument) is null or empty, ignoring");
            return;
        }
        Sequence.AddMenuCheckExpression(check);
    }
    /// <summary>
    /// Set an NPC / creature display sprite for this sequence (will be used for all of its contained dialogs).
    /// This is the sprite that is displayed on the left hand side when a user views a dialog.
    /// </summary>
    /// <param name="displaySprite">Integer representing the creature display sprite in the client datfiles.</param>
    public void SetNpcDisplaySprite(int displaySprite) =>
        Sequence.Sprite = (ushort)(0x4000 + displaySprite);

    /// <summary>
    /// Set an item display sprite for this sequence (will be used for all of its contained dialogs).
    /// This is the sprite that is displayed on the left hand side when a user views a dialog.
    /// </summary>
    /// <param name="displaySprite">Integer representing the item display sprite in the client datfiles.</param>
    public void SetItemDisplaySprite(int displaySprite) =>
        Sequence.Sprite = (ushort)(0x8000 + displaySprite);

    /// <summary>
    /// Set the display name for this sequence, which is the text that will be displayed if this sequence is
    /// listed from the main menu (e.g. is a pursuit).
    /// </summary>
    /// <param name="displayName"></param>
    public void SetDisplayName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            GameLog.ScriptingError("SetDisplayName: display name (first argument) is null or empty, setting to 'I-AM-ERROR'");
            Sequence.DisplayName = "I-AM-ERROR";
            return;
        }
        Sequence.DisplayName = displayName;
    }
    /// <summary>
    /// Associate a named script with this dialog sequence. This can be used to override processing or provide 
    /// more detailed custom scripting.
    /// </summary>
    /// <param name="scriptName">The name of a script known to Hybrasyl.</param>
    public void AssociateWithScript(string scriptName)
    {
        if (string.IsNullOrEmpty(scriptName))
        {
            GameLog.ScriptingError("AssociateWithScript: script name (first argument) is null or empty, ignoring");
            return;
        }
        // Clear any existing script; the next access to Script will 
        // result in the object being evaluated from the passed name.
        // This handles several edge cases of a running script trying to
        // register or associate dialogs to itself.
        Sequence.Script = null;
        Sequence.ScriptName = scriptName;
    }
}