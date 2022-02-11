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
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl.Messaging;

[JsonObject(MemberSerialization.OptIn)]
public class Board : MessageStore
{
    [JsonProperty] public bool Global;
    [JsonProperty] public HashSet<string> ModeratorList { get; private set; }
    [JsonProperty] public HashSet<string> ReaderList { get; private set; }
    [JsonProperty] public HashSet<string> WriterList { get; private set; }
    [JsonProperty] public HashSet<string> BlockList { get; private set; }

    private void InitializeStorage()
    {
        ModeratorList = new HashSet<string>();
        ReaderList = new HashSet<string>();
        WriterList = new HashSet<string>();
        BlockList = new HashSet<string>();
    }

    public static string GetStorageKey(string name) => $"{typeof(Board).FullName}:{name.ToLower()}";

    public Board(string name) : base(name)
    {
        Global = false;
        InitializeStorage();
    }

    public override bool ReceiveMessage(Message newMessage)
    {
        // Ensure board messages are not received highlighted or unread
        newMessage.Read = true;
        newMessage.Highlighted = false;
        if (CheckAccessLevel(newMessage.Sender, BoardAccessLevel.Write))
        {
            return base.ReceiveMessage(newMessage);
        }
        return false;
    }

    public bool CheckAccessLevel(string charName, BoardAccessLevel level)
    {
        var checkname = charName.ToLower();

        if (ModeratorList.Contains(checkname) || ModeratorList.Contains("*"))
            return true;

        if (BlockList.Contains(checkname))
            return false;

        switch (level)
        {
            case BoardAccessLevel.Read:
                return ReaderList.Count == 0 || ReaderList.Contains(checkname) || WriterList.Contains(checkname) || ReaderList.Contains("*");
            case BoardAccessLevel.Write:
                return WriterList.Count == 0 || WriterList.Contains(checkname) || WriterList.Contains("*");
            case BoardAccessLevel.Moderate:
                return ModeratorList.Contains(checkname);
        }
        return false;
    }

    public void SetAccessLevel(string charName, BoardAccessLevel level)
    {
        if (level == BoardAccessLevel.Read)
            ReaderList.Add(charName.ToLower());
        if (level == BoardAccessLevel.Moderate)
            ModeratorList.Add(charName.ToLower());
        if (level == BoardAccessLevel.Write)
            WriterList.Add(charName.ToLower());
    }

    public bool ToggleHighlight(short id)
    {
        if (id > Messages.Count)
            return false;
        Messages[id].Highlighted = !Messages[id].Highlighted;
        return true;
    }
}