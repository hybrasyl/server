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
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Casting;
using Hybrasyl.Extensions;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Persistence;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Messaging;
using Hybrasyl.Subsystems.Players;
using Hybrasyl.Subsystems.Players.Guilds;
using Hybrasyl.Xml.Objects;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using PlayerInventory = Hybrasyl.Subsystems.Players.Inventory;

namespace Hybrasyl.Tests;

/// <summary>
///     Pins the Redis persistence wire contract. Two layers:
///     <para>
///         Round-trip tests: every persisted root type is written through the production
///         serialization path, read back through the production deserialization path, and
///         written again. The two serializations must be identical JSON - any field that is
///         lost, mutated, or re-defaulted by a round trip shows up as a diff.
///     </para>
///     <para>
///         Golden corpus tests: JSON produced by the current serializer is checked in under
///         FixtureData/golden. A future serializer must read those exact bytes. Regenerate
///         deliberately with HYB_REGEN_GOLDEN=1 and review the diff - a changed golden file
///         is a wire-contract change.
///     </para>
/// </summary>
[Collection("Hybrasyl")]
public class RedisSerialization
{
    private const string KeyPrefix = "test:serialization:";

    // Frozen identifiers so regenerated golden files diff only when the wire contract changes
    private static readonly Guid OwnerGuid = Guid.Parse("7b1c4e9a-0d2f-4b6e-9a3c-1f5d8e7a2b4c");
    private static readonly Guid SecondGuid = Guid.Parse("2a9e6c1d-8f3b-4a7e-b5d2-0c4f9e1a6d3b");
    private static readonly Guid ThirdGuid = Guid.Parse("5e8d2b7f-3c1a-4d9b-8e6f-a2c4b1d7e9f0");
    private static readonly DateTime FixedTime = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly HybrasylFixture Fixture;

