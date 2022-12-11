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
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using System;
using Newtonsoft.Json;

namespace Hybrasyl.Messaging;

[JsonObject(MemberSerialization.OptIn)]
[RedisType]
public class SentMail : MessageStore
{
    // TODO: correct
    public SentMail(Guid guid) : base(guid.ToString()) { }

    [JsonProperty] public DateTime LastMailMessageSent { get; set; }

    [JsonProperty] public string LastMailRecipient { get; set; }

    [JsonProperty] public DateTime LastBoardMessageSent { get; set; }

    [JsonProperty] public string LastBoardRecipient { get; set; }

    public bool HasUnreadMessages => false;

    public override bool ReceiveMessage(Message newMessage)
    {
        if (IsLocked || Full) return false;
        CurrentId++;
        newMessage.Id = CurrentId;
        newMessage.Body =
            $"{{=e(( Originally Sent: {newMessage.Created} ))\n{{=e(( Sent To: {newMessage.Recipient} ))\n\n{{=a{newMessage.Body}";
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