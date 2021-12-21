using Hybrasyl;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace HybrasylTests
{

    public static class Game
    {
        public static readonly World World;
        static Game()
        {
            World = new World(1337, new DataStore() { Host = "127.0.0.1", Port = 6379 })
            {
                DataDirectory = Directory.GetCurrentDirectory()
            };
        }
    }

    public static class Fixtures
    {
        public static readonly Hybrasyl.Map Map;
        public static readonly User TestUser;

        static Fixtures()
        {
            Hybrasyl.Game.World = new World(1337, new DataStore() { Host = "127.0.0.1", Port = 6379 });

            var xmlMap = new Hybrasyl.Xml.Map
            {
                Id = 1000,
                X = 50,
                Y = 50,
                Warps = new List<Hybrasyl.Xml.Warp>(),
                Npcs = new List<MapNpc>(),
                Reactors = new List<Hybrasyl.Xml.Reactor>(),
                Signs = new List<MapSign>()
            };
            Map = new Hybrasyl.Map(xmlMap, Game.World);
            Game.World.WorldData.SetWithIndex(Map.Id, Map, Map.Name);

            var xmlNation = new Nation() { Default = true, Description = "Test Nation", Flag = 0, Name = "Test", SpawnPoints = new List<SpawnPoint>() };
            xmlNation.SpawnPoints.Add(new SpawnPoint() { MapName = "Test Map", X = 5, Y = 5 });
            Game.World.WorldData.Set(xmlNation.Name, xmlNation);

            TestUser = new User();

            //TODO: fix
            

            TestUser.Name = "TestUser";
            TestUser.Uuid = Guid.NewGuid().ToString();
            TestUser.Gender = Gender.Female;
            TestUser.Location.Direction = Direction.South;
            TestUser.Location.Map = Map;
            TestUser.Location.X = 10;
            TestUser.Location.Y = 10;
            TestUser.HairColor = 1;
            TestUser.HairStyle = 1;
            TestUser.Class = Class.Peasant;
            TestUser.Gold = 0;
            TestUser.AuthInfo.CreatedTime = DateTime.Now;
            TestUser.AuthInfo.FirstLogin = true;
            TestUser.AuthInfo.PasswordHash = "testing";
            TestUser.AuthInfo.LastPasswordChange = DateTime.Now;
            TestUser.AuthInfo.LastPasswordChangeFrom = "TestFixture";
            TestUser.AuthInfo.Save();
            TestUser.Nation = Game.World.DefaultNation;

            IDatabase cache = World.DatastoreConnection.GetDatabase();
            cache.Set(User.GetStorageKey(TestUser.Name), TestUser);
            var vault = new Vault(TestUser.Uuid);
            vault.Save();
            var parcelStore = new ParcelStore(TestUser.Uuid);
            parcelStore.Save();
        }

    }

    public class InventoryTestData : IEnumerable<object[]>
    {
        private static readonly Item TestItem;
        private static readonly Item StackableTestItem;


        public static ItemObject TestItemObject => new(TestItem.Id, Game.World);
        public static ItemObject StackableTestItemObject => new(StackableTestItem.Id, Game.World);
        public static readonly Dictionary<EquipmentSlot, ItemObject> TestEquipment = new Dictionary<EquipmentSlot, ItemObject>();
        public static int InventorySize => 59;

        static InventoryTestData()
        {
            TestItem = new Item
            {
                Name = "Test Item"
            };
            TestItem.Properties.Stackable.Max = 1;
            TestItem.Properties.Equipment = new Equipment() { WeaponType = WeaponType.None, Slot = EquipmentSlot.None };
            TestItem.Properties.Physical = new Physical() { Durability = 1000, Weight = 10 };
            Game.World.WorldData.Set(TestItem.Id, TestItem);

            StackableTestItem = new Item
            {
                Name = "Stackable Test Item"
            };
            StackableTestItem.Properties.Stackable.Max = 20;
            StackableTestItem.Properties.Equipment = new Equipment() { WeaponType = WeaponType.None, Slot = EquipmentSlot.None };
            StackableTestItem.Properties.Physical = new Physical() { Durability = 1000, Weight = 10 };
            Game.World.WorldData.Set(StackableTestItem.Id, StackableTestItem);

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var item = new Item() { Name = $"Equip Test {slot}" };
                item.Properties.Stackable.Max = 1;
                item.Properties.Equipment = new Equipment { WeaponType = slot == EquipmentSlot.Weapon ? WeaponType.Dagger : WeaponType.None, Slot = slot };
                item.Properties.Physical = new Physical() { Durability = 1000, Weight = 10 };
                Game.World.WorldData.Set(item.Id, item);
                TestEquipment[slot] = new ItemObject(item.Id, Game.World);
            }
        }

        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { TestItemObject };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
