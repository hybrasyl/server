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
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System.Collections.Generic;
using System.Reflection;
using Hybrasyl.Networking;
using Hybrasyl.Networking.ServerPackets;
using Hybrasyl.Subsystems.Messaging;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Messaging
{
    // The board post response payload is: [type marker][paging flag][highlight]...
    // The client reads the paging flag byte (index 1); 0x00 disables the "Prev"
    // (newer) button, so backward paging of board posts is impossible. Mail sends
    // 0x03 there and paging works; the board branch must send the same.
    private static byte[] PayloadOf(BoardResponseType responseType)
    {
        var response = new MessagingResponse
        {
            ResponseType = responseType,
            Messages = new List<MessageInfo>
            {
                new()
                {
                    Id = 1, Sender = "sender", Month = 1, Day = 1,
                    Subject = "subject", Body = "body", Highlight = false
                }
            }
        };
        var packet = response.Packet();
        return (byte[]) typeof(Packet)
            .GetField("Data", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(packet)!;
    }

    [Fact]
    public void BoardPostResponseEnablesPrevPaging()
    {
        // 0x03 keeps the client's "Prev" button live so board posts can page backward.
        Assert.Equal(0x03, PayloadOf(BoardResponseType.GetBoardMessage)[1]);
    }

    [Fact]
    public void MailMessageResponseEnablesPrevPaging()
    {
        // Mail already pages correctly; the board response must match this flag.
        Assert.Equal(0x03, PayloadOf(BoardResponseType.GetMailMessage)[1]);
    }
}
