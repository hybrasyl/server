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

using System;
using System.Linq;
using Hybrasyl.Subsystems.Messaging;
using Xunit;

namespace Hybrasyl.Tests;

/// <summary>
///     Pins the board/mail index contract: the client expects the newest messages
///     first, and once a store grows past the response cap the index must surface the
///     newest messages, never silently drop them in favor of the oldest.
/// </summary>
[Collection("Hybrasyl")]
public class MessageStoreIndex
{
    private readonly HybrasylFixture Fixture;

    public MessageStoreIndex(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private static Mailbox FilledMailbox(int count)
    {
        var mailbox = new Mailbox(Guid.NewGuid());
        for (var i = 1; i <= count; i++)
            // Subject and Id both encode receipt order: message i has Id i.
            Assert.True(mailbox.ReceiveMessage(new Message("recipient", "sender", $"subject-{i}", "body")));
        return mailbox;
    }

    [Fact]
    public void IndexIsNewestFirst()
    {
        var mailbox = FilledMailbox(3);

        var index = mailbox.GetIndex();

        Assert.Equal(3, index.Count);
        // Newest (last received) must lead the index; oldest trails it.
        Assert.Equal("subject-3", index[0].Subject);
        Assert.Equal(3, index[0].Id);
        Assert.Equal("subject-1", index[^1].Subject);
    }

    [Fact]
    public void IndexKeepsNewestWhenOverCap()
    {
        var cap = Game.ActiveConfiguration.Constants.BoardMessageResponseSize;
        var total = cap + 5;
        var mailbox = FilledMailbox(total);

        var index = mailbox.GetIndex();
        var subjects = index.Select(selector: m => m.Subject).ToHashSet();

        Assert.Equal(cap, index.Count);
        // The newest message leads and is present...
        Assert.Equal($"subject-{total}", index[0].Subject);
        Assert.Equal(total, index[0].Id);
        Assert.Contains($"subject-{total}", subjects);
        // ...and the oldest messages beyond the cap are the ones dropped.
        Assert.DoesNotContain("subject-1", subjects);
        Assert.Contains($"subject-{total - cap + 1}", subjects); // oldest surviving message
    }
}
