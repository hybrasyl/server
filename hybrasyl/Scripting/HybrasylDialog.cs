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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015-2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Dialogs;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{

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

        public void SetNpcDisplaySprite(int displaySprite)
        {
            Dialog.Sprite = (ushort)(0x4000 + displaySprite);
        }

        public void SetItemDisplaySprite(int displaySprite)
        {
            Dialog.Sprite = (ushort)(0x8000 + displaySprite);
        }

        public void AssociateDialogWithSequence(DialogSequence sequence)
        {
            Sequence = sequence;
            sequence.AddDialog(Dialog);
        }

        public void AttachCallback(string luaExpr)
        {
            Dialog.CallbackExpression = luaExpr;
        }



    }
}
