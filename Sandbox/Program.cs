using System;
using Hybrasyl.Xml.Creature;
using XmlMap = Hybrasyl.Xml.Map.Map;
using Hybrasyl.Xml.Loot;
using Hybrasyl.Xml.Common;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            // Load one map

            XmlMap newMap = XmlMap.LoadFromFile("C:\\Users\\justin.baugh\\Documents\\Hybrasyl\\world\\xml\\maps\\Crypt1-1.xml");

            LootSet ls = LootSet.LoadFromFile("C:\\Users\\justin.baugh\\Documents\\Hybrasyl\\world\\xml\\lootsets\\Crypt.xml");

            SpawnGroup f = SpawnGroup.LoadFromFile("C:\\Users\\justin.baugh\\Documents\\Hybrasyl\\world\\xml\\spawngroups\\Crypt_1.xml");

            LootTable gfy = null;

            Console.WriteLine("HELLO I LOADED A THING");

            var creature = new Creature();
            creature.Name = "Butt";
            creature.Sprite = 69;
            creature.Description = "A big ol fuckin butt";

            var spawnGroup = new SpawnGroup();
            var creatureMap = new Map();
            creatureMap.Name = "Crypt 1-1";
            creatureMap.MaxSpawn = 5;
            creatureMap.MinSpawn = 1;

            spawnGroup.Maps.Add(creatureMap);

            var spawn = new Spawn();
            spawn.Base = "Butt";
            spawn.Damage.Min = 50;
            spawn.Damage.Max = 100;
            spawn.Damage.Hit = 3;
            spawn.Loot.Gold = new LootGold { Max = 333, Min = 111 };
            spawn.Loot.Xp = 3333;

            var table = new LootTable();
            var tableList = new LootTableItemList();

            tableList.Chance = 0.25;

            var lootItem = new LootItem();
            lootItem.Value = "Heiler's Drug Money";
            lootItem.Unique = true;

            tableList.Item.Add(lootItem);

            table.Items = new System.Collections.Generic.List<LootTableItemList>();
            table.Items.Add(tableList);

            StreamReader streamReader = null;
            MemoryStream memoryStream = null;
            var Serializer = new XmlSerializerFactory().CreateSerializer(typeof(Spawn));

            memoryStream = new MemoryStream();
            System.Xml.XmlWriterSettings xmlWriterSettings = new System.Xml.XmlWriterSettings();
            xmlWriterSettings.Indent = true;
            xmlWriterSettings.IndentChars = "  ";
            System.Xml.XmlWriter xmlWriter = XmlWriter.Create(memoryStream, xmlWriterSettings);
            Serializer.Serialize(xmlWriter, spawn);
            memoryStream.Seek(0, SeekOrigin.Begin);
            streamReader = new StreamReader(memoryStream);
            System.Console.WriteLine(streamReader.ReadToEnd());

        }
    }
}
