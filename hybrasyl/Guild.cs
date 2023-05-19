using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Interfaces;
using Hybrasyl.Messaging;
using Hybrasyl.Objects;
using Newtonsoft.Json;

namespace Hybrasyl;

public class GuildMember
{
    public Guid RankGuid { get; set; }
    public string Name { get; set; }
}

public class GuildRank
{
    public Guid Guid { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public class Guild : IStateStorable
{
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
        GameLog.Info($"Guild {name}: Added default ranks");
        GameLog.Info($"Guild {name}: Created guild board");

        var leaderGuid = Ranks.First(predicate: x => x.Level == 0).Guid;
        var founderGuid = Ranks.First(predicate: x => x.Level == 2).Guid;

        var leaderName = Game.World.WorldState.GetNameByGuid(leader);
        Members.Add(leader, new GuildMember { Name = leaderName, RankGuid = leaderGuid });
        GameLog.Info($"Guild {name}: Adding leader {leaderName}");
        foreach (var founder in founders)
        {
            var founderName = Game.World.WorldState.GetNameByGuid(founder);
            Members.Add(founder, new GuildMember { Name = founderName, RankGuid = founderGuid });
            GameLog.Info($"Guild {name}: Adding founder {founderName}");
        }
    }

    [JsonProperty] public Guid Guid { get; set; }

    [JsonProperty] public string Name { get; set; }

    [JsonProperty] public List<GuildRank> Ranks { get; set; }

    public Board Board => Game.World.WorldState.GetBoard(Name);
    public GuildVault Vault => Game.World.WorldState.GetOrCreateByGuid<GuildVault>(Guid, Name);

    [JsonProperty] public Dictionary<Guid, GuildMember> Members { get; set; } = new();

    public GuildRank LeaderRank => Ranks.Single(predicate: x => x.Level == 0);

    public string StorageKey => $"{GetType()}:{Guid}";

    public void AddMember(User user)
    {
        if (user.GuildGuid != Guid.Empty)
        {
            GameLog.Info($"Guild {Name}: Attempt to add {user.Name} to guild, but user is already in another guild.");
            return;
        }

        var lowestRank = Ranks.Aggregate(func: (r1, r2) => r1.Level > r2.Level ? r1 : r2);
        GameLog.Info($"Guild {Name}: Lowest guild rank identified as {lowestRank.Name}");
        Members.Add(user.Guid, new GuildMember { Name = user.Name, RankGuid = lowestRank.Guid });
        user.GuildGuid = Guid;
        GameLog.Info($"Guild {Name}: Adding new member {user.Name} to rank {lowestRank.Name}");
    }

    public void RemoveMember(User user)
    {
        var (guid, membership) = Members.Single(predicate: x => x.Value.Name == user.Name);
        if (membership.RankGuid == LeaderRank.Guid)
        {
            GameLog.Info($"Guild {Name}: Sorry, the guild leader can't be removed.");
            return;
        }

        Members.Remove(guid);
        user.GuildGuid = Guid.Empty;
        GameLog.Info($"Guild {Name}: Removing member {user.Name}");
    }

    public void PromoteMember(string name)
    {
        var (guid, membership) = Members.Single(predicate: x => x.Value.Name == name);
        var currentRank = Ranks.FirstOrDefault(predicate: x => x.Guid == membership.RankGuid);
        var newRank = Ranks.FirstOrDefault(predicate: x => x.Level == currentRank.Level - 1);

        if (newRank == null || newRank.Level <= 0) return;
        membership.RankGuid = newRank.Guid;
        GameLog.Info($"Guild {Name}: Promoting {membership.Name} to rank {newRank.Name}");
    }

    public void DemoteMember(string name)
    {
        var member = Members.Single(predicate: x => x.Value.Name == name);
        var currentRank = Ranks.FirstOrDefault(predicate: x => x.Guid == member.Value.RankGuid);
        var newRank = Ranks.FirstOrDefault(predicate: x => x.Level == currentRank.Level + 1);

        if (newRank != null && newRank.Level > currentRank.Level)
        {
            if (currentRank.Level == 0)
            {
                GameLog.Info($"Guild {Name}: Sorry, the guild leader cannot be demoted.");
                return;
            }

            member.Value.RankGuid = newRank.Guid;
            GameLog.Info($"Guild {Name}: Demoting {member.Value.Name} to rank {newRank.Name}");
        }
    }

    public void ChangeRankTitle(string oldTitle, string newTitle)
    {
        var rank = Ranks.FirstOrDefault(predicate: x => x.Name == oldTitle);

        if (rank != null)
        {
            rank.Name = newTitle;
            GameLog.Info($"Guild {Name}: Renaming rank {oldTitle} to rank {newTitle}");
        }
    }

    public void AddRank(string title) //adds a new rank at the lowest tier
    {
        if (Ranks.Any(predicate: x => x.Name == title)) return;

        var lowestRank = Ranks.Aggregate(func: (r1, r2) => r1.Level > r2.Level ? r1 : r2);

        var rank = new GuildRank { Guid = Guid.NewGuid(), Name = title, Level = lowestRank.Level + 1 };

        Ranks.Add(rank);
        GameLog.Info($"Guild {Name}: New rank {rank.Name} added as level {rank.Level}");
    }

    public void RemoveRank() //only remove the lowest tier rank and move all members in rank up one level.
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
                    $"Guild {Name}: Member {member.Value.Name} moved to rank {nextRank.Name} due to rank deletion");
            }

            //remove lowest rank here to avoid missing members
            Ranks.Remove(lowestRank);
            GameLog.Info($"Guild {Name}: Deleted rank {lowestRank.Name}");
        }
    }

    public (string GuildName, string Rank) GetUserDetails(Guid guid)
    {
        var member = Members.FirstOrDefault(predicate: x => x.Key == guid);

        var guildName = Name;
        var rank = Ranks.FirstOrDefault(predicate: x => x.Guid == member.Value.RankGuid);

        return (guildName, rank.Name);
    }


    public void Save()
    {
        if (IsSaving) return;
        IsSaving = true;
        var cache = World.DatastoreConnection.GetDatabase();
        cache.Set(StorageKey, this);
        IsSaving = false;
    }

    public Dictionary<string, string> GetGuildMembers()
    {
        var ret = new Dictionary<string, string>();
        foreach (var member in Members)
        {
            var rank = Ranks.FirstOrDefault(predicate: x => x.Guid == member.Value.RankGuid).Name;
            ret.Add(member.Value.Name, rank);
        }

        return ret;
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class GuildCharter
{
    public GuildCharter() { }

    public GuildCharter(string guildName)
    {
        GuildName = guildName;
    }

    [JsonProperty] public Guid Guid { get; set; } = Guid.NewGuid();

    [JsonProperty] public string GuildName { get; set; }

    [JsonProperty] public Guid LeaderGuid { get; set; }

    [JsonProperty] public List<Guid> Supporters { get; set; } = new();

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