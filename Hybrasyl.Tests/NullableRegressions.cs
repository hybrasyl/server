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

using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Players;
using Hybrasyl.Subsystems.Statuses;
using Hybrasyl.Xml.Objects;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Tests;

/// <summary>
///     Regression tests pinning the intentional NRE-to-graceful fixes introduced by the
///     nullable migration. Each test
///     exercises a null input that would have thrown a NullReferenceException on main
///     and asserts the graceful recovery path (no-op / default / early return) instead.
/// </summary>
[Collection("Hybrasyl")]
public class NullableRegressions
{
    public NullableRegressions(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    public HybrasylFixture Fixture { get; set; }

    private Monster NewGabbaghoul()
    {
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        return new Monster(monsterXml, SpawnFlags.AiDisabled, 99);
    }

    // Site: Exchange.cs AddItem — crafted exchange packet referencing an empty inventory
    // slot. Pre-migration: NRE dereferencing the null item. Now: AddItem no-ops (false).
    [Fact]
    public void ExchangeAddItemFromEmptySlotIsIgnored()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();

        var exchange = new Exchange(Fixture.TestUser, Fixture.SecondTestUser);
        var result = true;
        var ex = Record.Exception(() => result = exchange.AddItem(Fixture.TestUser, 5));
        Assert.Null(ex);
        Assert.False(result);
        Assert.Equal(0, Fixture.TestUser.Inventory.Count);
        Assert.Equal(0, Fixture.SecondTestUser.Inventory.Count);
    }

    // Site: Merchant.cs GetOnHand — reading on-hand count for an item the merchant does
    // not stock. Pre-migration: NRE on FirstOrDefault(...).OnHand. Now: reports 0.
    [Fact]
    public void MerchantGetOnHandForUnstockedItemReturnsZero()
    {
        var merchant = Fixture.Map.Objects.OfType<Merchant>().FirstOrDefault(predicate: x => x.Name == "Maria");
        Assert.NotNull(merchant);

        uint onHand = 42;
        var ex = Record.Exception(() => onHand = merchant.GetOnHand("Definitely Not A Real Item"));
        Assert.Null(ex);
        Assert.Equal(0u, onHand);
    }

    // Site: Merchant.cs ReduceInventory — decrementing stock for an unstocked item name.
    // Pre-migration: NRE. Now: silent no-op, stock unchanged.
    [Fact]
    public void MerchantReduceInventoryForUnstockedItemIsNoOp()
    {
        var merchant = Fixture.Map.Objects.OfType<Merchant>().FirstOrDefault(predicate: x => x.Name == "Maria");
        Assert.NotNull(merchant);

        var before = merchant.GetOnHandInventory().Select(selector: x => (x.Item.Name, x.OnHand)).ToList();
        var ex = Record.Exception(() => merchant.ReduceInventory("Definitely Not A Real Item", 10));
        Assert.Null(ex);
        var after = merchant.GetOnHandInventory().Select(selector: x => (x.Item.Name, x.OnHand)).ToList();
        Assert.Equal(before, after);
    }

    // Site: Monster.cs BehaviorSet setter — assigning null to a monster that already has
    // a behavior set. Pre-migration: NRE in ProcessCastingSets(value.Behavior...). Now:
    // processes an empty casting set list.
    [Fact]
    public void MonsterBehaviorSetNullAssignmentDoesNotThrow()
    {
        var monster = NewGabbaghoul();
        Assert.NotNull(monster.BehaviorSet);
        monster.X = 29;
        monster.Y = 29;
        Fixture.Map.InsertMonster(monster); // CastableController is created on insert

        var ex = Record.Exception(() => monster.BehaviorSet = null);
        Assert.Null(ex);
        Assert.Null(monster.BehaviorSet);
    }

    // Site: Monster.cs Damage (elemental immunity check) — elemental hit on a monster
    // with no behavior set. Pre-migration: NRE on BehaviorSet.ImmuneToElement. Now:
    // treated as non-immune; damage applies.
    [Fact]
    public void MonsterElementalDamageWithNullBehaviorSetDoesNotThrow()
    {
        var monster = NewGabbaghoul();
        monster.X = 30;
        monster.Y = 30;
        Fixture.Map.InsertMonster(monster);
        monster.BehaviorSet = null;

        var hpBefore = monster.Stats.Hp;
        var ex = Record.Exception(() => monster.Damage(50, ElementType.Fire));
        Assert.Null(ex);
        Assert.True(monster.Stats.Hp < hpBefore,
            $"Elemental damage should apply as non-immune (hp {monster.Stats.Hp}, was {hpBefore})");
    }

