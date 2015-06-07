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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 *            
 */

using System;

namespace Hybrasyl.Objects
{
    public class Signpost : VisibleObject
    {
        private signposts Data { get; set; }
        private string Message
        {
            get
            {
                return Data.Message;
            }
        }
        private bool IsMessageboard
        {
            get
            {
                return Convert.ToBoolean(Data.Is_messageboard);
            }
        }

        public Signpost()
            : base()
        {
        }

        public Signpost(signposts post)
        {
            Data = post;
            X = (byte)post.Map_x;
            Y = (byte)post.Map_y;
        }

        public override void OnClick(User invoker)
        {
            Logger.DebugFormat("Signpost was clicked");
            if (!IsMessageboard)
            {
                if (Message.Length < 1024)
                {
                    invoker.SendMessage(Message, Hybrasyl.MessageTypes.SLATE);
                }
                else
                {
                    invoker.SendMessage(Message, Hybrasyl.MessageTypes.SLATE_WITH_SCROLLBAR);
                }
            }
        }
    }
}