    public RedisSerialization(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private static IDatabase Cache => World.DatastoreConnection.GetDatabase();

    #region Round-trip infrastructure

    /// <summary>
    ///     Serialize -> deserialize -> serialize through the production path
    ///     (StackExchangeRedisExtensions.Set/Get) and assert the fixed point.
    /// </summary>
    private static T AssertRoundTripStable<T>(T obj)
    {
        var key = $"{KeyPrefix}{typeof(T).Name}";
        var secondKey = $"{key}:second";
        Cache.Set(key, obj);
        var first = (string)Cache.StringGet(key);
        // The wire format is plain trees; reference metadata is a contract violation
        Assert.DoesNotContain("\"$id\"", first);
        Assert.DoesNotContain("\"$values\"", first);
        var reloaded = Cache.Get<T>(key);
        Assert.NotNull(reloaded);
        Cache.Set(secondKey, reloaded);
        var second = (string)Cache.StringGet(secondKey);
        AssertJsonEquivalent(JsonNode.Parse(first), JsonNode.Parse(second), typeof(T).Name);
        return reloaded;
    }

    private static void AssertJsonEquivalent(JsonNode expected, JsonNode actual, string context)
    {
        if (JsonNode.DeepEquals(expected, actual)) return;
        var diffs = new List<string>();
        CollectDiffs(expected, actual, "$", diffs);
        Assert.Fail(
            $"{context}: serialized JSON differs after round trip, {diffs.Count} path(s):\n" +
            string.Join('\n', diffs.Take(25)));
    }

    private static void CollectDiffs(JsonNode expected, JsonNode actual, string path, List<string> diffs)
    {
        if (JsonNode.DeepEquals(expected, actual)) return;
        if (expected is JsonObject expectedObj && actual is JsonObject actualObj)
        {
            var names = expectedObj.Select(selector: p => p.Key)
                .Union(actualObj.Select(selector: p => p.Key));
            foreach (var name in names)
                if (!expectedObj.ContainsKey(name))
                    diffs.Add($"{path}.{name}: absent before round trip, present after");
                else if (!actualObj.ContainsKey(name))
                    diffs.Add($"{path}.{name}: present before round trip, absent after");
                else
                    CollectDiffs(expectedObj[name], actualObj[name], $"{path}.{name}", diffs);

            return;
        }

        if (expected is JsonArray expectedArr && actual is JsonArray actualArr)
        {
            if (expectedArr.Count != actualArr.Count)
            {
                diffs.Add($"{path}: array length {expectedArr.Count} -> {actualArr.Count}");
                return;
            }

            for (var i = 0; i < expectedArr.Count; i++)
                CollectDiffs(expectedArr[i], actualArr[i], $"{path}[{i}]", diffs);
            return;
        }

        diffs.Add($"{path}: {Describe(expected)} -> {Describe(actual)}");
    }

    private static string Describe(JsonNode node)
    {
        var text = node?.ToJsonString() ?? "null";
        return text.Length > 80 ? text[..77] + "..." : text;
    }

    #endregion

    #region Round-trip tests

    [Fact]
    public void Vault_RoundTripIsStable()
    {
        var vault = new Vault(OwnerGuid);
        Assert.True(vault.AddGold(31337));
        Assert.True(vault.AddItem("Test Item", 3));
        Assert.True(vault.AddItem("Stackable Test Item", 20));

        var reloaded = AssertRoundTripStable(vault);

        Assert.Equal(OwnerGuid, reloaded.OwnerGuid);
        Assert.Equal(31337u, reloaded.CurrentGold);
        Assert.Equal(vault.GoldLimit, reloaded.GoldLimit);
        Assert.Equal(vault.ItemLimit, reloaded.ItemLimit);
        Assert.Equal(2, reloaded.Items.Count);
        Assert.Equal(3u, reloaded.Items["Test Item"]);
        Assert.Equal(20u, reloaded.Items["Stackable Test Item"]);
    }

    [Fact]
    public void GuildVault_RoundTripIsStable()
    {
        var vault = BuildGuildVault();

        var reloaded = AssertRoundTripStable(vault);

        Assert.Equal(OwnerGuid, reloaded.OwnerGuid);
        Assert.Equal(1000000u, reloaded.CurrentGold);
        Assert.Equal(SecondGuid, reloaded.GuildMasterGuid);
        Assert.Equal(new List<Guid> { ThirdGuid }, reloaded.AuthorizedViewerGuids);
        Assert.Equal(new List<Guid> { SecondGuid, ThirdGuid }, reloaded.AuthorizedWithdrawalGuids);
        Assert.Equal(new List<Guid> { SecondGuid }, reloaded.CouncilMemberGuids);
        Assert.Equal(5000, reloaded.AuthorizedWithdrawalLimit);
        Assert.Equal(3, reloaded.CouncilMemberLimit);
    }

    [Fact]
    public void ParcelStore_RoundTripIsStable()
    {
        var store = BuildParcelStore();

        var reloaded = AssertRoundTripStable(store);

        Assert.Equal(OwnerGuid, reloaded.OwnerGuid);
        var parcel = Assert.Single(reloaded.Items);
        Assert.Equal("Aisling", parcel.Sender);
        Assert.Equal("Test Item", parcel.Item);
        Assert.Equal(5u, parcel.Quantity);
        var moneygram = Assert.Single(reloaded.Gold);
        Assert.Equal("Aisling", moneygram.Sender);
        Assert.Equal(12345u, moneygram.Amount);
    }

    [Fact]
    public void AuthInfo_RoundTripIsStable()
    {
        var auth = BuildAuthInfo();

        var reloaded = AssertRoundTripStable(auth);

        Assert.Equal(OwnerGuid, reloaded.UserGuid);
        Assert.Equal(UserState.Disconnected, reloaded.CurrentState);
        Assert.Equal(FixedTime, reloaded.LastLogin);
        Assert.Equal(FixedTime.AddMinutes(-5), reloaded.LastLoginFailure);
        Assert.Equal("127.0.0.1", reloaded.LastLoginFrom);
        Assert.Equal("10.0.0.1", reloaded.LastLoginFailureFrom);
        Assert.Equal(2, reloaded.LoginFailureCount);
        Assert.True(reloaded.FirstLogin);
        Assert.Equal("notarealhash", reloaded.PasswordHash);
        Assert.Equal("RedisSerializationTest", reloaded.LastPasswordChangeFrom);
    }

    [Fact]
    public void Mailbox_RoundTripIsStable()
    {
        var mailbox = BuildMailbox();

        var reloaded = AssertRoundTripStable(mailbox);

        Assert.Equal(OwnerGuid.ToString(), reloaded.Name);
        Assert.Equal(3, reloaded.Messages.Count);
        Assert.Equal(3, reloaded.CurrentId);
        Assert.True(reloaded.HasUnreadMessages);
        Assert.Equal("Subject 1", reloaded.Messages[0].Subject);
        Assert.Equal("Body of message 1", reloaded.Messages[0].Body);
        Assert.True(reloaded.Messages[0].Read, "read flag (private field _read) did not survive");
        Assert.False(reloaded.Messages[1].Read);
        Assert.True(reloaded.Messages[2].Deleted);
    }

    [Fact]
    public void SentMail_RoundTripIsStable()
    {
        var sent = BuildSentMail();

        var reloaded = AssertRoundTripStable(sent);

        Assert.Equal(FixedTime, reloaded.LastMailMessageSent);
        Assert.Equal("Aisling", reloaded.LastMailRecipient);
        Assert.Equal(FixedTime.AddMinutes(1), reloaded.LastBoardMessageSent);
        Assert.Equal("Test Board", reloaded.LastBoardRecipient);
        var message = Assert.Single(reloaded.Messages);
        Assert.True(message.Read);
        Assert.Contains("Body of message 1", message.Body);
    }

    [Fact]
    public void Board_RoundTripIsStable()
    {
        var board = BuildBoard();

        var reloaded = AssertRoundTripStable(board);

        Assert.Equal("golden test board", reloaded.Name);
        Assert.Equal("Golden Test Board", reloaded.DisplayName);
        Assert.True(reloaded.Global);
        Assert.Equal(2, reloaded.Messages.Count);
        Assert.Contains("aisling", reloaded.ModeratorList);
        Assert.Contains("devlin", reloaded.WriterList);
        Assert.Contains("riona", reloaded.ReaderList);
    }

    [Fact]
    public void Guild_RoundTripIsStable()
    {
        var guild = BuildGuild();

        var reloaded = AssertRoundTripStable(guild);

        Assert.Equal(OwnerGuid, reloaded.Guid);
        Assert.Equal("Golden Guild", reloaded.Name);
        Assert.Equal(2, reloaded.Ranks.Count);
        Assert.Equal("Guild Leader", reloaded.LeaderRank.Name);
        Assert.Equal(2, reloaded.Members.Count);
        Assert.Equal("GoldenUser", reloaded.Members[SecondGuid].Name);
    }

    [Fact]
    public void User_RoundTripIsStable()
    {
        var user = Fixture.CreateUser("RoundTripUser");
        Game.World.Insert(user);
        user.Teleport(Fixture.Map.Id, 10, 10);
        try
        {
            RoundTripUser(user);
        }
        finally
        {
            // A user left standing on the shared test map draws aggro in the monster tests
            user.Map.Remove(user);
            Game.World.Remove(user);
        }
    }

    [Fact]
    public void User_ConditionBackrefIsReattachedOnLoad()
    {
        var user = Fixture.CreateUser("BackrefUser");
        var key = $"{KeyPrefix}User:backref";
        Cache.Set(key, user);
        var reloaded = Cache.Get<User>(key);

        Assert.NotNull(reloaded.Condition);
        Assert.Same(reloaded, reloaded.Condition.Creature);

        // User-gated setters are silent no-ops when the backref is missing
        Assert.False(reloaded.Condition.InExchange);
        reloaded.Condition.InExchange = true;
        Assert.True(reloaded.Condition.InExchange);
    }

    [Fact]
    public void User_NonFiniteStatSavesAndRoundTrips()
    {
        var user = Fixture.CreateUser("NaNUser");
        user.Stats.BaseCrit = double.NaN;
        user.Stats.BaseMr = double.PositiveInfinity;
        var key = $"{KeyPrefix}User:nonfinite";
        Cache.Set(key, user); // a save-abort here would loop forever in production
        var reloaded = Cache.Get<User>(key);

        Assert.True(double.IsNaN(reloaded.Stats.BaseCrit));
        Assert.True(double.IsPositiveInfinity(reloaded.Stats.BaseMr));
    }

    [Fact]
    public void Book_MalformedSlotsAreDroppedNotFatal()
    {
        // A corrupt slot entry costs that slot, not the whole character load
        const string json =
            """[null,{"Name":"testplusac","LastCast":"2026-07-01T12:00:00Z","TotalUses":3,"MasteryLevel":1},{},{"LastCast":"2026-07-01T12:00:00Z"},{"Name":"testplusac"}]""";
        var book = JsonSerializer.Deserialize<SpellBook>(json, RedisJsonSerializer.Options);

        Assert.NotNull(book);
        Assert.Equal("TestPlusAc", book[1].Castable.Name);
        Assert.Equal(3u, book[1].UseCount);
        Assert.Null(book[2]); // {}
        Assert.Null(book[3]); // no Name
        Assert.NotNull(book[4]); // Name but no LastCast: survives with default timestamp
        Assert.Equal(default, book[4].LastCast);
    }

    [Fact]
    public void Book_OversizedArrayDoesNotWrapOntoEarlySlots()
    {
        // A corrupt array longer than 256 entries must not wrap a byte counter
        // back onto slot 1 and overwrite it
        var entries = new List<string>
        {
            "null",
            """{"Name":"testplusac","LastCast":"2026-07-01T12:00:00Z","TotalUses":1,"MasteryLevel":0}"""
        };
        entries.AddRange(Enumerable.Repeat("null", 255));
        entries.Add("""{"Name":"testplusac","LastCast":"2026-07-01T12:00:00Z","TotalUses":99,"MasteryLevel":0}""");
        var book = JsonSerializer.Deserialize<SpellBook>("[" + string.Join(",", entries) + "]",
            RedisJsonSerializer.Options);

        Assert.Equal(1u, book[1].UseCount);
    }

    private void RoundTripUser(User user)
    {
        PopulateUser(user, deterministic: false);

        // Apply a status so the snapshot path (Save(serializeStatus: true)) is exercised
        var castable = Game.World.WorldData
            .Find<Castable>(condition: x => x.Name == "TestPlusAc").FirstOrDefault();
        Assert.NotNull(castable);
        user.SpellBook.Add(castable);
        Assert.True(user.UseCastable(castable, user));
        Assert.NotEmpty(user.CurrentStatuses);

        user.Save(serializeStatus: true);
        var key = User.GetStorageKey(user.Name);
        var first = (string)Cache.StringGet(key);
        var reloaded = Cache.Get<User>(key);
        Assert.NotNull(reloaded);
        var secondKey = $"{KeyPrefix}User:second";
        Cache.Set(secondKey, reloaded);
        var second = (string)Cache.StringGet(secondKey);

        var firstNode = JsonNode.Parse(first);
        // Guard: the status snapshot must actually be on the wire for this test to mean anything
        Assert.True(firstNode["Statuses"] is JsonArray { Count: > 0 },
            "expected serialized user to contain a status snapshot");

        AssertJsonEquivalent(firstNode, JsonNode.Parse(second), "User");

        Assert.Equal(user.Name, reloaded.Name);
        Assert.Equal(user.Guid, reloaded.Guid);
        Assert.Equal(255, reloaded.Stats.BaseStr);
        Assert.Equal("A serialization test user", reloaded.ProfileText);
        Assert.Equal(2, reloaded.Legend.Count);
        Assert.True(reloaded.Legend.TryGetMark("ser1", out _), "legend index not rebuilt after deserialize");
        Assert.Equal("Test Item", reloaded.Inventory[1].Name);
        Assert.Equal("Equip Test Weapon", reloaded.Equipment.Weapon.Name);
        Assert.Single(reloaded.SpellBook);
        Assert.Equal("TestPlusAc", reloaded.SpellBook.Single().Castable.Name);
    }

    #endregion

    #region Golden corpus

    private static bool Regenerate => Environment.GetEnvironmentVariable("HYB_REGEN_GOLDEN") == "1";

    private static string GoldenDir => Path.Combine(SolutionRoot(), "Hybrasyl.Tests", "FixtureData", "golden");

    private static string SolutionRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Hybrasyl.sln"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException("Could not locate Hybrasyl.sln above the test output directory");
    }

