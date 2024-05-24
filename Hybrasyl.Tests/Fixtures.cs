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

using Hybrasyl.Internals;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Players;
using Hybrasyl.Xml.Manager;
using Hybrasyl.Xml.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly:
    CollectionBehavior(CollectionBehavior.CollectionPerClass, DisableTestParallelization = true,
        MaxParallelThreads = 1)]

namespace Hybrasyl.Tests;

public class HybrasylFixture : IDisposable
{
    private IMessageSink sink;

    public HybrasylFixture(IMessageSink sink)
    {
        this.sink = sink;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
            .WriteTo.Console().WriteTo.File("hybrasyl-tests-.log", rollingInterval: RollingInterval.Day).WriteTo
            .TestCorrelator()
            .CreateLogger();
        var submoduleDir = AppDomain.CurrentDomain.BaseDirectory.Split("Hybrasyl.Tests");
        Game.LoadCollisions();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Game.DataDirectory = Settings.HybrasylTests.PlatformSettings.Directories["Windows"].DataDirectory;
            Game.WorldDataDirectory = Settings.HybrasylTests.PlatformSettings.Directories["Windows"].WorldDataDirectory;
            Game.LogDirectory = Settings.HybrasylTests.PlatformSettings.Directories["Windows"].LogDirectory;
        }
        else
        {
            Game.DataDirectory = Settings.HybrasylTests.PlatformSettings.Directories["Linux"].DataDirectory;
            Game.WorldDataDirectory = Settings.HybrasylTests.PlatformSettings.Directories["Linux"].WorldDataDirectory;
            Game.LogDirectory = Settings.HybrasylTests.PlatformSettings.Directories["Linux"].LogDirectory;
        }

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
        Game.ActiveConfiguration = new ServerConfig();

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
        TestItem.Properties.Equipment = new Xml.Objects.Equipment
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
        StackableTestItem.Properties.Equipment = new Xml.Objects.Equipment
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
            item.Properties.Equipment = new Xml.Objects.Equipment
            { WeaponType = slot == EquipmentSlot.Weapon ? WeaponType.Dagger : WeaponType.None, Slot = slot };
            item.Properties.Physical = new Physical { Durability = 1000, Weight = 1 };
            Game.World.WorldData.Add(item);
            TestEquipment.Add(slot, item);
        }

        TestUser = CreateUser("TestUser");
        SerializableUser = CreateUser("TestSaveUser");

        Game.World.Insert(TestUser);
        TestUser.Teleport(TestUser.Map.Id, TestUser.X, TestUser.Y);
    }

    public MapObject Map { get; }
    public MapObject MapNoCasting { get; }
    public Item TestItem { get; }
    public Item StackableTestItem { get; }
    public Dictionary<EquipmentSlot, Item> TestEquipment { get; } = new();
    public static byte InventorySize => 59;

    public User TestUser { get; init; }
    public User SerializableUser { get; init; }

    public CreatureBehaviorSet TestSet { get; set; }

    public void Dispose()
    {
        try
        {
            var ep = World.DatastoreConnection.GetEndPoints();
            var server = World.DatastoreConnection.GetServer(ep.First().ToString());
            server.FlushDatabase(15);
        }
        catch (Exception) { }
    }

    public User CreateUser(string username)
    {
        var user = new User
        {
            Name = username,
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
        user.AuthInfo.Save();
        user.Nation = Game.World.DefaultNation;

        var vault = new Vault(user.Guid);
        vault.Save();
        var parcelStore = new ParcelStore(user.Guid);
        parcelStore.Save();
        user.Save();
        return user;
    }

    public void ResetTestUserStats() => ResetUserStats(TestUser);
    public void ResetSerializableUserStats() => ResetUserStats(SerializableUser);

    private void ResetUserStats(User user)
    {
        user.Stats = new StatInfo
        {
            BaseInt = 3,
            BaseStr = 3,
            BaseDex = 3,
            BaseCon = 3,
            BaseWis = 3,
            Level = 1,
            Gold = 1000,
            BaseHp = 1000,
            BaseMp = 1000,
            Experience = 1000,
            BaseAc = 100
        };
        user.Stats.Hp = 1000;
        user.Stats.Mp = 1000;
        user.Class = Class.Peasant;
        user.Inventory.Clear();
        TestUser.Equipment.Clear();
        user.Vault.Clear();
        user.RemoveAllStatuses();
    }
}