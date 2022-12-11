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
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Hybrasyl.Messaging;

[JsonObject(MemberSerialization.OptIn)]
public class Message : ICloneable
{
    [JsonProperty] private bool _read;
    [JsonProperty] public string Body;
    [JsonProperty] public DateTime Created;
    [JsonProperty] public bool Deleted;
    [JsonProperty] public string Guid;
    [JsonProperty] public bool Highlighted;
    [JsonProperty] public int Id;

    [JsonProperty] public DateTime ReadTime;
    [JsonProperty] public string Recipient;
    [JsonProperty] public string Sender;
    [JsonProperty] public string Subject;

    public Message(string recipient, string sender, string subject, string body)
    {
        Created = DateTime.Now;
        Recipient = recipient;
        Sender = sender;
        Subject = subject;
        Body = body;
        Deleted = false;
        Highlighted = false;
        Guid = System.Guid.NewGuid().ToString();
        Read = false;
    }

    public MessageInfo Info => new()
    {
        Body = Body,
        Day = (byte) Created.Day,
        Month = (byte) Created.Month,
        Highlight = Highlighted,
        Id = (short) Id,
        Sender = Sender,
        Subject = Subject
    };

    public List<MessageInfo> InfoAsList => new() { Info };

    public bool Read
    {
        get => _read;
        set
        {
            _read = value;
            if (value)
                ReadTime = DateTime.Now;
        }
    }

    public object Clone()
    {
        var ret = new Message(Recipient, Sender, Subject, Body);
        ret.Deleted = Deleted;
        ret.Highlighted = Highlighted;
        ret.Read = Read;
        ret.Id = 0;
        return ret;
    }
}