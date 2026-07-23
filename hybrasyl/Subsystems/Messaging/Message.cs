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
using System.Collections.Generic;
using Hybrasyl.Internals.Attributes;

namespace Hybrasyl.Subsystems.Messaging;

[Persistable]
public class Message : ICloneable
{
    [Persist] private bool _read;
    [Persist] public string Body;
    [Persist] public DateTime Created;
    [Persist] public bool Deleted;
    [Persist] public string Guid;
    [Persist] public bool Highlighted;
    [Persist] public int Id;

    [Persist] public DateTime ReadTime;
    [Persist] public string Recipient;
    [Persist] public string Sender;
    [Persist] public string Subject;

    private Message() { }

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