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

using DALib.Networking.Packets.Server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Subsystems.Messaging;

internal class MessagingResponse
{
    public MessagingResponse()
    {
        Boards = new List<(ushort Id, string Name)>();
        Messages = new List<MessageInfo>();
        BoardId = 0;
        BoardName = "Mail";
    }

    public BoardResponseType ResponseType { get; set; }
    public List<(ushort Id, string Name)> Boards { get; set; }
    public List<MessageInfo> Messages { get; set; }
    public bool isClick { get; set; }
    public ushort BoardId { get; set; }
    public string BoardName { get; set; }
    public string ResponseString { get; set; } = string.Empty;
    public bool ResponseSuccess { get; set; }

    /// <summary>
    ///     Delay applied when this response is enqueued. The board list needs one to display the
    ///     messaging pane correctly. Computed, not set during <see cref="Packet" />, so callers may
    ///     read it in any order.
    /// </summary>
    public int TransmitDelay => ResponseType == BoardResponseType.BoardList ? 600 : 0;

    public BoardResponsePacket Packet()
    {
        switch (ResponseType)
        {
            case BoardResponseType.EndResult:
            case BoardResponseType.DeleteResult:
            case BoardResponseType.HighlightResult:
                return new BoardResultPacket
                {
                    ResponseType = ResponseType,
                    Success = ResponseSuccess,
                    Message = ResponseString
                };

            case BoardResponseType.PrivateBoard:
            case BoardResponseType.PublicBoard:
                return new BoardIndexPacket
                {
                    ResponseType = ResponseType,
                    // Mail is always 0x01; a board clicked in-world uses 0x02.
                    RefreshFlag = ResponseType == BoardResponseType.PrivateBoard
                        ? (byte) 0x01
                        : (byte) (isClick ? 0x02 : 0x01),
                    BoardId = BoardId,
                    BoardName = BoardName,
                    Messages = Messages.Select(selector: m => new BoardMessageHeader(
                            m.Highlight, (ushort) m.Id, m.Sender, m.Month, m.Day, m.Subject))
                        .ToList()
                };

            case BoardResponseType.BoardList:
                // The layout is [string8 heading][u8 count]{entries}: the heading is empty and
                // "Mail" is entry 0, which is what retail sends.
                return new BoardListPacket
                {
                    ResponseType = ResponseType,
                    Name = string.Empty,
                    Boards = Boards.Select(selector: b => new BoardListEntry(b.Id, b.Name))
                        .Prepend(new BoardListEntry(0, "Mail"))
                        .ToList()
                };

            case BoardResponseType.PublicPost:
            case BoardResponseType.PrivatePost:
                var message = Messages[0];
                return new BoardPostPacket
                {
                    ResponseType = ResponseType,
                    // 0x03 keeps the client "Prev" button live; 0x00 disables backward paging.
                    RefreshFlag = 0x03,
                    // Mailbox messages are always "read".
                    Highlight = ResponseType == BoardResponseType.PrivatePost || message.Highlight,
                    PostId = (ushort) message.Id,
                    Author = message.Sender,
                    Month = message.Month,
                    Day = message.Day,
                    Subject = message.Subject,
                    Body = message.Body
                };

            default:
                throw new InvalidOperationException(
                    $"MessagingResponse: unhandled response type {ResponseType}.");
        }
    }
}