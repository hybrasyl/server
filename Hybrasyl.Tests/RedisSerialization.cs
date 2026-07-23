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

using Hybrasyl.Extensions;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Persistence;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Messaging;
using Hybrasyl.Subsystems.Players;
using Hybrasyl.Subsystems.Players.Guilds;
using Hybrasyl.Xml.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests;

/// <summary>
///     Pins the Redis persistence wire contract ahead of the Newtonsoft.Json ->
///     System.Text.Json migration. Two layers:
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
        var reloaded = Cache.Get<T>(key);
        Assert.NotNull(reloaded);
        Cache.Set(secondKey, reloaded);
        var second = (string)Cache.StringGet(secondKey);
        AssertJsonEquivalent(JToken.Parse(first), JToken.Parse(second), typeof(T).Name);
        return reloaded;
    }

    private static void AssertJsonEquivalent(JToken expected, JToken actual, string context)
    {
        if (JToken.DeepEquals(expected, actual)) return;
        var diffs = new List<string>();
        CollectDiffs(expected, actual, "$", diffs);
        Assert.Fail(
            $"{context}: serialized JSON differs after round trip, {diffs.Count} path(s):\n" +
            string.Join('\n', diffs.Take(25)));
    }

    private static void CollectDiffs(JToken expected, JToken actual, string path, List<string> diffs)
    {
        if (JToken.DeepEquals(expected, actual)) return;
        if (expected is JObject expectedObj && actual is JObject actualObj)
        {
            var names = expectedObj.Properties().Select(selector: p => p.Name)
                .Union(actualObj.Properties().Select(selector: p => p.Name));
            foreach (var name in names)
            {
                var e = expectedObj[name];
                var a = actualObj[name];
                if (e == null)
                    diffs.Add($"{path}.{name}: absent before round trip, present after");
                else if (a == null)
                    diffs.Add($"{path}.{name}: present before round trip, absent after");
                else
                    CollectDiffs(e, a, $"{path}.{name}", diffs);
            }

            return;
        }

        if (expected is JArray expectedArr && actual is JArray actualArr)
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

    private static string Describe(JToken token)
    {
        var text = token.ToString(Formatting.None);
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

        var firstToken = JToken.Parse(first);
        // Guard: the status snapshot must actually be on the wire for this test to mean anything
        Assert.True(firstToken["Statuses"] is JToken statuses && statuses.HasValues,
            "expected serialized user to contain a status snapshot");

        AssertJsonEquivalent(firstToken, JToken.Parse(second), "User");

        Assert.Equal(user.Name, reloaded.Name);
        Assert.Equal(user.Guid, reloaded.Guid);
        Assert.Equal(255, reloaded.Stats.BaseStr);
        Assert.Equal("A serialization test user", reloaded.ProfileText);
        Assert.Equal(2, reloaded.Legend.Count);
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

    // Mirrors StackExchangeRedisExtensions.Deserialize: reads use default serializer settings
    private static T DeserializeAsProduction<T>(string json) => JsonConvert.DeserializeObject<T>(json);

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

    #endregion

    #region System.Text.Json migration parity

    /// <summary>
    ///     Round-trips an object through RedisJsonSerializer and proves two things:
    ///     the STJ serialization is a fixed point, and the round-tripped object is
    ///     semantically identical to the original under the Newtonsoft contract
    ///     (canonical Newtonsoft re-serialization compares equal) - i.e. the STJ
    ///     resolver sees exactly the member set Newtonsoft did, and every value
    ///     survives. Also asserts the new wire format carries no reference metadata.
    /// </summary>
    private static T AssertStjParity<T>(T obj)
    {
        var first = RedisJsonSerializer.Serialize(obj);
        var firstJson = System.Text.Encoding.UTF8.GetString(first);
        Assert.DoesNotContain("\"$id\"", firstJson);
        Assert.DoesNotContain("\"$values\"", firstJson);

        var reloaded = RedisJsonSerializer.Deserialize<T>(first);
        Assert.NotNull(reloaded);
        var second = RedisJsonSerializer.Serialize(reloaded);
        AssertJsonEquivalent(JToken.Parse(firstJson), JToken.Parse(System.Text.Encoding.UTF8.GetString(second)),
            $"{typeof(T).Name} (STJ fixed point)");

        // Canonicalize both objects through Newtonsoft's default contract; trees only,
        // so no reference handling is needed for the comparison
        AssertJsonEquivalent(
            JToken.Parse(JsonConvert.SerializeObject(obj)),
            JToken.Parse(JsonConvert.SerializeObject(reloaded)),
            $"{typeof(T).Name} (Newtonsoft-canonical cross-check)");

        return reloaded;
    }

    [Fact]
    public void Stj_Vault_RoundTrips() => AssertStjParity(BuildGoldenVault());

    [Fact]
    public void Stj_GuildVault_RoundTrips() => AssertStjParity(BuildGuildVault());

    [Fact]
    public void Stj_ParcelStore_RoundTrips() => AssertStjParity(BuildParcelStore());

    [Fact]
    public void Stj_AuthInfo_RoundTrips() => AssertStjParity(BuildAuthInfo());

    [Fact]
    public void Stj_Mailbox_RoundTrips()
    {
        var reloaded = AssertStjParity(BuildMailbox());
        Assert.True(reloaded.Messages[0].Read, "read flag (private field _read) did not survive STJ");
        Assert.True(reloaded.HasUnreadMessages);
    }

    [Fact]
    public void Stj_SentMail_RoundTrips() => AssertStjParity(BuildSentMail());

    [Fact]
    public void Stj_Board_RoundTrips() => AssertStjParity(BuildBoard());

    [Fact]
    public void Stj_Guild_RoundTrips() => AssertStjParity(BuildGuild());

    [Fact]
    public void Stj_User_RoundTrips()
    {
        var user = Fixture.CreateUser("StjRoundTripUser");
        Game.World.Insert(user);
        user.Teleport(Fixture.Map.Id, 12, 12);
        try
        {
            PopulateUser(user, deterministic: false);
            var castable = Game.World.WorldData
                .Find<Castable>(condition: x => x.Name == "TestPlusAc").FirstOrDefault();
            Assert.NotNull(castable);
            user.SpellBook.Add(castable);
            Assert.True(user.UseCastable(castable, user));
            Assert.NotEmpty(user.CurrentStatuses);
            // Populates the Statuses snapshot (and writes via the current wire, which is irrelevant here)
            user.Save(serializeStatus: true);

            var reloaded = AssertStjParity(user);

            Assert.Equal(user.Guid, reloaded.Guid);
            Assert.Equal(2, reloaded.Legend.Count);
            Assert.True(reloaded.Legend.TryGetMark("ser1", out _), "legend index not rebuilt after STJ deserialize");
            Assert.Equal("Test Item", reloaded.Inventory[1].Name);
            Assert.Equal("Equip Test Weapon", reloaded.Equipment.Weapon.Name);
            Assert.Equal("TestPlusAc", reloaded.SpellBook.Single(predicate: s => s.Castable != null).Castable.Name);
        }
        finally
        {
            user.Map.Remove(user);
            Game.World.Remove(user);
        }
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
        // (Newtonsoft sets private setters); populate them so the contract test covers that path
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
