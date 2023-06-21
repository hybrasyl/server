using Hybrasyl.Objects;
using Hybrasyl.Xml.Manager;
using Hybrasyl.Xml.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hybrasyl.Internals;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Hybrasyl.Tests;

public class HybrasylFixture : IDisposable
{
    private IMessageSink sink;

    public HybrasylFixture(IMessageSink sink)
    {
        this.sink = sink;
        Log.Logger = new LoggerConfiguration()
            .WriteTo.TestOutput(sink)
            .CreateLogger();
        var submoduleDir = AppDomain.CurrentDomain.BaseDirectory.Split("Hybrasyl.Tests");
        Game.LoadCollisions();
        Game.DataDirectory = Settings.HybrasylTests.JsonSettings.DataDirectory;
        Game.WorldDataDirectory = Settings.HybrasylTests.JsonSettings.WorldDataDirectory;
        Game.LogDirectory = Settings.HybrasylTests.JsonSettings.LogDirectory;
        var manager = new XmlDataManager(Game.WorldDataDirectory);
        manager.LoadData();
        var rHost = Environment.GetEnvironmentVariable("REDIS_HOST");
        var rPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
        var rawPort = Environment.GetEnvironmentVariable("REDIS_PORT");
        var rawDb = Environment.GetEnvironmentVariable("REDIS_DB");

        var redisConn = new RedisConnection
        {
            Port = int.TryParse(rawPort, out var rPort) ? rPort : 6379,
            Database = int.TryParse(rawDb, out var rDb) ? rDb : 15,
            Password = rPassword,
            Host = string.IsNullOrWhiteSpace(rHost) ? "127.0.0.1" : rHost
        };
        sink.OnMessage(new DiagnosticMessage($"Redis: {redisConn.Host}:{redisConn.Port}/{redisConn.Database}"));

        GameLog.Info();
        Game.World = new World(1337, redisConn, manager, "en_us", true);

        Game.World.CompileScripts();
        Game.World.SetPacketHandlers();
        Game.World.SetControlMessageHandlers();
        Game.World.StartControlConsumers();
        if (!Game.World.LoadData())
            throw new InvalidDataException("LoadData encountered errors");

        Map = Game.World.WorldState.Get<MapObject>("40000");
        MapNoCasting = Game.World.WorldState.Get<MapObject>("40000");

        var xmlNation = new Nation
        {
            Default = true,
            Description = "Test Nation",
            Flag = 0,
            Name = "Test",
            SpawnPoints = new List<SpawnPoint> { new() { MapName = "Test Map", X = 5, Y = 5 } }
        };
        Game.World.WorldData.Add(xmlNation, xmlNation.Name);

        TestItem = new Item
        {
            Name = "Test Item"
        };
        TestItem.Properties.Stackable.Max = 1;
        TestItem.Properties.Equipment = new Hybrasyl.Xml.Objects.Equipment
        { WeaponType = WeaponType.None, Slot = EquipmentSlot.None };
        TestItem.Properties.Physical = new Physical { Durability = 1000, Weight = 1 };
        TestItem.Properties.Categories = new List<Category>
        {
            new() { Value = "junk" },
            new() { Value = "xmlitem" }
        };
        Game.World.WorldData.Add(TestItem, TestItem.Id);

        StackableTestItem = new Item
        {
            Name = "Stackable Test Item"
        };
        StackableTestItem.Properties.Stackable.Max = 20;
        StackableTestItem.Properties.Equipment = new Hybrasyl.Xml.Objects.Equipment
        { WeaponType = WeaponType.None, Slot = EquipmentSlot.None };
        StackableTestItem.Properties.Physical = new Physical { Durability = 1000, Weight = 1 };
        StackableTestItem.Properties.Categories = new List<Category>
        {
            new() { Value = "nonjunk" },
            new() { Value = "stackable" },
            new() { Value = "xmlitem" }
        };

        Game.World.WorldData.Add(StackableTestItem);

        foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
        {
            var item = new Item { Name = $"Equip Test {slot}" };
            item.Properties.Stackable.Max = 1;
            item.Properties.Equipment = new Hybrasyl.Xml.Objects.Equipment
            { WeaponType = slot == EquipmentSlot.Weapon ? WeaponType.Dagger : WeaponType.None, Slot = slot };
            item.Properties.Physical = new Physical { Durability = 1000, Weight = 1 };
            Game.World.WorldData.Add(item);
            TestEquipment.Add(slot, item);
        }

        TestUser = new User
        {
            Name = "TestUser",
            Guid = Guid.NewGuid(),
            Gender = Gender.Female,
            Location =
            {
                Direction = Direction.South,
                Map = Map,
                X = 20,
                Y = 20
            },
            HairColor = 1,
            HairStyle = 1,
            Class = Class.Peasant,
            AuthInfo =
            {
                CreatedTime = DateTime.Now,
                FirstLogin = true,
                PasswordHash = "testing",
                LastPasswordChange = DateTime.Now,
                LastPasswordChangeFrom = "TestFixture"
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
                Gold = 0
            }
        };
        TestUser.AuthInfo.Save();
        TestUser.Nation = Game.World.DefaultNation;

        var vault = new Vault(TestUser.Guid);
        vault.Save();
        var parcelStore = new ParcelStore(TestUser.Guid);
        parcelStore.Save();
        TestUser.Save();
        Game.World.Insert(TestUser);
        Map.Insert(TestUser, TestUser.X, TestUser.Y, false);
    }

    public MapObject Map { get; }
    public MapObject MapNoCasting { get; }
    public Item TestItem { get; }
    public Item StackableTestItem { get; }
    public Dictionary<EquipmentSlot, Item> TestEquipment { get; } = new();
    public static byte InventorySize => 59;

    public User TestUser { get; }

    public CreatureBehaviorSet TestSet { get; set; }

    public void Dispose()
    {
        var ep = World.DatastoreConnection.GetEndPoints();
        if (ep.First() == null) return;
        var server = World.DatastoreConnection.GetServer(ep.First().ToString());
        server.FlushDatabase(15);
    }

    public void ResetUserStats()
    {
        TestUser.Stats = new StatInfo
        {
            BaseInt = 3,
            BaseStr = 3,
            BaseDex = 3,
            BaseCon = 3,
            BaseWis = 3,
            Level = 1,
            Gold = 1000,
            Hp = 50,
            Mp = 50,
            BaseHp = 50,
            BaseMp = 50,
            Experience = 1000,
            BaseAc = 100
        };
        TestUser.Class = Class.Peasant;
        TestUser.Inventory.Clear();
        TestUser.Equipment.Clear();
        TestUser.Vault.Clear();
    }
}

[CollectionDefinition("Hybrasyl")]
public class HybrasylCollection : IClassFixture<HybrasylFixture> { }
