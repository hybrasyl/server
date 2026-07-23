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
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Subsystems.Players.Guilds;

[Persistable]
public class Guild : IStateStorable
{
    // One lock for mutation and serialization: Save snapshots consistent state, and
    // mutators can never interleave with an in-flight serialization
    private readonly object _lock = new();

    public bool IsSaving;

    public Guild() { }

    public Guild(string name, Guid leader, List<Guid> founders)
    {
        Name = name;
        Guid = Guid.NewGuid();
        Ranks = new List<GuildRank>
        {
            //default ranks, with guid as key so naming can be changed by user
            new() { Guid = Guid.NewGuid(), Name = "Guild Leader", Level = 0 },
            new() { Guid = Guid.NewGuid(), Name = "Council", Level = 1 },
            new() { Guid = Guid.NewGuid(), Name = "Founder", Level = 2 },
            new() { Guid = Guid.NewGuid(), Name = "Member", Level = 3 },
            new() { Guid = Guid.NewGuid(), Name = "Initiate", Level = 4 }
        };
        GameLog.Info("Guild {Guild}: Added default ranks", name);
        GameLog.Info("Guild {Guild}: Created guild board", name);

        var leaderGuid = Ranks.First(predicate: x => x.Level == 0).Guid;
        var founderGuid = Ranks.First(predicate: x => x.Level == 2).Guid;

        var leaderName = Game.World.WorldState.GetNameByGuid(leader);
        Members.Add(leader, new GuildMember { Name = leaderName, RankGuid = leaderGuid });
        GameLog.Info("Guild {Guild}: Adding leader {Leader}", name, leaderName);
        foreach (var founder in founders)
        {
            var founderName = Game.World.WorldState.GetNameByGuid(founder);
            Members.Add(founder, new GuildMember { Name = founderName, RankGuid = founderGuid });
            GameLog.Info("Guild {Guild}: Adding founder {Founder}", name, founderName);
        }
    }

    [Persist] public Guid Guid { get; set; }

    [Persist] public string Name { get; set; } = string.Empty;

    [Persist] public List<GuildRank> Ranks { get; set; } = new();

    public Board Board => Game.World.WorldState.GetBoard(Name);
    public GuildVault Vault => Game.World.WorldState.GetOrCreateByGuid<GuildVault>(Guid, Name);

    [Persist] public Dictionary<Guid, GuildMember> Members { get; set; } = new();

    public GuildRank LeaderRank => Ranks.Single(predicate: x => x.Level == 0);

    public string StorageKey => $"{GetType()}:{Guid}";

    public void AddMember(User user)
    {
        lock (_lock)
        {
            if (user.GuildGuid != Guid.Empty)
            {
                GameLog.Info("Guild {Guild}: Attempt to add {User} to guild, but user is already in another guild.",
                    Name, user.Name);
                return;
            }

            var lowestRank = Ranks.Aggregate(func: (r1, r2) => r1.Level > r2.Level ? r1 : r2);
            GameLog.Info("Guild {Guild}: Lowest guild rank identified as {Rank}", Name, lowestRank.Name);
            Members.Add(user.Guid, new GuildMember { Name = user.Name, RankGuid = lowestRank.Guid });
            user.GuildGuid = Guid;
            GameLog.Info("Guild {Guild}: Adding new member {User} to rank {Rank}", Name, user.Name, lowestRank.Name);
        }
    }

    public void RemoveMember(User user)
    {
        lock (_lock)
        {
            var (guid, membership) = Members.Single(predicate: x => x.Value.Name == user.Name);
            if (membership.RankGuid == LeaderRank.Guid)
            {
                GameLog.Info("Guild {Guild}: Sorry, the guild leader can't be removed.", Name);
                return;
            }

            Members.Remove(guid);
            user.GuildGuid = Guid.Empty;
            GameLog.Info("Guild {Guild}: Removing member {User}", Name, user.Name);
        }
    }

    public void PromoteMember(string name)
    {
        lock (_lock)
        {
            var (guid, membership) = Members.Single(predicate: x => x.Value.Name == name);
            var currentRank = Ranks.FirstOrDefault(predicate: x => x.Guid == membership.RankGuid);
            if (currentRank == null)
            {
                GameLog.Error("Guild {Guild}: member {Member} has dangling rank {Rank}, cannot promote", Name,
                    membership.Name, membership.RankGuid);
                return;
            }

            var newRank = Ranks.FirstOrDefault(predicate: x => x.Level == currentRank.Level - 1);

            if (newRank == null || newRank.Level <= 0) return;
            membership.RankGuid = newRank.Guid;
            GameLog.Info("Guild {Guild}: Promoting {Member} to rank {Rank}", Name, membership.Name, newRank.Name);
        }
    }