    // Site: Monster.cs Damage (castable immunity check) — castable hit on a monster with
    // no behavior set. Pre-migration: NRE on BehaviorSet.ImmuneToCastable. Now: treated
    // as non-immune; damage applies.
    [Fact]
    public void MonsterCastableDamageWithNullBehaviorSetDoesNotThrow()
    {
        var monster = NewGabbaghoul();
        monster.X = 31;
        monster.Y = 31;
        Fixture.Map.InsertMonster(monster);
        monster.BehaviorSet = null;

        var castable = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "Assail").FirstOrDefault();
        Assert.NotNull(castable);

        var hpBefore = monster.Stats.Hp;
        var ex = Record.Exception(() => monster.Damage(50, castable: castable));
        Assert.Null(ex);
        Assert.True(monster.Stats.Hp < hpBefore,
            $"Castable damage should apply as non-immune (hp {monster.Stats.Hp}, was {hpBefore})");
    }

    // Site: Monster.cs LootableXp setter/getter — monster spawned with no loot. Setter
    // pre-migration: NRE on Loot.Xp. Now: setter no-ops, getter reports 0.
    [Fact]
    public void MonsterLootableXpWithNullLootIsNoOp()
    {
        var monster = NewGabbaghoul(); // loot defaults to null

        var ex = Record.Exception(() => monster.LootableXp = 500);
        Assert.Null(ex);
        Assert.Equal(0u, monster.LootableXp);
        Assert.Equal(0u, monster.LootableGold);
    }

    // Site: Creature.cs Damage (tagging / loot rights) — FirstHitter is a Monster, not a
    // User (e.g. summon or monster-vs-monster tagging). Pre-migration: NRE on
    // (FirstHitter as User).Group. Now: tagging check fails closed; damage from the
    // unrelated user is ignored (early return).
    [Fact]
    public void MonsterDamageWithNonUserFirstHitterDoesNotThrow()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();

        var victim = NewGabbaghoul();
        victim.X = 32;
        victim.Y = 32;
        Fixture.Map.InsertMonster(victim);

        var tagger = NewGabbaghoul();
        tagger.Name = Fixture.TestUser.Name;

        // The FirstHitter survival check requires FirstHitter's name to belong to a
        // connected user; wire the fixture user up with a test client and register it.
        var clientField = typeof(User).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(clientField);
        var oldClient = clientField.GetValue(Fixture.TestUser);
        clientField.SetValue(Fixture.TestUser, new TestClient(new TestSocket()));
        Game.World.AddUser(Fixture.TestUser, Fixture.TestUser.ConnectionId);

        try
        {
            Assert.True(Game.World.UserConnected(Fixture.TestUser.Name),
                "Test user should read as connected for the tagging check");

            // First hit establishes a recent LastHitTime (avoids the tagging-timeout reset)
            victim.Damage(10, attacker: Fixture.TestUser);
            var hpAfterFirstHit = victim.Stats.Hp;

            // Force the invariant-violating state: a Monster as FirstHitter
            victim.FirstHitter = tagger;

            var ex = Record.Exception(() => victim.Damage(10, attacker: Fixture.SecondTestUser));
            Assert.Null(ex);
            // Tagging check fails closed: the unrelated user's damage is not applied
            Assert.Equal(hpAfterFirstHit, victim.Stats.Hp);
            Assert.Same(tagger, victim.FirstHitter);
        }
        finally
        {
            Game.World.RemoveUser(Fixture.TestUser.Name);
            clientField.SetValue(Fixture.TestUser, oldClient);
        }
    }

    // Site: World.cs MerchantMenuHandler_WithdrawItem — withdraw-confirm packet arriving
    // with no pending withdraw item selected (desynced flow). Pre-migration: null passed
    // into WithdrawItemConfirm, NRE. Now: withdraw silently aborted.
    [Fact]
    public void VaultWithdrawItemWithNoPendingItemIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = Fixture.Map.Objects.OfType<Merchant>().FirstOrDefault(predicate: x => x.Name == "Maria");
        Assert.NotNull(merchant);
        Assert.Null(Fixture.TestUser.PendingWithdrawItem);

        var handler = typeof(World).GetMethod("MerchantMenuHandler_WithdrawItem",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handler);

        // 0xAA <len16> <opcode> <ordinal> <data: string8 "1">
        var packet = new ClientPacket(new byte[] { 0xAA, 0x00, 0x03, 0x38, 0x00, 0x01, 0x31 });

        var ex = Record.Exception(() => handler.Invoke(Game.World, new object[] { Fixture.TestUser, merchant, packet }));
        Assert.Null(ex);
        Assert.Equal(0, Fixture.TestUser.Inventory.Count);
    }

    // Site: User.cs Resurrect — no Death handler configured. Pre-migration: NRE reading
    // handler.LegendMark. Now: no legend mark is added and resurrection proceeds.
    [Fact]
    public void ResurrectWithNoDeathHandlerDoesNotThrow()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport(Fixture.Map.Id, 20, 20);

        var savedHandlers = Game.ActiveConfiguration.Handlers;
        Game.ActiveConfiguration.Handlers = null;
        try
        {
            Fixture.TestUser.Condition.Alive = false;
            var ex = Record.Exception(() => Fixture.TestUser.Resurrect());
            Assert.Null(ex);
            Assert.True(Fixture.TestUser.Condition.Alive);
            Assert.Equal(1u, Fixture.TestUser.Stats.Hp);
            Assert.Equal(1u, Fixture.TestUser.Stats.Mp);
        }
        finally
        {
            Game.ActiveConfiguration.Handlers = savedHandlers;
            Fixture.TestUser.Teleport(Fixture.Map.Id, 20, 20);
        }
    }

    // Site: Creature.cs ProcessStatusTicks — a coma status (one that sets the Coma
    // condition) expiring on a Monster, violating the User-only coma invariant.
    // Pre-migration: NRE on (this as User).OnDeath(). Now: no-op; the status is
    // still removed.
    [Fact]
    public void MonsterComaStatusExpiryDoesNotThrow()
    {
        Assert.True(
            Game.World.WorldData.TryGetValue<Hybrasyl.Xml.Objects.Status>("TestAddComa", out var comaTemplate),
            "Status TestAddComa not found");

        var monster = NewGabbaghoul();
        monster.X = 33;
        monster.Y = 33;
        Fixture.Map.InsertMonster(monster);
        monster.BehaviorSet = null; // ensure no status immunities interfere

        // The real TestAddComa status (sets the Coma condition, which the expiry-death
        // path matches) with a zero duration override so it is expired on arrival.
        var status = new CreatureStatus(comaTemplate, monster, duration: 0);
        Assert.True(monster.ApplyStatus(status));
        Assert.True(status.Expired, "Zero-duration status should be immediately expired");

        var ex = Record.Exception(() => monster.ProcessStatusTicks());
        Assert.Null(ex);
        Assert.Equal(0, monster.ActiveStatusCount);
    }

    // Site: GlobalConnectionManifest.DeregisterClient — deregistering a client that was
    // never registered (double-deregistration / connection race). The TryRemove calls
    // tolerate the missing keys and the method returns gracefully.
    [Fact]
    public void DeregisterUnknownClientDoesNotThrow()
    {
        var client = new TestClient(new TestSocket()); // Server null => ServerTypes.World
        var ex = Record.Exception(() => GlobalConnectionManifest.DeregisterClient(client));
        Assert.Null(ex);
        Assert.False(GlobalConnectionManifest.ConnectedClients.ContainsKey(client.ConnectionId));
        Assert.False(GlobalConnectionManifest.WorldClients.ContainsKey(client.ConnectionId));
    }

    // Site: Script.ExecuteExpression/ExecuteFunction — the disabled early-returns build
    // fresh results, not the ScriptExecutionResult.Disabled factory, so the "script is
    // disabled" diagnostic must be set on that path too or consumers (the GM REPL) show
    // an empty message.
    [Fact]
    public void DisabledScriptExecutionCarriesDiagnostic()
    {
        var script = new Subsystems.Scripting.Script("disabled_diag_test.lua", Game.World.ScriptProcessor)
        { Disabled = true };

        var exprResult = script.ExecuteExpression("return 1");
        Assert.Equal(Subsystems.Scripting.ScriptResult.Disabled, exprResult.Result);
        Assert.Equal("script is disabled", exprResult.Error.HumanizedError);

        var fnResult = script.ExecuteFunction("AnyFunction");
        Assert.Equal(Subsystems.Scripting.ScriptResult.Disabled, fnResult.Result);
        Assert.Equal("script is disabled", fnResult.Error.HumanizedError);
    }

    // Site: Inventory indexer — crafted packets can carry out-of-range slot bytes (0, or
    // above inventory size). Pre-change: ArgumentException into the World.cs packet-handler
    // catch-all (exception report + log). Now: reads as empty (null), mirroring Book, so
    // packet handlers take their existing, pinned empty-slot recovery paths.
    [Fact]
    public void InventoryOutOfRangeSlotReadsAsEmpty()
    {
        var ex = Record.Exception(() =>
        {
            Assert.Null(Fixture.TestUser.Inventory[0]);
            Assert.Null(Fixture.TestUser.Inventory[byte.MaxValue]);
        });
        Assert.Null(ex);
    }

    // Site: ClientPacket.Decrypt — a default-key-encrypted packet arriving on a connection
    // that has not completed the key exchange (crafted traffic straight to the login/world
    // port). Pre-migration: NRE dereferencing the null key. Now: Decrypt reports failure
    // and the caller discards the packet.
    [Fact]
    public void DecryptBeforeKeyExchangeReportsFailure()
    {
        // 0x02 is EncryptMethod.Normal (default key); one payload byte so the XOR loop runs.
        var buffer = new byte[] { 0xAA, 0x00, 0x06, 0x02, 0x01, 0xFF, 0x00, 0x00, 0x00 };
        var packet = new ClientPacket(buffer);
        Assert.True(packet.UseDefaultKey);

        var keyless = new Client();
        var result = true;
        var ex = Record.Exception(() => result = packet.Decrypt(keyless));
        Assert.Null(ex);
        Assert.False(result);

        var keyed = new Client { EncryptionKey = "UrkcnItnI"u8.ToArray() };
        Assert.True(packet.Decrypt(keyed));
    }

    // Site: ServerPacket.Encrypt — a default-key-encrypted response queued before the key
    // exchange has completed. Pre-migration: NRE dereferencing the null key. Now: Encrypt
    // reports failure and the caller drops the packet instead of transmitting it.
    [Fact]
    public void EncryptBeforeKeyExchangeReportsFailure()
    {
        // 0x02 (LoginMessage) is EncryptMethod.Normal (default key).
        var packet = new ServerPacket(0x02);
        packet.WriteByte(0x01);
        packet.GenerateFooter();

        var keyless = new Client();
        var result = true;
        var ex = Record.Exception(() => result = packet.Encrypt(keyless));
        Assert.Null(ex);
        Assert.False(result);
    }

    // Site: GlobalConnectionManifest.RequestEncryptionKey — key endpoint returns a JSON
    // null body. Pre-migration: a null key was returned (NRE downstream). Now: the
    // "NOTVALID!" sentinel key is returned.
    [Fact]
    public async Task RequestEncryptionKeyNullResponseReturnsInvalidSentinel()
    {
        var (listener, prefix) = StartListener();
        var served = ServeOneJsonNull(listener);
        try
        {
            var key = GlobalConnectionManifest.RequestEncryptionKey(prefix, IPAddress.Loopback);
            await served.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(Encoding.ASCII.GetBytes("NOTVALID!"), key);
        }
        finally
        {
            listener.Close();
        }
    }

    // Site: GlobalConnectionManifest.ValidateEncryptionKey — validation endpoint returns
    // a JSON null body. Pre-migration: NRE unboxing the null bool (swallowed by the
    // catch). Now: the null-coalescing guard returns false directly.
    [Fact]
    public async Task ValidateEncryptionKeyNullResponseReturnsFalse()
    {
        var (listener, prefix) = StartListener();
        var served = ServeOneJsonNull(listener);
        try
        {
            var valid = GlobalConnectionManifest.ValidateEncryptionKey(prefix,
                new ServerToken { Ip = "127.0.0.1", Seed = new byte[] { 0x01 } });
            await served.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(valid);
        }
        finally
        {
            listener.Close();
        }
    }

    private static (HttpListener Listener, string Prefix) StartListener()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var port = Random.Shared.Next(20000, 60000);
            var prefix = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
                return (listener, prefix);
            }
            catch (Exception)
            {
                listener.Close();
            }
        }

        throw new InvalidOperationException("Could not find a free port for the test HTTP listener");
    }

    private static Task ServeOneJsonNull(HttpListener listener) =>
        Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            using (var reader = new StreamReader(ctx.Request.InputStream))
            {
                await reader.ReadToEndAsync();
            }

            var body = Encoding.UTF8.GetBytes("null");
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
        });
}
