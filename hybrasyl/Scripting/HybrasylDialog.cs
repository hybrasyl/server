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

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
public class HybrasylDialog
{
    internal Dialog Dialog { get; set; }
    internal DialogSequence Sequence { get; set; }
    internal System.Type DialogType => Dialog.GetType();

    public HybrasylDialog(Dialog dialog)
    {
        Dialog = dialog;
    }

    /// <summary>
    /// Set the display sprite for this specific dialog to an NPC / creature sprite. This is the sprite that is displayed on the left hand side when a user views a dialog.
    /// </summary>
    /// <param name="displaySprite">int representing the sprite in the datfiles</param>
    public void SetNpcDisplaySprite(int displaySprite)
    {
        Dialog.Sprite = (ushort)(0x4000 + displaySprite);
    }
    /// <summary>
    /// Set the display sprite for this specific dialog to an item sprite. This is the sprite that is displayed on the left hand side when a user views a dialog.
    /// </summary>
    /// <param name="displaySprite">int representing the item sprite in the datfiles</param>

    public void SetItemDisplaySprite(int displaySprite)
    {
        Dialog.Sprite = (ushort)(0x8000 + displaySprite);
    }

    /// <summary>
    /// Assoiciate this particular dialog with a sequence.
    /// </summary>
    /// <param name="sequence"></param>
    public void AssociateDialogWithSequence(DialogSequence sequence)
    {
        if (sequence is null)
        {
            GameLog.ScriptingError("AssociateDialogWithSequence: sequence (first argument) cannot be null");
            return;
        }
        Sequence = sequence;
        sequence.AddDialog(Dialog);
    }

    /// <summary>
    /// Attach a callback expression to this dialog, which will be invoked when the dialog is displayed.
    /// </summary>
    /// <param name="luaExpr">A Lua expression to be evaluated.</param>
    public void AttachCallback(string luaExpr)
    {
        if (string.IsNullOrEmpty(luaExpr))
        {
            GameLog.ScriptingWarning("AttachCallback: lua expression (first argument) was null or empty, ignoring");
            return;
        }
        Dialog.CallbackExpression = luaExpr;
    }



}