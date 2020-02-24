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

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]
    public class HybrasylDialogSequence
    {
        internal DialogSequence Sequence { get; private set; }

	public HybrasylDialogSequence(string sequenceName, bool closeOnEnd = false) =>
            Sequence = new DialogSequence(sequenceName, closeOnEnd);

        internal HybrasylDialogSequence(DialogSequence sequence) =>
            Sequence = sequence;

        public void AddDialog(HybrasylDialog scriptDialog) =>
            scriptDialog.AssociateDialogWithSequence(Sequence);
        
        public void AddDisplayCallback(string check) =>
            Sequence.AddPreDisplayCallback(check);

        public void AddMenuCheckExpression(string check) =>
            Sequence.AddMenuCheckExpression(check);

        public void SetNpcDisplaySprite(int displaySprite) =>
            Sequence.Sprite = (ushort)(0x4000 + displaySprite);

        public void SetItemDisplaySprite(int displaySprite) =>
            Sequence.Sprite = (ushort)(0x8000 + displaySprite);

        public void SetDisplayName(string displayName) =>
            Sequence.DisplayName = displayName;


        public void AssociateWithScript(string scriptName)
        {
            // Clear any existing script; the next access to Script will 
            // result in the object being evaluated from the passed name.
            // This handles several edge cases of a running script trying to
            // register or associate dialogs to itself.
            Sequence.Script = null;
            Sequence.ScriptName = scriptName;
        }
    }
}
