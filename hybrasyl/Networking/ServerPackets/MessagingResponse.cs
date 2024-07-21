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

using System.Collections.Generic;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Subsystems.Messaging;

namespace Hybrasyl.Networking.ServerPackets;

internal class MessagingResponse
{
    private readonly byte OpCode;

    public MessagingResponse()
    {
        OpCode = OpCodes.Board;
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
    public string ResponseString { get; set; }
    public bool ResponseSuccess { get; set; }

    public ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);

        if (ResponseType == BoardResponseType.EndResult ||
            ResponseType == BoardResponseType.DeleteMessage ||
            ResponseType == BoardResponseType.HighlightMessage)
        {
            packet.WriteByte((byte) ResponseType);
            packet.WriteBoolean(ResponseSuccess);
            packet.WriteString8(ResponseString);
        }
        else if (ResponseType == BoardResponseType.GetMailboxIndex ||
                 ResponseType == BoardResponseType.GetBoardIndex)
        {
            if (ResponseType == BoardResponseType.GetMailboxIndex)
            {
                packet.WriteByte(0x04); // 0x02 - public, 0x04 - mail
                packet.WriteByte(0x01); // ??? - needs to be odd number unless board in world has been clicked
            }
            else
            {
                packet.WriteByte(0x02);
                packet.WriteByte((byte) (isClick ? 0x02 : 0x01));
            }

            packet.WriteUInt16(BoardId);
            packet.WriteString8(BoardName);
            packet.WriteByte((byte) Messages.Count);
            foreach (var message in Messages)
            {
                packet.WriteBoolean(message.Highlight);
                packet.WriteInt16(message.Id);
                packet.WriteString8(message.Sender);
                packet.WriteByte(message.Month);
                packet.WriteByte(message.Day);
                packet.WriteString8(message.Subject);
            }
        }

        else if (ResponseType == BoardResponseType.DisplayList)
        {
            packet.WriteByte(0x01);
            packet.WriteUInt16((ushort) (Boards.Count + 1));
            packet.WriteUInt16(0);
            packet.WriteString8("Mail");
            foreach (var (Id, Name) in Boards)
            {
                packet.WriteUInt16(Id);
                packet.WriteString8(Name);
            }

            // This is required to correctly display the messaging pane
            packet.TransmitDelay = 600;
        }

        else if (ResponseType == BoardResponseType.GetBoardMessage ||
                 ResponseType == BoardResponseType.GetMailMessage)
        {
            // Functionality unknown but necessary
            var message = Messages[0];
            if (ResponseType == BoardResponseType.GetMailMessage)
            {
                packet.WriteByte(0x05);
                packet.WriteByte(0x03);
                packet.WriteBoolean(true); // Mailbox messages are always "read"
            }
            else
            {
                packet.WriteByte(0x03);
                packet.WriteByte(0x00);
                packet.WriteBoolean(message.Highlight);
            }

            packet.WriteUInt16((ushort) message.Id);
            packet.WriteString8(message.Sender);
            packet.WriteByte(message.Month);
            packet.WriteByte(message.Day);
            packet.WriteString8(message.Subject);
            packet.WriteString16(message.Body);
        }

        return packet;
    }
}