    /// <summary>
    ///     Load a golden fixture, regenerating it through the production write path
    ///     (StackExchangeRedisExtensions.Set) when HYB_REGEN_GOLDEN=1.
    /// </summary>
    private static string GoldenJson<T>(Func<T> build)
    {
        var path = Path.Combine(GoldenDir, $"{typeof(T).Name}.json");
        if (Regenerate)
        {
            Directory.CreateDirectory(GoldenDir);
            var key = $"{KeyPrefix}golden:{typeof(T).Name}";
            Cache.Set(key, build());
            File.WriteAllText(path, (string)Cache.StringGet(key));
        }

        Assert.True(File.Exists(path),
            $"Golden fixture {path} is missing. Run the suite once with HYB_REGEN_GOLDEN=1 and commit the file.");
        return File.ReadAllText(path);
    }

    // Mirrors the production read path (StackExchangeRedisExtensions.Get)
    private static T DeserializeAsProduction<T>(string json) =>
        RedisJsonSerializer.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Golden_Vault_IsReadable()
    {
        var vault = DeserializeAsProduction<Vault>(GoldenJson(BuildGoldenVault));
        Assert.Equal(OwnerGuid, vault.OwnerGuid);
        Assert.Equal(31337u, vault.CurrentGold);
        Assert.Equal(2, vault.Items.Count);
        Assert.Equal(3u, vault.Items["Test Item"]);
    }

