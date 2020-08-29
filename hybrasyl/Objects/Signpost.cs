﻿/*
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

namespace Hybrasyl.Objects
{
    public class Signpost : VisibleObject
    {
        public string Message { get; set; }
        public bool IsMessageboard { get; set; }
        public string BoardName { get; set; }
        public Board Board { get; private set; }

        public Signpost(byte postX, byte postY, string message, bool messageboard = false,
            string boardname = null)
            : base()
        {
            X = postX;
            Y = postY;
            Message = message;
            IsMessageboard = messageboard;
            BoardName = boardname;
            Board = null;
            if (IsMessageboard && !string.IsNullOrEmpty(boardname))
                Board = Game.World.GetBoard(BoardName);
        }

        public override void OnClick(User invoker)
        {
            GameLog.DebugFormat("Signpost was clicked");
            if (!IsMessageboard)
                invoker.SendMessage(Message, Message.Length < 1024 ? (byte)MessageTypes.SLATE : (byte)MessageTypes.SLATE_WITH_SCROLLBAR);
            else
                invoker.Enqueue(Board.RenderToPacket(true));
        }
    }

}
