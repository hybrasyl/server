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
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Players.Guilds;

[Persistable]
public class GuildCharter
{
    public GuildCharter() { }

    public GuildCharter(string guildName)
    {
        GuildName = guildName;
    }

    [Persist] public Guid Guid { get; set; } = Guid.NewGuid();

    [Persist] public string GuildName { get; set; }

    [Persist] public Guid LeaderGuid { get; set; }

    [Persist] public List<Guid> Supporters { get; set; } = new();

    public bool AddSupporter(User user)
    {
        Supporters.Add(user.Guid);
        return true;
    }

    public bool CreateGuild()
    {
        if (Supporters.Count == 10)
        {
            var guild = new Guild(GuildName, LeaderGuid, Supporters);
            guild.Save();

            return true;
        }

        return false;
    }
}