    public void DemoteMember(string name)
    {
        lock (_lock)
        {
            var member = Members.Single(predicate: x => x.Value.Name == name);
            var currentRank = Ranks.FirstOrDefault(predicate: x => x.Guid == member.Value.RankGuid);
            if (currentRank == null)
            {
                GameLog.Error("Guild {Guild}: member {Member} has dangling rank {Rank}, cannot demote", Name,
                    member.Value.Name, member.Value.RankGuid);
                return;
            }

            var newRank = Ranks.FirstOrDefault(predicate: x => x.Level == currentRank.Level + 1);

            if (newRank != null && newRank.Level > currentRank.Level)
            {
                if (currentRank.Level == 0)
                {
                    GameLog.Info("Guild {Guild}: Sorry, the guild leader cannot be demoted.", Name);
                    return;
                }

                member.Value.RankGuid = newRank.Guid;
                GameLog.Info("Guild {Guild}: Demoting {Member} to rank {Rank}", Name, member.Value.Name, newRank.Name);
            }
        }
    }

    public void ChangeRankTitle(string oldTitle, string newTitle)
    {
        lock (_lock)
        {
            var rank = Ranks.FirstOrDefault(predicate: x => x.Name == oldTitle);

            if (rank != null)
            {
                rank.Name = newTitle;
                GameLog.Info("Guild {Guild}: Renaming rank {OldTitle} to rank {NewTitle}", Name, oldTitle, newTitle);
            }
        }
    }

    public void AddRank(string title) //adds a new rank at the lowest tier
    {
        lock (_lock)
        {
            if (Ranks.Any(predicate: x => x.Name == title)) return;

            var lowestRank = Ranks.Aggregate(func: (r1, r2) => r1.Level > r2.Level ? r1 : r2);

            var rank = new GuildRank { Guid = Guid.NewGuid(), Name = title, Level = lowestRank.Level + 1 };

            Ranks.Add(rank);
            GameLog.Info("Guild {Guild}: New rank {Rank} added as level {Level}", Name, rank.Name, rank.Level);
        }
    }

    public void RemoveRank() //only remove the lowest tier rank and move all members in rank up one level.
    {
        lock (_lock)
        {
            var lowestRank = Ranks.Aggregate(func: (r1, r2) => r1.Level > r2.Level ? r1 : r2);
            var nextRank = Ranks.FirstOrDefault(predicate: x => x.Level == lowestRank.Level - 1);

            if (nextRank != null && nextRank.Level != 0)
            {
                var moveMembers = Members.Where(predicate: x => x.Value.RankGuid == lowestRank.Guid).ToList();

                foreach (var member in moveMembers)
                {
                    member.Value.RankGuid = nextRank.Guid;
                    GameLog.Info(
                        "Guild {Guild}: Member {Member} moved to rank {Rank} due to rank deletion", Name,
                        member.Value.Name, nextRank.Name);
                }

                //remove lowest rank here to avoid missing members
                Ranks.Remove(lowestRank);
                GameLog.Info("Guild {Guild}: Deleted rank {Rank}", Name, lowestRank.Name);
            }
        }
    }

    public (string GuildName, string Rank) GetUserDetails(Guid guid)
    {
        var member = Members.FirstOrDefault(predicate: x => x.Key == guid);

        var guildName = Name;
        // member.Value is null when the guid isn't a member; RankGuid can dangle after rank edits
        var rank = member.Value is null ? null : Ranks.FirstOrDefault(predicate: x => x.Guid == member.Value.RankGuid);
        if (rank is null)
            GameLog.Error("Guild {Guild}: user {Guid} has no resolvable rank", Name, guid);

        return (guildName, rank?.Name ?? "Unknown");
    }


    public void Save()
    {
        lock (_lock)
        {
            // Monitors are reentrant: a same-thread Save triggered during serialization
            // re-enters the lock and is stopped here, while cross-thread savers queue
            if (IsSaving) return;
            IsSaving = true;
            try
            {
                var cache = World.DatastoreConnection.GetDatabase();
                cache.Set(StorageKey, this);
            }
            finally
            {
                // A failed save must not permanently disable saving
                IsSaving = false;
            }
        }
    }

    public Dictionary<string, string> GetGuildMembers()
    {
        var ret = new Dictionary<string, string>();
        foreach (var member in Members)
        {
            var rank = Ranks.FirstOrDefault(predicate: x => x.Guid == member.Value.RankGuid);
            if (rank is null)
            {
                GameLog.Error("Guild {Guild}: member {Member} has dangling rank {Rank}", Name, member.Value.Name,
                    member.Value.RankGuid);
                continue;
            }

            ret.Add(member.Value.Name, rank.Name);
        }

        return ret;
    }
}