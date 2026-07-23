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

using System;
using System.Globalization;
using Hybrasyl.Internals.Attributes;

namespace Hybrasyl.Subsystems.Messaging;

[Persistable]
[RedisType]
public class SentMail : MessageStore
{
    private SentMail() { }

    // TODO: correct
    public SentMail(Guid ownerGuid) : base(ownerGuid.ToString()) { }

    [Persist] public DateTime LastMailMessageSent { get; set; }

    // Persisted (STJ): initialize non-null; a stored record overwrites, an absent field keeps the default.
    [Persist] public string LastMailRecipient { get; set; } = string.Empty;

    [Persist] public DateTime LastBoardMessageSent { get; set; }

    [Persist] public string LastBoardRecipient { get; set; } = string.Empty;

    public bool HasUnreadMessages => false;

    public override bool ReceiveMessage(Message newMessage)
    {
        if (IsLocked || Full) return false;
        CurrentId++;
        newMessage.Id = CurrentId;
        // Fixed format: mail bodies are persisted, so they must not vary with server locale
        newMessage.Body =
            $"{{=e(( Originally Sent: {newMessage.Created.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} ))\n{{=e(( Sent To: {newMessage.Recipient} ))\n\n{{=a{newMessage.Body}";
        if (newMessage.Body.Length > ushort.MaxValue)
            newMessage.Body = newMessage.Body.Substring(0, ushort.MaxValue);
        // Sent mail is always read
        newMessage.Read = true;
        newMessage.ReadTime = DateTime.Now;
        Messages.Add(newMessage);
        Save();
        return true;
    }
}