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
using Hybrasyl.Subsystems.Messaging;
// Both namespaces define BoardResponseType; alias each so the mapping below stays explicit about
// which side it means (they are NOT the same values — see Packet()).
using BoardResponseType = Hybrasyl.Subsystems.Messaging.BoardResponseType;
using DalibBoardResponsePacket = DALib.Networking.Packets.Server.BoardResponsePacket;
using DalibBoardResponseType = DALib.Networking.Packets.Server.BoardResponseType;

namespace Hybrasyl.Networking.ServerPackets;

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
    public int TransmitDelay => ResponseType == BoardResponseType.DisplayList ? 600 : 0;

    /// <summary>
    ///     Builds the 0x31 response. Note that <see cref="BoardResponseType" />'s values are NOT the
    ///     wire type bytes for the index/message forms — the legacy builder wrote literals that differ
    ///     from the enum (e.g. GetBoardIndex=3 emitted wire type 2). The wire types below are the
    ///     retail ones (rung-1: darkages-741 049-0x31): 1 board list, 2 board index, 3 board post,
    ///     4 mailbox index, 5 mail post, 6/7/8 result popups.
    /// </summary>
    public DalibBoardResponsePacket Packet()
    {
        switch (ResponseType)
        {
            case BoardResponseType.EndResult:
            case BoardResponseType.DeleteMessage:
            case BoardResponseType.HighlightMessage:
                return new BoardResultPacket
                {
                    // These three enum values (6/7/8) DO match the wire.
                    ResponseType = (DalibBoardResponseType) (byte) ResponseType,
                    Success = ResponseSuccess,
                    Message = ResponseString
                };

            case BoardResponseType.GetMailboxIndex:
            case BoardResponseType.GetBoardIndex:
                return new BoardIndexPacket
                {
                    ResponseType = ResponseType == BoardResponseType.GetMailboxIndex
                        ? DalibBoardResponseType.PrivateBoard // wire 4
                        : DalibBoardResponseType.PublicBoard, // wire 2
                    // Mail is always 0x01; a board clicked in-world uses 0x02.
                    RefreshFlag = ResponseType == BoardResponseType.GetMailboxIndex
                        ? (byte) 0x01
                        : (byte) (isClick ? 0x02 : 0x01),
                    BoardId = BoardId,
                    BoardName = BoardName,
                    Messages = Messages.Select(selector: m => new BoardMessageHeader(
                            m.Highlight, (ushort) m.Id, m.Sender, m.Month, m.Day, m.Subject))
                        .ToList()
                };

            case BoardResponseType.DisplayList:
                // The legacy emit wrote [u16 count+1][u16 0][string8 "Mail"], which the
                // client parsed as [string8 heading][u8 count] — the u16's high byte doubling as an
                // empty heading length. Same bytes below (empty heading, "Mail" as entry 0), now in
                // the real layout, which also removes the desync past 255 boards.
                return new BoardListPacket
                {
                    ResponseType = DalibBoardResponseType.BoardList,
                    Name = string.Empty,
                    Boards = Boards.Select(selector: b => new BoardListEntry(b.Id, b.Name))
                        .Prepend(new BoardListEntry(0, "Mail"))
                        .ToList()
                };

            case BoardResponseType.GetBoardMessage:
            case BoardResponseType.GetMailMessage:
                var message = Messages[0];
                return new BoardPostPacket
                {
                    ResponseType = ResponseType == BoardResponseType.GetMailMessage
                        ? DalibBoardResponseType.PrivatePost // wire 5
                        : DalibBoardResponseType.PublicPost, // wire 3
                    // 0x03 keeps the client "Prev" button live; 0x00 disables backward paging.
                    RefreshFlag = 0x03,
                    // Mailbox messages are always "read".
                    Highlight = ResponseType == BoardResponseType.GetMailMessage || message.Highlight,
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