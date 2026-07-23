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

using Hybrasyl.Extensions;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Attributes;
using Hybrasyl.Servers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Subsystems.Messaging;

public class MessageStoreLocked : Exception { }

[Persistable]
public class MessageStore : IEnumerable<Message>, IStateStorable
{
    private readonly object Lock = new();
    [Persist] public short CurrentId;
    [Persist] public string DisplayName;
    [Persist] public Guid Guid;
    public int Id;
    public bool IsLocked;
    public bool IsSaving;
    [Persist] public List<Message> Messages = new();
    [Persist] public string Name;

    // Deserialization only; serialized state overwrites the members
    protected MessageStore() { }

    public MessageStore(string name, string displayName = "")
    {
        Name = name;
        IsSaving = false;
        Guid = Guid.NewGuid();
        CurrentId = 0;
        DisplayName = displayName != "" ? displayName : Name;
    }

    public bool Full => Messages.Count == short.MaxValue;

    public string StorageKey => string.Concat(GetType(), ':', Name.ToLower());

    public IEnumerator<Message> GetEnumerator()
    {
        // Newest-first, capped at the client's response size. Order before Take so a
        // store larger than the cap surfaces the NEWEST messages, not the oldest.
        // Messages is insertion order (oldest first); Enumerable.Reverse (not the
        // in-place List.Reverse) flips it without mutating the persisted list.
        return ((IEnumerable<Message>) Messages).Reverse()
            .Where(predicate: message => !message.Deleted)
            .Take(Game.ActiveConfiguration.Constants.BoardMessageResponseSize)
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Save()
    {
        if (IsSaving) return;
        lock (Lock)
        {
            IsSaving = true;
            var cache = World.DatastoreConnection.GetDatabase();
            cache.Set(StorageKey, this);
            IsSaving = false;
        }
    }

    public virtual bool ReceiveMessage(Message newMessage)
    {
        if (IsLocked || Full) return false;
        lock (Lock)
        {
            CurrentId++;
            newMessage.Id = CurrentId;
            Messages.Add(newMessage);
        }

        return true;
    }

    public virtual bool DeleteMessage(int id)
    {
        if (id > Messages.Count)
            return false;
        lock (Lock)
        {
            Messages[id].Deleted = true;
            return true;
        }
    }

    public Message GetMessage(int id)
    {
        if (id > Messages.Count)
            return null;
        return Messages[id];
    }

    public void Cleanup()
    {
        IsLocked = true;
        lock (Lock)
        {
            // Don't do anything until the store is at ~80% capacity
            if (Messages.Count > short.MaxValue - 6500)
            {
                // Try to remove deleted messages first.
                Messages.RemoveAll(match: m => m.Deleted);
                // If we still are within 250 messages of the maximum, start deleting older messages
                if (Messages.Count > short.MaxValue - 250)
                    // Delete 1000 of the oldest messages
                    Messages.RemoveRange(0, 1000);
                // Renumber mailbox.
                // This sucks, but I'm not sure how to make it better given the client restrictions.
                CurrentId = 0;
                foreach (var message in Messages)
                {
                    message.Id = CurrentId;
                    CurrentId++;
                }
            }
        }

        // Unlock and save.
        IsLocked = false;
        Save();
    }

    public List<MessageInfo> GetIndex()
    {
        var index = new List<MessageInfo>();
        // Enumeration is already newest-first and capped (see GetEnumerator).
        foreach (var message in this)
        {
            var info = message.Info;
            if (this is Board)
                info.Highlight = message.Highlighted;
            else
                info.Highlight = !message.Read;
            index.Add(info);
        }

        return index;
    }
}