    [Fact]
    public void Golden_GuildVault_IsReadable()
    {
        var vault = DeserializeAsProduction<GuildVault>(GoldenJson(BuildGuildVault));
        Assert.Equal(OwnerGuid, vault.OwnerGuid);
        Assert.Equal(1000000u, vault.CurrentGold);
        Assert.Equal(SecondGuid, vault.GuildMasterGuid);
        Assert.Equal(5000, vault.AuthorizedWithdrawalLimit);
    }

    [Fact]
    public void Golden_ParcelStore_IsReadable()
    {
        var store = DeserializeAsProduction<ParcelStore>(GoldenJson(BuildParcelStore));
        Assert.Equal(OwnerGuid, store.OwnerGuid);
        Assert.Equal("Test Item", Assert.Single(store.Items).Item);
        Assert.Equal(12345u, Assert.Single(store.Gold).Amount);
    }

    [Fact]
    public void Golden_AuthInfo_IsReadable()
    {
        var auth = DeserializeAsProduction<AuthInfo>(GoldenJson(BuildAuthInfo));
        Assert.Equal(OwnerGuid, auth.UserGuid);
        Assert.Equal(FixedTime, auth.LastLogin);
        Assert.Equal("notarealhash", auth.PasswordHash);
        Assert.Equal(2, auth.LoginFailureCount);
    }

