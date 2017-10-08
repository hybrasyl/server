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

namespace Hybrasyl.Scripting
{

    public class HybrasylDialogSequence
    {
        internal DialogSequence Sequence { get; private set; }
        

        public HybrasylDialogSequence(string sequenceName, bool closeOnEnd = false)
        {
            Sequence = new DialogSequence(sequenceName, closeOnEnd);
        }

        public void AddDialog(HybrasylDialog scriptDialog)
        {
            scriptDialog.AssociateDialogWithSequence(Sequence);
        }

        public void AddCheck(dynamic check)
        {
            Sequence.AddPreDisplayCallback(check);
        }
    }
}
