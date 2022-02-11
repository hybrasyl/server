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

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hybrasyl.Messaging;

public class MessageStoreLocked : Exception
{

}

[JsonObject(MemberSerialization.OptIn)]
public class MessageStore : IEnumerable<Message>
{
    [JsonProperty] public string Name;
    [JsonProperty] public string DisplayName;
    [JsonProperty] public List<Message> Messages;
    [JsonProperty] public Guid Guid;
    [JsonProperty] public short CurrentId;
    public int Id;

    public bool Full => Messages.Count == short.MaxValue;
    public bool IsSaving;
    public bool IsLocked;
    private readonly object Lock;

    public MessageStore(string name, string displayName = "")
    {
        Name = name;
        IsSaving = false;
        Guid = Guid.NewGuid();
        CurrentId = 0;
        Lock = new object();
        Messages = new List<Message>();
        DisplayName = displayName != "" ? displayName : Name;
    }

    public string StorageKey => string.Concat(GetType(), ':', Name.ToLower());

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
        if (IsLocked || Full == true)
        {
            return false;
        }
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

    public IEnumerator<Message> GetEnumerator()
    {
        return Messages.Take(Constants.MESSAGE_RETURN_SIZE).Where(message => !message.Deleted).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public Message GetMessage(int id)
    {
        if (id > Messages.Count)
            return null;
        else return Messages[id];
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
                Messages.RemoveAll(m => m.Deleted == true);
                // If we still are within 250 messages of the maximum, start deleting older messages
                if (Messages.Count > short.MaxValue - 250)
                {
                    // Delete 1000 of the oldest messages
                    Messages.RemoveRange(0, 1000);
                }
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
        foreach (var message in this.Take(Constants.MESSAGE_RETURN_SIZE))
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