    [Fact]
    public void Golden_Mailbox_IsReadable()
    {
        var mailbox = DeserializeAsProduction<Mailbox>(GoldenJson(BuildMailbox));
        Assert.Equal(3, mailbox.Messages.Count);
        Assert.True(mailbox.HasUnreadMessages);
        Assert.True(mailbox.Messages[0].Read);
        Assert.Equal("Body of message 2", mailbox.Messages[1].Body);
    }

    [Fact]
    public void Golden_SentMail_IsReadable()
    {
        var sent = DeserializeAsProduction<SentMail>(GoldenJson(BuildSentMail));
        Assert.Equal("Aisling", sent.LastMailRecipient);
        Assert.True(Assert.Single(sent.Messages).Read);
    }

    [Fact]
    public void Golden_Board_IsReadable()
    {
        var board = DeserializeAsProduction<Board>(GoldenJson(BuildBoard));
        Assert.Equal("golden test board", board.Name);
        Assert.True(board.Global);
        Assert.Equal(2, board.Messages.Count);
        Assert.Contains("aisling", board.ModeratorList);
    }

    [Fact]
    public void Golden_Guild_IsReadable()
    {
        var guild = DeserializeAsProduction<Guild>(GoldenJson(BuildGuild));
        Assert.Equal("Golden Guild", guild.Name);
        Assert.Equal("GoldenUser", guild.Members[SecondGuid].Name);
    }

    [Fact]
    public void Golden_User_IsReadable()
    {
        var user = DeserializeAsProduction<User>(GoldenJson(BuildGoldenUser));
        Assert.Equal("GoldenUser", user.Name);
        Assert.Equal(OwnerGuid, user.Guid);
        Assert.Equal(Gender.Female, user.Gender);
        Assert.Equal(255, user.Stats.BaseStr);
        Assert.Equal(99, user.Stats.Level);
        Assert.Equal("A serialization test user", user.ProfileText);
        Assert.Equal(2, user.Legend.Count);
        Assert.Equal("Test Item", user.Inventory[1].Name);
        Assert.Equal(20, user.Inventory[2].Count);
        Assert.Equal("Equip Test Weapon", user.Equipment.Weapon.Name);
        Assert.Single(user.SpellBook);
        Assert.Equal("TestPlusAc", user.SpellBook.Single().Castable.Name);
    }

