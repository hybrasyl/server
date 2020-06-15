using Hybrasyl.Objects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hybrasyl
{
    public class GuildMember
    {
        public string RankUuid { get; set; }
        public string Name { get; set; }
    }

    public class GuildRank
    {
        public string Uuid { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class Guild
    {
        [JsonProperty]
        public string Uuid { get; set; }
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public List<GuildRank> Ranks { get; set; }
        public Board Board => Game.World.GetBoard(Uuid);
        public GuildVault Vault => Game.World.GetGuildVault(Uuid);
        [JsonProperty]
        public Dictionary<string, GuildMember> Members { get; set; }

        public string StorageKey => string.Concat(GetType(), ':', Uuid);
        public bool IsSaving;

        public Guild() {}

        public Guild(string name, User leader, List<User> founders)
        {
            Name = name;
            Uuid = Guid.NewGuid().ToString();
            Ranks = new List<GuildRank>();

            //default ranks, with guid as key so naming can be changed by user
            Ranks.Add(new GuildRank() { Uuid = Guid.NewGuid().ToString(), Name = "Guild Leader", Level = 0 });
            Ranks.Add(new GuildRank() { Uuid = Guid.NewGuid().ToString(), Name = "Council", Level = 1 });
            Ranks.Add(new GuildRank() { Uuid = Guid.NewGuid().ToString(), Name = "Founder", Level = 2 });
            Ranks.Add(new GuildRank() { Uuid = Guid.NewGuid().ToString(), Name = "Member", Level = 3 });
            Ranks.Add(new GuildRank() { Uuid = Guid.NewGuid().ToString(), Name = "Initiate", Level = 4 });
            GameLog.Info($"Guild {name}: Added default ranks");
            GameLog.Info($"Guild {name}: Created guild board");
            Members = new Dictionary<string, GuildMember>();

            var leaderGuid = Ranks.FirstOrDefault(x => x.Name == "Guild Leader").Uuid;
            var founderGuid = Ranks.FirstOrDefault(x => x.Name == "Founder").Uuid;

            Members.Add(leader.Uuid, new GuildMember() { Name = leader.Name, RankUuid = leaderGuid });
            GameLog.Info($"Guild {name}: Adding leader {leader.Name}");
            foreach (var founder in founders)
            {
                Members.Add(founder.Uuid, new GuildMember() { Name = founder.Name, RankUuid = founderGuid });
                GameLog.Info($"Guild {name}: Adding founder {founder.Name}");
            }


        }

        public void AddMember(User user)
        {
            if(user.GuildUuid != null)
            {
                GameLog.Info($"Guild {Name}: Attempt to add {user.Name} to guild, but user is already in another guild.");
                return;
            }

            var lowestRank = Ranks.Aggregate((r1, r2) => r1.Level > r2.Level ? r1 : r2);
            GameLog.Info($"Guild {Name}: Lowest guild rank identified as {lowestRank.Name}");
            Members.Add(user.Uuid, new GuildMember() { Name = user.Name, RankUuid = lowestRank.Uuid });
            user.GuildUuid = Uuid;
            GameLog.Info($"Guild {Name}: Adding new member {user.Name} to rank {lowestRank.Name}");
            
        }

        public void RemoveMember(User user)
        {
            var member = Members.Single(x => x.Value.Name == user.Name);
            if(member.Value.RankUuid == Ranks.Single(x => x.Level == 0).Uuid)
            {
                GameLog.Info($"Guild {Name}: Sorry, the guild leader can't be removed.");
                return;
            }
            Members.Remove(member.Key);
            user.GuildUuid = null;
            GameLog.Info($"Guild {Name}: Removing member {user.Name}");
        }

        public void PromoteMember(string name)
        {
            var member = Members.Single(x => x.Value.Name == name);
            var currentRank = Ranks.FirstOrDefault(x => x.Uuid == member.Value.RankUuid);
            var newRank = Ranks.FirstOrDefault(x => x.Level == currentRank.Level - 1);

            if(newRank != null && newRank.Level > 0) //we can only have one leader
            {
                member.Value.RankUuid = newRank.Uuid;
                GameLog.Info($"Guild {Name}: Promoting {member.Value.Name} to rank {newRank.Name}");
            }
        }

        public void DemoteMember(string name)
        {
            var member = Members.Single(x => x.Value.Name == name);
            var currentRank = Ranks.FirstOrDefault(x => x.Uuid == member.Value.RankUuid);
            var newRank = Ranks.FirstOrDefault(x => x.Level == currentRank.Level + 1);

            if (newRank != null && newRank.Level > currentRank.Level)
            {
                if(currentRank.Level == 0)
                {
                    GameLog.Info($"Guild {Name}: Sorry, the guild leader cannot be demoted.");
                    return;
                }
                member.Value.RankUuid = newRank.Uuid;
                GameLog.Info($"Guild {Name}: Demoting {member.Value.Name} to rank {newRank.Name}");
            }
        }

        public void ChangeRankTitle(string oldTitle, string newTitle)
        {
            var rank = Ranks.FirstOrDefault(x => x.Name == oldTitle);

            if(rank != null)
            {
                rank.Name = newTitle;
                GameLog.Info($"Guild {Name}: Renaming rank {oldTitle} to rank {newTitle}");
            }
        }

        public void AddRank(string title) //adds a new rank at the lowest tier
        {
            if (Ranks.Any(x => x.Name == title)) return;

            var lowestRank = Ranks.Aggregate((r1, r2) => r1.Level > r2.Level ? r1 : r2);

            var rank = new GuildRank() { Uuid = Guid.NewGuid().ToString(), Name = title, Level = lowestRank.Level + 1 };

            Ranks.Add(rank);
            GameLog.Info($"Guild {Name}: New rank {rank.Name} added as level {rank.Level}");
        }

        public void RemoveRank() //only remove the lowest tier rank and move all members in rank up one level.
        {
            var lowestRank = Ranks.Aggregate((r1, r2) => r1.Level > r2.Level ? r1 : r2);
            var nextRank = Ranks.FirstOrDefault(x => x.Level == lowestRank.Level - 1);

            if(nextRank != null && nextRank.Level != 0)
            {
                var moveMembers = Members.Where(x => x.Value.RankUuid == lowestRank.Uuid).ToList();

                foreach(var member in moveMembers)
                {
                    member.Value.RankUuid = nextRank.Uuid;
                    GameLog.Info($"Guild {Name}: Member {member.Value.Name} moved to rank {nextRank.Name} due to rank deletion");
                }

                //remove lowest rank here to avoid missing members
                Ranks.Remove(lowestRank);
                GameLog.Info($"Guild {Name}: Deleted rank {lowestRank.Name}");
            }

        }

        public (string GuildName, string Rank ) GetUserDetails(string uuid)
        {
            var member = Members.FirstOrDefault(x => x.Key == uuid);

            var guildName = Name;
            var rank = Ranks.FirstOrDefault(x => x.Uuid == member.Value.RankUuid);

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
            foreach(var member in Members)
            {
                var rank = Ranks.FirstOrDefault(x => x.Uuid == member.Value.RankUuid).Name;
                ret.Add(member.Value.Name, rank);
            }
            return ret;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class GuildCharter
    {
        [JsonProperty]
        public string Uuid { get; set; }
        [JsonProperty]
        public string GuildName { get; set; }
        [JsonProperty]
        public string LeaderUuid { get; set; }
        [JsonProperty]
        public Dictionary<string, string> Supporters { get; set; }
        

        public GuildCharter()
        {

        }

        public GuildCharter(string guildName)
        {
            GuildName = guildName;
            Supporters = new Dictionary<string, string>();
        }

        public bool AddSupporter(User user)
        {
            Supporters.Add(user.Uuid, user.Name);
            return true;
        }

        public bool CreateGuild()
        {
            if(Supporters.Count == 10)
            {
                var leader = Game.World.WorldData.Get<User>(LeaderUuid);
                var founders = new List<User>();

                foreach(var supporter in Supporters)
                {
                    //get user by name, switch to uuid once that conversion is complete
                    var user = Game.World.WorldData.Get<User>(supporter.Value);

                    founders.Add(user);
                }

                var guild = new Guild(GuildName, leader, founders);
                guild.Save();

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