    [Fact]
    public void StatSnapshot_IsWireEquivalentToSource()
    {
        // The status-origin snapshot must carry exactly the wire-visible stat members
        var user = Fixture.CreateUser("SnapshotEquivUser");
        user.Stats.BaseCrit = 0.25;
        user.Stats.BaseMr = 1.5;
        user.Stats.Experience = 123456;
        var snapshotId = ((IStatSnapshotProvider)user).CreateStatSnapshot();
        Assert.True(Game.World.WorldState.TryGetValue(snapshotId, out CreatureSnapshot snapshot),
            $"snapshot {snapshotId} not in Game.World.WorldState; " +
            $"user.World == Game.World: {ReferenceEquals(((IStatSnapshotProvider)user).World, Game.World)}");
        AssertJsonEquivalent(
            JsonNode.Parse(System.Text.Encoding.UTF8.GetString(RedisJsonSerializer.Serialize(user.Stats))),
            JsonNode.Parse(System.Text.Encoding.UTF8.GetString(RedisJsonSerializer.Serialize(snapshot.Stats))),
            "StatInfo snapshot");
    }

    [Fact]
    public void CorruptBlobFailsWithKeyContext()
    {
        // A corrupt blob must still fail loudly, but naming the Redis key - a bare
        // JsonException at server startup is undiagnosable
        var key = $"{KeyPrefix}corrupt";
        Cache.StringSet(key, "{this is not json");
        var ex = Assert.Throws<InvalidDataException>(() => Cache.Get<Vault>(key));
        Assert.Contains(key, ex.Message);
        Assert.Contains(nameof(Vault), ex.Message);
    }

    [Fact]
    public void CorruptFieldInValidJsonAlsoFailsWithKeyContext()
    {
        // Syntactically valid JSON with corrupt content: the converter throws
        // FormatException (bad item GUID), not JsonException - the key-context
        // guarantee must cover the whole deserialization, not just JSON parsing
        var key = $"{KeyPrefix}corrupt:guid";
        Cache.StringSet(key,
            $$$"""{"1":{"Name":"Test Item","Count":1,"Id":"{{{Fixture.TestItem.Id}}}","Durability":1000,"Guid":"not-a-guid"}}""");
        var ex = Assert.Throws<InvalidDataException>(() => Cache.Get<PlayerInventory>(key));
        Assert.Contains(key, ex.Message);
    }

    [Fact]
    public void Golden_WriteSideMatchesCorpus()
    {
        // The goldens pin the write side too: dropping a [Persist] must fail here,
        // not silently survive because reads and writes share the same WirePlan
        AssertWriteMatchesGolden(BuildGoldenVault);
        AssertWriteMatchesGolden(BuildGuildVault);
        AssertWriteMatchesGolden(BuildParcelStore);
        AssertWriteMatchesGolden(BuildAuthInfo);
        AssertWriteMatchesGolden(BuildMailbox);
        AssertWriteMatchesGolden(BuildSentMail);
        AssertWriteMatchesGolden(BuildBoard);
        AssertWriteMatchesGolden(BuildGuild);
        AssertWriteMatchesGolden(BuildGoldenUser);
    }

    private static void AssertWriteMatchesGolden<T>(Func<T> build)
    {
        var key = $"{KeyPrefix}writeside:{typeof(T).Name}";
        Cache.Set(key, build());
        AssertJsonEquivalent(JsonNode.Parse(GoldenJson(build)),
            JsonNode.Parse((string)Cache.StringGet(key)),
            $"{typeof(T).Name} (write side vs golden)");
    }

    #endregion

    #region Builders

    private static Message BuildMessage(int id, bool read, bool deleted = false)
    {
        var message = new Message("Recipient", "Sender", $"Subject {id}", $"Body of message {id}")
        {
            Created = FixedTime,
            Guid = new Guid($"00000000-0000-0000-0000-{id:d12}").ToString(),
            Deleted = deleted
        };
        if (read)
        {
            message.Read = true;
            message.ReadTime = FixedTime.AddMinutes(id);
        }

        return message;
    }

    private static Vault BuildGoldenVault()
    {
        var vault = new Vault(OwnerGuid);
        vault.AddGold(31337);
        vault.AddItem("Test Item", 3);
        vault.AddItem("Stackable Test Item", 20);
        return vault;
    }

    private static GuildVault BuildGuildVault()
    {
        var vault = new GuildVault(OwnerGuid, 2000000, 100);
        vault.AddGold(1000000);
        vault.AddItem("Test Item", 10);
        // No public mutators exist for the authorization lists, but they are on the wire
        // (the resolver sets private setters); populate them so the contract test covers that path
        SetPrivate(vault, nameof(GuildVault.GuildMasterGuid), SecondGuid);
        SetPrivate(vault, nameof(GuildVault.AuthorizedViewerGuids), new List<Guid> { ThirdGuid });
        SetPrivate(vault, nameof(GuildVault.AuthorizedWithdrawalGuids), new List<Guid> { SecondGuid, ThirdGuid });
        SetPrivate(vault, nameof(GuildVault.CouncilMemberGuids), new List<Guid> { SecondGuid });
        SetPrivate(vault, nameof(GuildVault.AuthorizedWithdrawalLimit), 5000);
        SetPrivate(vault, nameof(GuildVault.CouncilMemberLimit), 3);
        return vault;
    }

    private static void SetPrivate<T>(T target, string property, object value) =>
        typeof(T).GetProperty(property)!.SetValue(target, value);

    private static ParcelStore BuildParcelStore()
    {
        var store = new ParcelStore(OwnerGuid);
        store.Items.Add(new Parcel("Aisling", "Test Item", 5));
        store.Gold.Add(new Moneygram("Aisling", 12345));
        return store;
    }

    private static AuthInfo BuildAuthInfo() =>
        new(OwnerGuid)
        {
            CurrentState = UserState.Disconnected,
            LastStateChange = FixedTime,
            LastLogin = FixedTime,
            LastLogoff = FixedTime.AddHours(1),
            LastLoginFailure = FixedTime.AddMinutes(-5),
            LastLoginFrom = "127.0.0.1",
            LastLoginFailureFrom = "10.0.0.1",
            LoginFailureCount = 2,
            CreatedTime = FixedTime.AddDays(-30),
            FirstLogin = true,
            PasswordHash = "notarealhash",
            LastPasswordChange = FixedTime.AddDays(-7),
            LastPasswordChangeFrom = "RedisSerializationTest"
        };

    private static Mailbox BuildMailbox()
    {
        var mailbox = new Mailbox(OwnerGuid) { Guid = SecondGuid };
        Assert.True(mailbox.ReceiveMessage(BuildMessage(1, read: true)));
        Assert.True(mailbox.ReceiveMessage(BuildMessage(2, read: false)));
        Assert.True(mailbox.ReceiveMessage(BuildMessage(3, read: false, deleted: true)));
        return mailbox;
    }

    private static SentMail BuildSentMail()
    {
        var sent = new SentMail(OwnerGuid)
        {
            Guid = ThirdGuid,
            LastMailMessageSent = FixedTime,
            LastMailRecipient = "Aisling",
            LastBoardMessageSent = FixedTime.AddMinutes(1),
            LastBoardRecipient = "Test Board"
        };
        // SentMail.ReceiveMessage stamps ReadTime with the wall clock; re-freeze it afterwards
        Assert.True(sent.ReceiveMessage(BuildMessage(1, read: false)));
        sent.Messages[0].ReadTime = FixedTime;
        return sent;
    }

    private static Board BuildBoard()
    {
        var board = new Board("golden test board")
        {
            DisplayName = "Golden Test Board",
            Global = true,
            Guid = SecondGuid
        };
        // Receive while the writer list is still empty (empty list = everyone may post);
        // board messages are stamped read on receipt, so re-freeze ReadTime for determinism
        Assert.True(board.ReceiveMessage(BuildMessage(1, read: false)));
        Assert.True(board.ReceiveMessage(BuildMessage(2, read: false)));
        board.Messages[0].ReadTime = FixedTime;
        board.Messages[1].ReadTime = FixedTime;
        board.SetAccessLevel("Aisling", BoardAccessLevel.Moderate);
        board.SetAccessLevel("Devlin", BoardAccessLevel.Write);
        board.SetAccessLevel("Riona", BoardAccessLevel.Read);
        return board;
    }

    private static Guild BuildGuild()
    {
        var leaderRank = new GuildRank { Guid = SecondGuid, Name = "Guild Leader", Level = 0 };
        var memberRank = new GuildRank { Guid = ThirdGuid, Name = "Member", Level = 3 };
        return new Guild
        {
            Guid = OwnerGuid,
            Name = "Golden Guild",
            Ranks = new List<GuildRank> { leaderRank, memberRank },
            Members = new Dictionary<Guid, GuildMember>
            {
                [SecondGuid] = new() { Name = "GoldenUser", RankGuid = leaderRank.Guid },
                [ThirdGuid] = new() { Name = "Aisling", RankGuid = memberRank.Guid }
            }
        };
    }

    /// <summary>
    ///     Populate a user with one of everything the wire contract carries. When
    ///     deterministic, all guids and timestamps are frozen for the golden corpus.
    /// </summary>
    private void PopulateUser(User user, bool deterministic)
    {
        var itemGuid1 = deterministic ? SecondGuid : Guid.NewGuid();
        var itemGuid2 = deterministic ? ThirdGuid : Guid.NewGuid();
        var weaponGuid = deterministic ? Guid.Parse("9c3e5a7b-1d4f-4c8a-b2e6-f0a8d6c4b2e0") : Guid.NewGuid();

        user.Stats.Experience = 90000;
        user.LevelPoints = 2;
        user.Title = "Tester";
        user.ProfileText = "A serialization test user";
        user.PortraitData = new byte[] { 1, 2, 3, 4 };
        user.IsMuted = false;
        user.Grouping = true;
        user.ClientSettings = new Dictionary<byte, bool> { [1] = true, [2] = false };

        user.Legend.AddMark(LegendIcon.Community, LegendColor.White, "Serialization test mark",
            FixedTime, prefix: "ser1");
        user.Legend.AddMark(LegendIcon.Victory, LegendColor.Blue, "Second mark",
            FixedTime.AddDays(1), prefix: "ser2", quantity: 3);
        if (deterministic)
            // AddMark stamps Created/LastUpdated with the wall clock; freeze them so
            // regenerated golden files differ only on genuine contract changes
            foreach (var mark in user.Legend)
            {
                SetPrivate(mark, nameof(LegendMark.Created), FixedTime);
                mark.LastUpdated = FixedTime;
            }

        var world = Game.GetDefaultServerGuid<World>();
        Assert.True(user.Inventory.AddItem(new ItemObject(Fixture.TestItem.Id, world, itemGuid1)));
        var stackable = new ItemObject(Fixture.StackableTestItem.Id, world, itemGuid2) { Count = 20 };
        Assert.True(user.Inventory.AddItem(stackable));
        Assert.True(user.AddEquipment(
            new ItemObject(Fixture.TestEquipment[EquipmentSlot.Weapon].Id, world, weaponGuid),
            (byte)EquipmentSlot.Weapon, false));
    }

    private User BuildGoldenUser()
    {
        var user = new User
        {
            Name = "GoldenUser",
            Guid = OwnerGuid,
            AccountGuid = SecondGuid,
            Gender = Gender.Female,
            Location = { Direction = Direction.South, Map = Fixture.Map, X = 10, Y = 10 },
            HairColor = 1,
            HairStyle = 1,
            Class = Class.Wizard,
            AuthInfo =
            {
                CreatedTime = FixedTime.AddDays(-30),
                FirstLogin = false,
                PasswordHash = "notarealhash",
                LastPasswordChange = FixedTime.AddDays(-7),
                LastPasswordChangeFrom = "RedisSerializationTest",
                LastLogin = FixedTime,
                LastStateChange = FixedTime
            },
            Stats =
            {
                BaseInt = 100,
                BaseStr = 255,
                BaseDex = 100,
                BaseCon = 100,
                BaseWis = 100,
                BaseAc = 100,
                Level = 99,
                BaseHp = 10000,
                Hp = 10000,
                BaseMp = 10000,
                Mp = 10000,
                Gold = 424242
            }
        };
        user.Nation = Game.World.DefaultNation;
        PopulateUser(user, deterministic: true);

        var castable = Game.World.WorldData
            .Find<Castable>(condition: x => x.Name == "TestPlusAc").FirstOrDefault();
        Assert.NotNull(castable);
        user.SpellBook.Add(castable);
        // Freeze the book slot timestamp the client-facing cast tracking would otherwise vary
        user.SpellBook.Single().LastCast = FixedTime;
        return user;
    }

    #endregion
}
