/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using Hybrasyl.Config;
using Hybrasyl.Creatures;
using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Items;
using Hybrasyl.Nations;
using Hybrasyl.Objects;
using Hybrasyl.XML;
using log4net;
using log4net.Core;
using Microsoft.Scripting.Utils;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Xml;
using System.Xml.Schema;
using Castable = Hybrasyl.Castables.Castable;
using Creature = Hybrasyl.Objects.Creature;

namespace Hybrasyl
{
    public static class SampleStackExchangeRedisExtensions
    {
        public static T Get<T>(this IDatabase cache, string key)
        {
            return Deserialize<T>(cache.StringGet(key));
        }

        public static object Get(this IDatabase cache, string key)
        {
            return Deserialize<object>(cache.StringGet(key));
        }

        public static void Set(this IDatabase cache, string key, object value)
        {
            cache.StringSet(key, Serialize(value));
        }

        private static byte[] Serialize(object o)
        {
            if (o == null)
            {
                return null;
            }

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                binaryFormatter.Serialize(memoryStream, o);
                byte[] objectDataAsStream = memoryStream.ToArray();
                return objectDataAsStream;
            }
        }

        private static T Deserialize<T>(byte[] stream)
        {
            if (stream == null)
            {
                return default(T);
            }

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using (MemoryStream memoryStream = new MemoryStream(stream))
            {
                T result = (T)binaryFormatter.Deserialize(memoryStream);
                return result;
            }
        }
    }

    public class World : Server
    {
        private static uint worldObjectID = 0;

        public static DateTime StartDate
            => Game.Config.Time.StartDate != null ? (DateTime)Game.Config.Time.StartDate : Game.StartDate;

        public new static ILog Logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Dictionary<uint, WorldObject> Objects { get; set; }

        public Dictionary<ushort, Map> Maps { get; set; }
        public Dictionary<string, WorldMap> WorldMaps { get; set; }
        public static Dictionary<int, Item> Items { get; set; }
        public Dictionary<string, Items.VariantGroup> ItemVariants { get; set; }
        public Dictionary<int, Castables.Castable> Skills { get; set; }
        public Dictionary<int, Castables.Castable> Spells { get; set; }
        public Dictionary<int, MonsterTemplate> Monsters { get; set; }
        public Dictionary<int, MerchantTemplate> Merchants { get; set; }
        public Dictionary<int, ReactorTemplate> Reactors { get; set; }
        public Dictionary<string, string> Portraits { get; set; }
        public Dictionary<string, MethodInfo> Methods { get; set; }
        public Dictionary<string, User> Users { get; set; }
        public Dictionary<Int64, MapPoint> MapPoints { get; set; }
        public Dictionary<string, CompiledMetafile> Metafiles { get; set; }
        public Dictionary<string, Nation> Nations { get; set; }
        public Dictionary<string, Mailbox> Mailboxes { get; set; }
        public Dictionary<int, Board> MessageboardIndex { get; set; }
        public Dictionary<string, Board> Messageboards { get; set; }
        public Dictionary<string, Creatures.Creature> Creatures { get; set; }
        public Dictionary<int, SpawnGroup> SpawnGroups { get; set; }

        public Nation DefaultNation
        {
            get
            {
                var nation = Nations.Values.FirstOrDefault(n => n.Default);
                return nation ?? Nations.Values.First();
            }
        }

        public List<DialogSequence> GlobalSequences { get; set; }
        public Dictionary<String, DialogSequence> GlobalSequencesCatalog { get; set; }
        private Dictionary<MerchantMenuItem, MerchantMenuHandler> merchantMenuHandlers;

        public Dictionary<Tuple<Sex, String>, Item> ItemCatalog { get; set; }
        public Dictionary<String, Map> MapCatalog { get; set; }

        public HybrasylScriptProcessor ScriptProcessor { get; set; }

        public static BlockingCollection<HybrasylMessage> MessageQueue;
        public static ConcurrentDictionary<long, User> ActiveUsers { get; private set; }
        public ConcurrentDictionary<string, long> ActiveUsersByName { get; set; }

        private Thread ConsumerThread { get; set; }

        public Login Login { get; private set; }

        private static Lazy<ConnectionMultiplexer> _lazyConnector;

        public static ConnectionMultiplexer DatastoreConnection => _lazyConnector.Value;

        public static string DataDirectory => Constants.DataDirectory;

        public static string MapFileDirectory => Path.Combine(DataDirectory, "world", "mapfiles");

        public static string ScriptDirectory => Path.Combine(DataDirectory, "world", "scripts");

        public static string CastableDirectory => Path.Combine(DataDirectory, "world", "xml", "castables");

        public static string ItemDirectory => Path.Combine(DataDirectory, "world", "xml", "items");

        public static string NationDirectory => Path.Combine(DataDirectory, "world", "xml", "nations");

        public static string MapDirectory => Path.Combine(DataDirectory, "world", "xml", "maps");

        public static string WorldMapDirectory => Path.Combine(DataDirectory, "world", "xml", "worldmaps");

        public static string CreatureDirectory => Path.Combine(DataDirectory, "world", "xml", "creatures");

        public static string SpawnGroupDirectory => Path.Combine(DataDirectory, "world", "xml", "spawngroups");

        public static string ItemVariantDirectory => Path.Combine(DataDirectory, "world", "xml", "itemvariants");

        public static bool TryGetUser(string name, out User userobj)
        {
            var jsonString = (string)DatastoreConnection.GetDatabase().Get(User.GetStorageKey(name));
            if (jsonString == null)
            {
                userobj = null;
                return false;
            }
            userobj = JsonConvert.DeserializeObject<User>(jsonString);
            if (userobj == null)
            {
                Logger.FatalFormat("{0}: JSON object could not be deserialized!", name);
                return false;
            }

            return true;
        }

        public World(int port, DataStore store)
            : base(port)
        {
            Maps = new Dictionary<ushort, Map>();
            WorldMaps = new Dictionary<string, WorldMap>();
            Items = new Dictionary<int, Item>();
            Skills = new Dictionary<int, Castables.Castable>();
            Spells = new Dictionary<int, Castables.Castable>();
            Creatures = new Dictionary<string, Creatures.Creature>();
            SpawnGroups = new Dictionary<int, SpawnGroup>();
            Merchants = new Dictionary<int, MerchantTemplate>();
            Methods = new Dictionary<string, MethodInfo>();
            Objects = new Dictionary<uint, WorldObject>();
            Users = new Dictionary<string, User>(StringComparer.CurrentCultureIgnoreCase);
            MapPoints = new Dictionary<Int64, MapPoint>();
            Metafiles = new Dictionary<string, CompiledMetafile>();
            Nations = new Dictionary<string, Nation>();
            Portraits = new Dictionary<string, string>();
            GlobalSequences = new List<DialogSequence>();
            ItemVariants = new Dictionary<string, Items.VariantGroup>();
            Mailboxes = new Dictionary<string, Mailbox>();
            Messageboards = new Dictionary<string, Board>();
            MessageboardIndex = new Dictionary<int, Board>();

            GlobalSequencesCatalog = new Dictionary<String, DialogSequence>();
            ItemCatalog = new Dictionary<Tuple<Sex, String>, Item>();
            MapCatalog = new Dictionary<String, Map>();

            ScriptProcessor = new HybrasylScriptProcessor(this);
            MessageQueue = new BlockingCollection<HybrasylMessage>(new ConcurrentQueue<HybrasylMessage>());
            ActiveUsers = new ConcurrentDictionary<long, User>();
            ActiveUsersByName = new ConcurrentDictionary<string, long>();

            var datastoreConfig = new ConfigurationOptions()
            {
                EndPoints =
                {
                    {store.Host, store.Port}
                }
            };

            if (!String.IsNullOrEmpty(store.Password))
                datastoreConfig.Password = store.Password;

            _lazyConnector = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(datastoreConfig));
        }

        public bool InitWorld()
        {
            if (!LoadData())
            {
                Logger.Fatal("There were errors loading basic world data. Hybrasyl has halted.");
                Logger.Fatal("Please fix the errors and try to restart the server again.");
                return false;
            }
            CompileScripts();
            LoadMetafiles();
            SetPacketHandlers();
            SetControlMessageHandlers();
            SetMerchantMenuHandlers();
            Logger.InfoFormat("Hybrasyl server ready");
            return true;
        }

        internal void RegisterGlobalSequence(DialogSequence sequence)
        {
            sequence.Id = (uint)GlobalSequences.Count();
            GlobalSequences.Add(sequence);
            GlobalSequencesCatalog.Add(sequence.Name, sequence);
        }

        public bool PlayerExists(string name)
        {
            var redis = DatastoreConnection.GetDatabase();
            return redis.KeyExists(User.GetStorageKey(name));
        }

        private bool LoadData()
        {
            // You'll notice some inconsistencies here in that we use both wrapper classes and
            // native XML classes for Hybrasyl objects. This is unfortunate and should be
            // refactored later, but it is way too much work to do now (e.g. maps, etc).

            // Load maps
            foreach (var xml in Directory.GetFiles(MapDirectory))
            {
                try
                {
                    Maps.Map newMap = Serializer.Deserialize(XmlReader.Create(xml), new Maps.Map());
                    var map = new Map(newMap, this);
                    Maps.Add(map.Id, map);
                    MapCatalog.Add(map.Name, map);
                    Logger.DebugFormat("Maps: Loaded {0}", map.Name);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            Logger.InfoFormat("Maps: {0} maps loaded", Maps.Count);

            // Load nations
            foreach (var xml in Directory.GetFiles(NationDirectory))
            {
                try
                {
                    Nation newNation = Serializer.Deserialize(XmlReader.Create(xml), new Nation());
                    Logger.DebugFormat("Nations: Loaded {0}", newNation.Name);
                    Nations.Add(newNation.Name, newNation);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            // Ensure at least one nation and one map exist. Otherwise, things get a little weird
            if (Nations.Count == 0)
            {
                Logger.FatalFormat("National data: at least one well-formed nation file must exist!");
                return false;
            }

            if (Maps.Count == 0)
            {
                Logger.FatalFormat("Map data: at least one well-formed map file must exist!");
                return false;
            }

            Logger.InfoFormat("National data: {0} nations loaded", Nations.Count);

            //Load Creatures
            foreach (var xml in Directory.GetFiles(CreatureDirectory))
            {
                try
                {
                    var creature = Serializer.Deserialize(XmlReader.Create(xml), new Creatures.Creature());
                    Logger.DebugFormat("Creatures: loaded {0}", creature.Name);
                    Creatures.Add(creature.Name, creature);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }
            //Load SpawnGroups
            foreach (var xml in Directory.GetFiles(SpawnGroupDirectory))
            {
                try
                {
                    var spawnGroup = Serializer.Deserialize(XmlReader.Create(xml), new SpawnGroup());
                    Logger.DebugFormat("SpawnGroup: loaded {0}", spawnGroup.GetHashCode());
                    SpawnGroups.Add(spawnGroup.GetHashCode(), spawnGroup);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            // Load worldmaps
            foreach (var xml in Directory.GetFiles(WorldMapDirectory))
            {
                try
                {
                    Maps.WorldMap newWorldMap = Serializer.Deserialize(XmlReader.Create(xml), new Maps.WorldMap());
                    var worldmap = new WorldMap(newWorldMap);
                    WorldMaps.Add(worldmap.Name, worldmap);
                    foreach (var point in worldmap.Points)
                        MapPoints.Add(point.Id, point);
                    Logger.DebugFormat("World Maps: Loaded {0}", worldmap.Name);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            Logger.InfoFormat("World Maps: {0} world maps loaded", WorldMaps.Count);

            // Load item variants
            foreach (var xml in Directory.GetFiles(ItemVariantDirectory))
            {
                try
                {
                    Items.VariantGroup newGroup = Serializer.Deserialize(XmlReader.Create(xml), new Items.VariantGroup());
                    Logger.DebugFormat("Item variants: loaded {0}", newGroup.Name);
                    ItemVariants.Add(newGroup.Name, newGroup);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            Logger.InfoFormat("ItemObject variants: {0} variant sets loaded", ItemVariants.Count);

            // Load items
            foreach (var xml in Directory.GetFiles(ItemDirectory))
            {
                try
                {
                    Item newItem = Serializer.Deserialize(XmlReader.Create(xml), new Item());
                    Logger.DebugFormat("Items: loaded {0}, id {1}", newItem.Name, newItem.Id);
                    Items.Add(newItem.Id, newItem);
                    ItemCatalog.Add(new Tuple<Sex, string>(Sex.Neutral, newItem.Name), newItem);
                    foreach (var targetGroup in newItem.Properties.Variants.Group)
                    {
                        foreach (var variant in ItemVariants[targetGroup].Variant)
                        {
                            var variantItem = ResolveVariant(newItem, variant, targetGroup);
                            //variantItem.Name = $"{variant.Name} {newItem.Name}";
                            Logger.DebugFormat("ItemObject {0}: variantgroup {1}, subvariant {2}", variantItem.Name, targetGroup, variant.Name);
                            if (Items.ContainsKey(variantItem.Id)) Logger.ErrorFormat("Item already exists with Key {0} : {1}. Cannot add {2}", variantItem.Id, Items[variantItem.Id].Name, variantItem.Name);
                            Items.Add(variantItem.Id, variantItem);
                            ItemCatalog.Add(new Tuple<Sex, string>(Sex.Neutral, variantItem.Name), variantItem);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            foreach (var xml in Directory.GetFiles(CastableDirectory))
            {
                try
                {
                    string name = string.Empty;
                    Castables.Castable newCastable = Serializer.Deserialize(XmlReader.Create(xml), new Castables.Castable());
                    if (newCastable.Book == Castables.Book.PrimarySkill || newCastable.Book == Castables.Book.SecondarySkill ||
                        newCastable.Book == Castables.Book.UtilitySkill)
                    {
                        Skills.Add(newCastable.Id, newCastable);
                    }
                    else
                        Spells.Add(newCastable.Id, newCastable);

                    Logger.DebugFormat("Castables: loaded {0}, id {1}", newCastable.Name, newCastable.Id);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            // Load data from Redis
            // Load mailboxes
            var server = World.DatastoreConnection.GetServer(World.DatastoreConnection.GetEndPoints()[0]);
            foreach (var key in server.Keys(pattern: "Hybrasyl.Mailbox*"))
            {
                Logger.InfoFormat("Loading mailbox at {0}", key);
                var jsonString = (string)World.DatastoreConnection.GetDatabase().Get(key);
                var mailbox = JsonConvert.DeserializeObject<Mailbox>(jsonString);
                var name = key.ToString().Split(':')[1].ToLower();
                if (name == string.Empty)
                {
                    Logger.Warn("Potentially corrupt mailbox data in Redis; ignoring");
                    continue;
                }
                Mailboxes.Add(name, mailbox);
            }

            // Load all boards
            foreach (var key in server.Keys(pattern: "Hybrasyl.Board*"))
            {
                Logger.InfoFormat("Loading board at {0}", key);
                var jsonString = (string)World.DatastoreConnection.GetDatabase().Get(key);
                var messageboard = JsonConvert.DeserializeObject<Board>(jsonString);
                var name = key.ToString().Split(':')[1];
                if (name == string.Empty)
                {
                    Logger.Warn("Potentially corrupt board data in Redis; ignoring");
                    continue;
                }
                // Messageboard IDs are fairly irrelevant and only matter to the client
                messageboard.Id = Messageboards.Count + 1;
                Messageboards.Add(messageboard.Name, messageboard);
                MessageboardIndex.Add(messageboard.Id, messageboard);
            }

            // Ensure global boards exist and are up to date with anything specified in the config

            foreach (var globalboard in Game.Config.Boards)
            {
                var board = GetBoard(globalboard.Name);
                board.DisplayName = globalboard.DisplayName;
                foreach (var reader in globalboard.AccessList.Read)
                {
                    board.SetAccessLevel(Convert.ToString(reader), BoardAccessLevel.Read);
                }
                foreach (var writer in globalboard.AccessList.Write)
                {
                    board.SetAccessLevel(Convert.ToString(writer), BoardAccessLevel.Write);
                }
                foreach (var moderator in globalboard.AccessList.Moderate)
                {
                    board.SetAccessLevel(Convert.ToString(moderator), BoardAccessLevel.Moderate);
                }
                Logger.InfoFormat("Boards: Global board {0} initialized", globalboard.Name);
                board.Save();
            }
            return true;
        }

        public Item ResolveVariant(Item item, Items.Variant variant, string variantGroup)
        {
            var variantItem = item.Clone();

            variantItem.Name = variant.Modifier + " " + item.Name;
            variantItem.Properties.Flags = variant.Properties.Flags;
            variantItem.Properties.Physical.Value = variant.Properties.Physical.Value == 100 ? item.Properties.Physical.Value : Convert.ToUInt32(Math.Round(item.Properties.Physical.Value * (variant.Properties.Physical.Value * .01)));
            variantItem.Properties.Physical.Durability = variant.Properties.Physical.Durability == 100 ? item.Properties.Physical.Durability : Convert.ToUInt32(Math.Round(item.Properties.Physical.Durability * (variant.Properties.Physical.Durability * .01)));
            variantItem.Properties.Physical.Weight = variant.Properties.Physical.Weight == 100 ? item.Properties.Physical.Weight : Convert.ToInt32(Math.Round(item.Properties.Physical.Weight * (variant.Properties.Physical.Weight * .01)));

            switch (variantGroup)
            {
                case "consecratable":
                    {
                        variantItem.Properties.Restrictions.Level.Min += variant.Properties.Restrictions.Level.Min;
                        variantItem.Properties.StatEffects.Base.Dex += variant.Properties.StatEffects.Base.Dex;
                        variantItem.Properties.StatEffects.Base.Con += variant.Properties.StatEffects.Base.Con;
                        variantItem.Properties.StatEffects.Base.Str += variant.Properties.StatEffects.Base.Str;
                        variantItem.Properties.StatEffects.Base.Wis += variant.Properties.StatEffects.Base.Wis;
                        variantItem.Properties.StatEffects.Base.Int += variant.Properties.StatEffects.Base.Int;
                        break;
                    }
                case "elemental":
                    {
                        variantItem.Properties.StatEffects.Element.Offense = variant.Properties.StatEffects.Element.Offense;
                        variantItem.Properties.StatEffects.Element.Defense = variant.Properties.StatEffects.Element.Defense;
                        break;
                    }
                case "enchantable":
                    {
                        variantItem.Properties.Restrictions.Level.Min += variant.Properties.Restrictions.Level.Min;
                        variantItem.Properties.StatEffects.Combat.Ac = (sbyte)(item.Properties.StatEffects.Combat.Ac + variant.Properties.StatEffects.Combat.Ac);
                        variantItem.Properties.StatEffects.Combat.Dmg += variant.Properties.StatEffects.Combat.Dmg;
                        variantItem.Properties.StatEffects.Combat.Hit += variant.Properties.StatEffects.Combat.Hit;
                        variantItem.Properties.StatEffects.Combat.Mr += variant.Properties.StatEffects.Combat.Mr;
                        variantItem.Properties.StatEffects.Combat.Regen += variant.Properties.StatEffects.Combat.Regen;
                        variantItem.Properties.StatEffects.Base.Dex += variant.Properties.StatEffects.Base.Dex;
                        variantItem.Properties.StatEffects.Base.Str += variant.Properties.StatEffects.Base.Str;
                        variantItem.Properties.StatEffects.Base.Wis += variant.Properties.StatEffects.Base.Wis;
                        variantItem.Properties.StatEffects.Base.Con += variant.Properties.StatEffects.Base.Con;
                        variantItem.Properties.StatEffects.Base.Int += variant.Properties.StatEffects.Base.Int;
                        variantItem.Properties.StatEffects.Base.Hp += variant.Properties.StatEffects.Base.Hp;
                        variantItem.Properties.StatEffects.Base.Mp += variant.Properties.StatEffects.Base.Mp;
                        break;
                    }
                case "smithable":
                    {
                        variantItem.Properties.Restrictions.Level.Min += variant.Properties.Restrictions.Level.Min;
                        variantItem.Properties.Damage.Large.Min = Convert.ToUInt16(Math.Round(item.Properties.Damage.Large.Min * (variant.Properties.Damage.Large.Min * .01)));
                        variantItem.Properties.Damage.Large.Max = Convert.ToUInt16(Math.Round(item.Properties.Damage.Large.Max * (variant.Properties.Damage.Large.Max * .01)));
                        variantItem.Properties.Damage.Small.Min = Convert.ToUInt16(Math.Round(item.Properties.Damage.Small.Min * (variant.Properties.Damage.Small.Min * .01)));
                        variantItem.Properties.Damage.Small.Max = Convert.ToUInt16(Math.Round(item.Properties.Damage.Small.Max * (variant.Properties.Damage.Small.Max * .01)));
                        break;
                    }
                case "tailorable":
                    {
                        variantItem.Properties.Restrictions.Level.Min += variant.Properties.Restrictions.Level.Min;
                        variantItem.Properties.StatEffects.Combat.Ac = (sbyte)(item.Properties.StatEffects.Combat.Ac + variant.Properties.StatEffects.Combat.Ac);
                        variantItem.Properties.StatEffects.Combat.Dmg += variant.Properties.StatEffects.Combat.Dmg;
                        variantItem.Properties.StatEffects.Combat.Hit += variant.Properties.StatEffects.Combat.Hit;
                        variantItem.Properties.StatEffects.Combat.Mr += variant.Properties.StatEffects.Combat.Mr;
                        variantItem.Properties.StatEffects.Combat.Regen += variant.Properties.StatEffects.Combat.Regen;
                        break;
                    }
                default:
                    break;
            }
            return variantItem;
        }

        /*End ItemVariants*/

        public Mailbox GetMailbox(string name)
        {
            var mailboxName = name.ToLower();
            if (Mailboxes.ContainsKey(mailboxName)) return Mailboxes[mailboxName];
            Mailboxes.Add(mailboxName, new Mailbox(mailboxName));
            Mailboxes[mailboxName].Save();
            Logger.InfoFormat("Mailbox: Creating mailbox for {0}", name);
            return Mailboxes[mailboxName];
        }

        public Board GetBoard(string name)
        {
            if (Messageboards.ContainsKey(name)) return Messageboards[name];
            var newBoard = new Board(name) { Id = MessageboardIndex.Count + 1 };
            Messageboards.Add(name, new Board(name));
            MessageboardIndex.Add(newBoard.Id, newBoard);
            Messageboards[name].Save();
            Logger.InfoFormat("Board: Creating {0}", name);
            return Messageboards[name];
        }

        private static void ValidationCallBack(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Warning)
                Logger.WarnFormat("XML warning: {0}", args.Message);
            else
                Logger.ErrorFormat("XML ERROR: {0}", args.Message);
        }

        private void LoadMetafiles()
        {
            // these might be better suited in LoadData as the database is being read, but only items are in database atm

            #region ItemInfo

            var iteminfo0 = new Metafile("ItemInfo0");
            // TODO: split items into multiple ItemInfo files (DA does ~700 each)
            foreach (var item in Items.Values)
            {
                iteminfo0.Nodes.Add(new MetafileNode(item.Name, item.Properties.Restrictions.Level.Min, (int)item.Properties.Restrictions.@Class, item.Properties.Physical.Weight,
                    item.Properties.Vendor.ShopTab, item.Properties.Vendor.Description));
            }
            Metafiles.Add(iteminfo0.Name, iteminfo0.Compile());

            #endregion ItemInfo

            #region SClass

            for (int i = 1; i <= 5; ++i)
            {
                var sclass = new Metafile("SClass" + i);
                sclass.Nodes.Add("Skill");
                foreach (var skill in Skills.Values)
                // placeholder; change to skills where class == i, are learnable from trainer, and sort by level
                {
                    sclass.Nodes.Add(new MetafileNode(skill.Name,
                        string.Format("{0}/{1}/{2}", 0, 0, 0), // req level, master (0/1), req ab
                        string.Format("{0}/{1}/{2}", 0, 0, 0), // skill icon, x position (defunct), y position (defunct)
                        string.Format("{0}/{1}/{2}/{3}/{4}", 3, 3, 3, 3, 3),
                        // str, dex, int, wis, con (not a typo, dex after str)
                        string.Format("{0}/{1}", 0, 0), // req skill 1 (skill name or 0 for none), req skill 1 level
                        string.Format("{0}/{1}", 0, 0) // req skill 2 (skill name or 0 for none), req skill 2 level
                        ));
                }
                sclass.Nodes.Add("Skill_End");
                sclass.Nodes.Add("Spell");
                foreach (var spell in Spells.Values)
                // placeholder; change to skills where class == i, are learnable from trainer, and sort by level
                {
                    sclass.Nodes.Add(new MetafileNode(spell.Name,
                        string.Format("{0}/{1}/{2}", 0, 0, 0), // req level, master (0/1), req ab
                        string.Format("{0}/{1}/{2}", 0, 0, 0), // spell icon, x position (defunct), y position (defunct)
                        string.Format("{0}/{1}/{2}/{3}/{4}", 3, 3, 3, 3, 3),
                        // str, dex, int, wis, con (not a typo, dex after str)
                        string.Format("{0}/{1}", 0, 0), // req spell 1 (spell name or 0 for none), req spell 1 level
                        string.Format("{0}/{1}", 0, 0) // req spell 2 (spell name or 0 for none), req spell 2 level
                        ));
                }
                sclass.Nodes.Add("Spell_End");
                Metafiles.Add(sclass.Name, sclass.Compile());
            }

            #endregion SClass

            #region NPCIllust

            var npcillust = new Metafile("NPCIllust");
            foreach (var kvp in Portraits) // change to merchants that have a portrait rather than all
            {
                npcillust.Nodes.Add(new MetafileNode(kvp.Key, kvp.Value /* portrait filename */));
            }
            Metafiles.Add(npcillust.Name, npcillust.Compile());

            #endregion NPCIllust

            #region NationDesc

            var nationdesc = new Metafile("NationDesc");
            foreach (var nation in Nations.Values)
            {
                Logger.DebugFormat("Adding flag {0} for nation {1}", nation.Flag, nation.Name);
                nationdesc.Nodes.Add(new MetafileNode("nation_" + nation.Flag, nation.Name));
            }
            Metafiles.Add(nationdesc.Name, nationdesc.Compile());

            #endregion NationDesc
        }

        public void CompileScripts()
        {
            // Scan each directory for *.py files
            foreach (var dir in Constants.SCRIPT_DIRECTORIES)
            {
                Logger.InfoFormat("Scanning script directory: {0}", dir);
                var directory = Path.Combine(ScriptDirectory, dir);
                if (!Directory.Exists(directory))
                {
                    Logger.ErrorFormat("Scripting directory {0} not found!", dir);
                    continue;
                }

                var filelist = Directory.GetFiles(directory);
                foreach (var file in filelist)
                {
                    try
                    {
                        if (Path.GetExtension(file) == ".py")
                        {
                            var scriptname = Path.GetFileName(file);
                            Logger.InfoFormat("Loading script {0}\\{1}", dir, scriptname);
                            var script = new Script(file, ScriptProcessor);
                            ScriptProcessor.RegisterScript(script);
                            if (dir == "common")
                                script.InstantiateScriptable();
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.ErrorFormat("Script {0}\\{1}: Registration failed: {2}", dir, file, e.ToString());
                    }
                }
            }
        }

        #region Set Handlers

        public void SetControlMessageHandlers()
        {
            ControlMessageHandlers[ControlOpcodes.CleanupUser] = ControlMessage_CleanupUser;
            ControlMessageHandlers[ControlOpcodes.SaveUser] = ControlMessage_SaveUser;
            ControlMessageHandlers[ControlOpcodes.ShutdownServer] = ControlMessage_ShutdownServer;
            ControlMessageHandlers[ControlOpcodes.RegenUser] = ControlMessage_RegenerateUser;
            ControlMessageHandlers[ControlOpcodes.LogoffUser] = ControlMessage_LogoffUser;
            ControlMessageHandlers[ControlOpcodes.MailNotifyUser] = ControlMessage_MailNotifyUser;
            ControlMessageHandlers[ControlOpcodes.StatusTick] = ControlMessage_StatusTick;
            ControlMessageHandlers[ControlOpcodes.MonolithSpawn] = ControlMessage_SpawnMonster;
        }

        public void SetPacketHandlers()
        {
            PacketHandlers[0x05] = PacketHandler_0x05_RequestMap;
            PacketHandlers[0x06] = PacketHandler_0x06_Walk;
            PacketHandlers[0x07] = PacketHandler_0x07_PickupItem;
            PacketHandlers[0x08] = PacketHandler_0x08_DropItem;
            PacketHandlers[0x0B] = PacketHandler_0x0B_ClientExit;
            PacketHandlers[0x0E] = PacketHandler_0x0E_Talk;
            PacketHandlers[0x0F] = PacketHandler_0x0F_UseSpell;
            PacketHandlers[0x10] = PacketHandler_0x10_ClientJoin;
            PacketHandlers[0x11] = PacketHandler_0x11_Turn;
            PacketHandlers[0x13] = PacketHandler_0x13_Attack;
            PacketHandlers[0x18] = PacketHandler_0x18_ShowPlayerList;
            PacketHandlers[0x19] = PacketHandler_0x19_Whisper;
            PacketHandlers[0x1C] = PacketHandler_0x1C_UseItem;
            PacketHandlers[0x1D] = PacketHandler_0x1D_Emote;
            PacketHandlers[0x24] = PacketHandler_0x24_DropGold;
            PacketHandlers[0x29] = PacketHandler_0x29_DropItemOnCreature;
            PacketHandlers[0x2A] = PacketHandler_0x2A_DropGoldOnCreature;
            PacketHandlers[0x2D] = PacketHandler_0x2D_PlayerInfo;
            PacketHandlers[0x2E] = PacketHandler_0x2E_GroupRequest;
            PacketHandlers[0x2F] = PacketHandler_0x2F_GroupToggle;
            PacketHandlers[0x30] = PacketHandler_0x30_MoveUIElement;
            PacketHandlers[0x38] = PacketHandler_0x38_Refresh;
            PacketHandlers[0x39] = PacketHandler_0x39_NPCMainMenu;
            PacketHandlers[0x3A] = PacketHandler_0x3A_DialogUse;
            PacketHandlers[0x3B] = PacketHandler_0x3B_AccessMessages;
            PacketHandlers[0x3E] = PacketHandler_0x3E_UseSkill;
            PacketHandlers[0x3F] = PacketHandler_0x3F_MapPointClick;
            PacketHandlers[0x43] = PacketHandler_0x43_PointClick;
            PacketHandlers[0x44] = PacketHandler_0x44_EquippedItemClick;
            PacketHandlers[0x45] = PacketHandler_0x45_ByteHeartbeat;
            PacketHandlers[0x47] = PacketHandler_0x47_StatPoint;
            PacketHandlers[0x4a] = PacketHandler_0x4A_Trade;
            PacketHandlers[0x4D] = PacketHandler_0x4D_BeginCasting;
            PacketHandlers[0x4E] = PacketHandler_0x4E_CastLine;
            PacketHandlers[0x4F] = PacketHandler_0x4F_ProfileTextPortrait;
            PacketHandlers[0x75] = PacketHandler_0x75_TickHeartbeat;
            PacketHandlers[0x79] = PacketHandler_0x79_Status;
            PacketHandlers[0x7B] = PacketHandler_0x7B_RequestMetafile;
        }

        public void SetMerchantMenuHandlers()
        {
            merchantMenuHandlers = new Dictionary<MerchantMenuItem, MerchantMenuHandler>()
            {
                {MerchantMenuItem.MainMenu, new MerchantMenuHandler(0, MerchantMenuHandler_MainMenu)},
                {
                    MerchantMenuItem.BuyItemMenu,
                    new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItemMenu)
                },
                {MerchantMenuItem.BuyItem, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItem)},
                {
                    MerchantMenuItem.BuyItemQuantity,
                    new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItemWithQuantity)
                },
                {
                    MerchantMenuItem.SellItemMenu,
                    new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemMenu)
                },
                {MerchantMenuItem.SellItem, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItem)},
                {
                    MerchantMenuItem.SellItemQuantity,
                    new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemWithQuantity)
                },
                {
                    MerchantMenuItem.SellItemAccept,
                    new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemConfirmation)
                },
                {
                    MerchantMenuItem.LearnSkillMenu, new MerchantMenuHandler(MerchantJob.Train, MerchantMenuHandler_LearnSkill)
                },
                {
                    MerchantMenuItem.LearnSpellMenu, new MerchantMenuHandler(MerchantJob.Train, MerchantMenuHandler_LearnSpell)
                },
                {
                    MerchantMenuItem.ForgetSkillMenu, new MerchantMenuHandler(MerchantJob.Train, MerchantMenuHandler_ForgetSkill)
                },
                {
                    MerchantMenuItem.ForgetSpellMenu, new MerchantMenuHandler(MerchantJob.Train, MerchantMenuHandler_ForgetSpell)
                },
                {
                    MerchantMenuItem.LearnSkillAccept, new MerchantMenuHandler(MerchantJob.Train, MerchantMenuHandler_LearnSkillAccept)
                },
                {
                    MerchantMenuItem.LearnSpellAccept, new MerchantMenuHandler(MerchantJob.Train, MerchantMenuHandler_LearnSpellAccept)
                },
                {
                    MerchantMenuItem.ForgetSkillAccept, new MerchantMenuHandler(MerchantJob.Train, MerchantMenuHandler_ForgetSkillAccept)
                },
                {
                    MerchantMenuItem.ForgetSpellAccept, new MerchantMenuHandler(MerchantJob.Train, MerchantMenuHandler_ForgetSpellAccept)
                },

            };
        }

        #endregion Set Handlers

        // FIXME: *User here should now use the ConcurrentDictionaries instead
        public void DeleteUser(string username)
        {
            Users.Remove(username);
        }

        public void AddUser(User userobj)
        {
            Users[userobj.Name] = userobj;
        }

        public User FindUser(string username)
        {
            if (Users.ContainsKey(username))
            {
                return Users[username];
            }
            else
            {
                return null;
            }
        }

        public override void Shutdown()
        {
            Logger.WarnFormat("Shutdown initiated, disconnecting {0} active users", ActiveUsers.Count);

            foreach (var connection in ActiveUsers)
            {
                var user = connection.Value;
                user.Logoff();
            }
            Listener.Close();
            Logger.Warn("Shutdown complete");
        }

        #region Control Message Handlers

        private void ControlMessage_CleanupUser(HybrasylControlMessage message)
        {
            // clean up after a broken connection
            var connectionId = (long)message.Arguments[0];
            User user;
            if (ActiveUsers.TryRemove(connectionId, out user))
            {
                Logger.InfoFormat("cid {0}: closed, player {1} removed", connectionId, user.Name);
                if (user.ActiveExchange != null)
                    user.ActiveExchange.CancelExchange(user);
                ((IDictionary)ActiveUsersByName).Remove(user.Name);
                user.Save();
                user.UpdateLogoffTime();
                user.Map.Remove(user);

                if (user.Group != null)
                {
                    user.Group.Remove(user);
                }

                Remove(user);
                Logger.DebugFormat("cid {0}: {1} cleaned up successfully", user.Name);
                DeleteUser(user.Name);
            }
        }

        private void ControlMessage_RegenerateUser(HybrasylControlMessage message)
        {
            // regenerate a user
            // USDA Formula for HP: MAXHP * (0.1 + (CON - Lv) * 0.01) <20% MAXHP
            // USDA Formula for MP: MAXMP * (0.1 + (WIS - Lv) * 0.01) <20% MAXMP
            // Regen = regen * 0.0015 (so 100 regen = 15%)
            User user;
            var connectionId = (long)message.Arguments[0];
            if (ActiveUsers.TryGetValue(connectionId, out user))
            {
                uint hpRegen = 0;
                uint mpRegen = 0;
                double fixedRegenBuff = Math.Min(user.Regen * 0.0015, 0.15);
                fixedRegenBuff = Math.Max(fixedRegenBuff, 0.125);
                if (user.Hp != user.MaximumHp)
                {
                    hpRegen = (uint)Math.Min(user.MaximumHp * (0.1 * Math.Max(user.Con, (user.Con - user.Level)) * 0.01),
                        user.MaximumHp * 0.20);
                    hpRegen = hpRegen + (uint)(fixedRegenBuff * user.MaximumHp);
                }
                if (user.Mp != user.MaximumMp)
                {
                    mpRegen = (uint)Math.Min(user.MaximumMp * (0.1 * Math.Max(user.Int, (user.Int - user.Level)) * 0.01),
                        user.MaximumMp * 0.20);
                    mpRegen = mpRegen + (uint)(fixedRegenBuff * user.MaximumMp);
                }
                Logger.DebugFormat("User {0}: regen HP {1}, MP {2}", user.Name,
                    hpRegen, mpRegen);
                user.Hp = Math.Min(user.Hp + hpRegen, user.MaximumHp);
                user.Mp = Math.Min(user.Mp + mpRegen, user.MaximumMp);
                user.UpdateAttributes(StatUpdateFlags.Current);
            }
        }

        private void ControlMessage_SaveUser(HybrasylControlMessage message)
        {
            // save a user
            User user;
            var connectionId = (long)message.Arguments[0];
            if (ActiveUsers.TryGetValue(connectionId, out user))
            {
                Logger.DebugFormat("Saving user {0}", user.Name);
                user.Save();
            }
            else
            {
                Logger.WarnFormat("Tried to save user associated with connection ID {0} but user doesn't exist",
                    connectionId);
            }
        }

        private void ControlMessage_ShutdownServer(HybrasylControlMessage message)
        {
            // Initiate an orderly shutdown
            var userName = (string)message.Arguments[0];
            Logger.WarnFormat("Server shutdown request initiated by {0}", userName);
            // Chaos is Rising Up, yo.
            foreach (var connection in ActiveUsers)
            {
                var user = connection.Value;
                user.SendMessage("Chaos is rising up. Please re-enter in a few minutes.",
                    Hybrasyl.MessageTypes.SYSTEM_WITH_OVERHEAD);
            }

            // Actually shut down the server. This terminates the listener loop in Game.
            if (Game.IsActive())
                Game.ToggleActive();

            Logger.WarnFormat("Server has begun shutdown");
        }

        private void ControlMessage_LogoffUser(HybrasylControlMessage message)
        {
            // Log off the specified user
            var userName = (string)message.Arguments[0];
            Logger.WarnFormat("{0}: forcing logoff", userName);
            User user;
            if (Users.TryGetValue(userName, out user))
            {
                user.Logoff();
            }
        }

        private void ControlMessage_MailNotifyUser(HybrasylControlMessage message)
        {
            // Set unread mail flag and if the user is online, send them an UpdateAttributes packet
            var userName = (string)message.Arguments[0];
            Logger.DebugFormat("mail: attempting to notify {0} of new mail", userName);
            User user;
            if (Users.TryGetValue(userName, out user))
            {
                user.UpdateAttributes(StatUpdateFlags.Secondary);
                Logger.DebugFormat("mail: notification to {0} sent", userName);
            }
            else
            {
                Logger.DebugFormat("mail: notification to {0} failed, not logged in?", userName);
            }
        }

        private void ControlMessage_StatusTick(HybrasylControlMessage message)
        {
            var userName = (string)message.Arguments[0];
            Logger.DebugFormat("statustick: processing tick for {0}", userName);
            User user;
            if (Users.TryGetValue(userName, out user))
            {
                user.ProcessStatusTicks();
            }
            else
            {
                Logger.DebugFormat("tick: Cannot process tick for {0}, not logged in?", userName);
            }
        }

        private void ControlMessage_SpawnMonster(HybrasylControlMessage message)
        {
            var monster = (Monster)message.Arguments[0];
            var map = (Map)message.Arguments[1];
            Logger.DebugFormat("monolith: spawning monster {0} on map {1}", monster.Name, map.Name);
            map.InsertCreature(monster);
        }

        #endregion Control Message Handlers

        #region Packet Handlers

        private void PacketHandler_0x05_RequestMap(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            int index = 0;

            for (ushort row = 0; row < user.Map.Y; ++row)
            {
                var x3C = new ServerPacket(0x3C);
                x3C.WriteUInt16(row);
                for (int col = 0; col < user.Map.X * 6; col += 2)
                {
                    x3C.WriteByte(user.Map.RawData[index + 1]);
                    x3C.WriteByte(user.Map.RawData[index]);
                    index += 2;
                }
                user.Enqueue(x3C);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [ProhibitedCondition(PlayerCondition.Paralyzed)]
        private void PacketHandler_0x06_Walk(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var direction = packet.ReadByte();
            if (direction > 3) return;
            user.Walk((Direction)direction);
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x07_PickupItem(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();
            var x = packet.ReadInt16();
            var y = packet.ReadInt16();

            //var user = client.User;
            //var map = user.Map;

            // Is the player within PICKUP_DISTANCE tiles of what they're trying to pick up?
            if (Math.Abs(x - user.X) > Constants.PICKUP_DISTANCE ||
                Math.Abs(y - user.Y) > Constants.PICKUP_DISTANCE)
                return;

            // Check if inventory slot is valid and empty
            if (slot == 0 || slot > user.Inventory.Size || user.Inventory[slot] != null)
                return;

            // Find the items that are at the pickup area

            var tile = new Rectangle(x, y, 1, 1);

            // We don't want to pick up people
            var pickupObject = user.Map.EntityTree.GetObjects(tile).FindLast(i => i is Gold || i is ItemObject);

            if (pickupObject == null) return;

            string error;
            if (!pickupObject.CanBeLooted(user.Name, out error))
            {
                user.SendSystemMessage(error);
                return;
            }

            // If the add is successful, remove the item from the map quadtree
            if (pickupObject is Gold)
            {
                var gold = (Gold)pickupObject;
                if (user.AddGold(gold))
                {
                    Logger.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
                        gold.Name, gold.Amount, user.Map.Name, x, y);
                    user.Map.RemoveGold(gold);
                }
            }
            else if (pickupObject is ItemObject)
            {
                var item = (ItemObject)pickupObject;
                if (item.Unique && user.Inventory.Contains(item.TemplateId))
                {
                    user.SendMessage(string.Format("You can't carry any more of those.", item.Name), 3);
                    return;
                }
                if (item.Stackable && user.Inventory.Contains(item.TemplateId))
                {
                    byte existingSlot = user.Inventory.SlotOf(item.TemplateId);
                    var existingItem = user.Inventory[existingSlot];

                    int maxCanGive = existingItem.MaximumStack - existingItem.Count;
                    int quantity = Math.Min(item.Count, maxCanGive);

                    item.Count -= quantity;
                    existingItem.Count += quantity;

                    Logger.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
                        item.Name, item.Count, user.Map.Name, x, y);
                    user.Map.Remove(item);
                    user.SendItemUpdate(existingItem, existingSlot);

                    if (item.Count == 0) Remove(item);
                    else
                    {
                        user.Map.Insert(item, user.X, user.Y);
                        user.SendMessage(string.Format("You can't carry any more {0}.", item.Name), 3);
                    }
                }
                else
                {
                    Logger.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
                        item.Name, item.Count, user.Map.Name, x, y);
                    user.Map.Remove(item);
                    user.AddItem(item, slot);
                }
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x08_DropItem(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();
            var x = packet.ReadInt16();
            var y = packet.ReadInt16();
            var count = packet.ReadInt32();

            Logger.DebugFormat("{0} {1} {2} {3}", slot, x, y, count);

            // Do a few sanity checks
            //
            // Is the distance valid? (Can't drop things beyond
            // MAXIMUM_DROP_DISTANCE tiles away)
            if (Math.Abs(x - user.X) > Constants.PICKUP_DISTANCE ||
                Math.Abs(y - user.Y) > Constants.PICKUP_DISTANCE)
            {
                Logger.ErrorFormat("Request to drop item exceeds maximum distance {0}",
                    Hybrasyl.Constants.MAXIMUM_DROP_DISTANCE);
                return;
            }

            // Is this a valid slot?
            if ((slot == 0) || (slot > Hybrasyl.Constants.MAXIMUM_INVENTORY))
            {
                Logger.ErrorFormat("Slot not valid. Aborting");
                return;
            }

            // Does the player actually have an item in the slot? Does the count in the packet exceed the
            // count in the player's inventory?  Are they trying to drop the item on something that
            // is impassable (i.e. a wall)?
            if ((user.Inventory[slot] == null) || (count > user.Inventory[slot].Count) ||
                (user.Map.IsWall[x, y] == true) || !user.Map.IsValidPoint(x, y))
            {
                Logger.ErrorFormat(
                    "Slot {0} is null, or count {1} exceeds count {2}, or {3},{4} is a wall, or {3},{4} is out of bounds",
                    slot, count, user.Inventory[slot].Count, x, y);
                return;
            }

            ItemObject toDrop = user.Inventory[slot];

            if (toDrop.Stackable && count < toDrop.Count)
            {
                toDrop.Count -= count;
                user.SendItemUpdate(toDrop, slot);

                toDrop = new ItemObject(toDrop);
                toDrop.Count = count;
                Insert(toDrop);
            }
            else
            {
                user.RemoveItem(slot);
            }

            // Are we dropping an item onto a reactor?
            Objects.Reactor reactor;
            var coordinates = new Tuple<byte, byte>((byte)x, (byte)y);
            if (user.Map.Reactors.TryGetValue(coordinates, out reactor))
            {
                reactor.OnDrop(user, toDrop);
            }
            else
                user.Map.AddItem(x, y, toDrop);
        }

        private void PacketHandler_0x0E_Talk(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var isShout = packet.ReadByte();
            var message = packet.ReadString8();

            if (message.StartsWith("/"))
            {
                var args = message.Split(' ');

                #region world's biggest switch statement

                switch (args[0].ToLower())
                {
                    case "/gold":
                        {
                            uint amount;

                            if (args.Length != 2 || !uint.TryParse(args[1], out amount))
                                break;

                            user.Gold = amount;
                            user.UpdateAttributes(StatUpdateFlags.Experience);
                            break;
                        }
                    /**
                     * Give the current user some amount of experience. This experience
                     * will be distributed across a group if the user is in a group, or
                     * passed directly to them if they're not in a group.
                     */
                    case "/hp":
                        {
                            uint hp = 0;
                            if (uint.TryParse(args[1], out hp))
                            {
                                user.Hp = hp;
                                user.UpdateAttributes(StatUpdateFlags.Current);
                            }
                        }
                        break;

                    case "/clearstatus":
                        {
                            user.RemoveAllStatuses();
                            user.Status = PlayerCondition.Alive;
                            user.SendSystemMessage("All statuses cleared.");
                        }
                        break;

                    case "/clearconditions":
                        {
                            user.Status = PlayerCondition.Alive;
                            user.SendSystemMessage("Alive, conditions cleared");
                        }
                        break;

                    case "/applystatus":
                        {
                            var status = args[1];
                            if (status.ToLower() == "poison")
                            {
                                user.ApplyStatus(new PoisonStatus(user, 30, 1, 5));
                            }
                            if (status.ToLower() == "sleep")
                            {
                                user.ApplyStatus(new SleepStatus(user, 30, 1));
                            }
                            if (status.ToLower() == "paralyze")
                            {
                                user.ApplyStatus(new ParalyzeStatus(user, 30, 1));
                            }
                            if (status.ToLower() == "blind")
                            {
                                user.ApplyStatus(new BlindStatus(user, 30, 1));
                            }
                        }
                        break;

                    case "/damage":
                        {
                            var dmg = double.Parse(args[1]);
                            user.Damage(dmg);
                        }
                        break;

                    case "/condition":
                        {
                            user.SendSystemMessage($"Status: {user.Status}");
                        }
                        break;

                    case "/status":
                        {
                            var icon = ushort.Parse(args[1]);

                            user.Enqueue(new ServerPacketStructures.StatusBar { Icon = icon, BarColor = (StatusBarColor)Enum.Parse(typeof(StatusBarColor), args[2]) }.Packet());
                        }
                        break;

                    case "/exp":
                        {
                            uint amount = 0;
                            if (args.Length == 2 && uint.TryParse(args[1], out amount))
                            {
                                user.ShareExperience(amount);
                            }
                        }
                        break;
                    /* Reset a user to level 1, with no level points and no experience. */
                    case "/expreset":
                        {
                            user.LevelPoints = 0;
                            user.Level = 1;
                            user.Experience = 0;
                            user.UpdateAttributes(StatUpdateFlags.Full);
                        }
                        break;

                    case "/group":
                        User newMember = FindUser(args[1]);

                        if (newMember == null)
                        {
                            user.SendMessage("Unknown user in group request.", MessageTypes.SYSTEM);
                            break;
                        }

                        user.InviteToGroup(newMember);
                        break;

                    case "/ungroup":
                        if (user.Group != null)
                        {
                            user.Group.Remove(user);
                        }
                        break;

                    case "/summon":
                        {
                            if (!user.IsPrivileged)
                                return;

                            if (args.Length == 2)
                            {
                                if (!Users.ContainsKey(args[1]))
                                {
                                    user.SendMessage("User not logged in.", MessageTypes.SYSTEM);
                                    return;
                                }
                                var target = Users[args[1]];
                                if (target.IsExempt)
                                    user.SendMessage("Access denied.", MessageTypes.SYSTEM);
                                else
                                {
                                    target.Teleport(user.Map.Id, user.MapX, user.MapY);
                                    Logger.InfoFormat("GM activity: {0} summoned {1}", user.Name, target.Name);
                                }
                            }
                        }
                        break;

                    case "/nation":
                        {
                            if (args.Length == 2)
                            {
                                if (Nations.ContainsKey(args[1]))
                                {
                                    user.Nation = Nations[args[1]];
                                    user.SendSystemMessage($"Citizenship set to {args[1]}");
                                }
                            }
                        }
                        break;

                    case "/kick":
                        {
                            if (!user.IsPrivileged)
                                return;

                            if (args.Length == 2)
                            {
                                if (!Users.ContainsKey(args[1]))
                                {
                                    user.SendMessage("User not logged in.", MessageTypes.SYSTEM);
                                    return;
                                }
                                var target = Users[args[1]];
                                if (target.IsExempt)
                                    user.SendMessage("Access denied.", MessageTypes.SYSTEM);
                                else
                                    target.Logoff();
                                Logger.InfoFormat("GM activity: {0} kicked {1}",
                                    user.Name, target.Name);
                            }
                        }
                        break;

                    case "/teleport":
                        {
                            ushort number = ushort.MaxValue;
                            byte x = user.X, y = user.Y;

                            if (args.Length == 2)
                            {
                                if (!ushort.TryParse(args[1], out number))
                                {
                                    if (!Users.ContainsKey(args[1]))
                                    {
                                        user.SendMessage("Invalid map number or user name", 3);
                                        return;
                                    }
                                    else
                                    {
                                        var target = Users[args[1]];
                                        number = target.Map.Id;
                                        x = target.X;
                                        y = target.Y;
                                    }
                                }
                            }
                            else if (args.Length == 4)
                            {
                                ushort.TryParse(args[1], out number);
                                byte.TryParse(args[2], out x);
                                byte.TryParse(args[3], out y);
                            }

                            if (Maps.ContainsKey(number))
                            {
                                var map = Maps[number];
                                if (x < map.X && y < map.Y) user.Teleport(number, x, y);
                                else user.SendMessage("Invalid x/y", 3);
                            }
                            else user.SendMessage("Invalid map number", 3);
                        }
                        break;

                    case "/motion":
                        {
                            byte motion;
                            short speed = 20;
                            if (args.Length > 1 && byte.TryParse(args[1], out motion))
                            {
                                if (args.Length > 2) short.TryParse(args[2], out speed);
                                user.Motion(motion, speed);
                            }
                        }
                        break;

                    case "/maplist":
                        {
                            // This is an extremely expensive slash command
                            var searchString = "";
                            if (args.Length == 1)
                            {
                                user.SendMessage("Usage:   /maplist <searchterm>\nExample: /maplist Mileth - show maps with Mileth in the title\n",
                                    MessageTypes.SLATE);
                                return;
                            }
                            else if (args.Length == 2)
                                searchString = args[1];
                            else
                                searchString = String.Join(" ", args, 1, args.Length - 1);

                            Regex searchTerm;
                            try
                            {
                                Logger.InfoFormat("Search term was {0}", searchString);
                                searchTerm = new Regex(String.Format("{0}", searchString));
                            }
                            catch
                            {
                                user.SendMessage("Invalid search. Try again or send no options for help.",
                                    MessageTypes.SYSTEM);
                                return;
                            }

                            var queryMaps = from amap in MapCatalog
                                            where searchTerm.IsMatch(amap.Key)
                                            select amap;
                            var result = queryMaps.Aggregate("",
                                (current, map) => current + String.Format("{0} - {1}\n", map.Value.Id, map.Value.Name));

                            if (result.Length > 65400)
                                result = String.Format("{0}\n(Results truncated)", result.Substring(0, 65400));

                            user.SendMessage(String.Format("Search Results\n---------------\n\n{0}",
                                result),
                                MessageTypes.SLATE_WITH_SCROLLBAR);
                        }
                        break;

                    case "/unreadmail":
                        {
                            user.SendSystemMessage(user.UnreadMail ? "Unread mail." : "No unread mail.");
                        }
                        break;

                    case "/effect":
                        {
                            ushort effect;
                            short speed = 100;
                            if (args.Length > 1 && ushort.TryParse(args[1], out effect))
                            {
                                if (args.Length > 2) short.TryParse(args[2], out speed);
                                user.Effect(effect, speed);
                            }
                        }
                        break;

                    case "/sound":
                        {
                            byte sound;
                            if (args.Length > 1 && byte.TryParse(args[1], out sound))
                            {
                                user.SendSound(sound);
                            }
                        }
                        break;

                    case "/music":
                        {
                            byte track;
                            if (args.Length > 1 && byte.TryParse(args[1], out track))
                            {
                                user.Map.Music = track;
                                foreach (var mapuser in user.Map.Users.Values)
                                {
                                    mapuser.SendMusic(track);
                                }
                            }
                        }
                        break;

                    case "/mapmsg":
                        {
                            if (args.Length > 1)
                            {
                                var mapmsg = string.Join(" ", args, 1, args.Length - 1);
                                user.Map.Message = mapmsg;
                                foreach (var mapuser in user.Map.Users.Values)
                                {
                                    mapuser.SendMessage(mapmsg, 18);
                                }
                            }
                        }
                        break;

                    case "/worldmsg":
                        {
                            if (args.Length > 1)
                            {
                                var msg = string.Join(" ", args, 1, args.Length - 1);
                                foreach (var connectedUser in ActiveUsers)
                                {
                                    connectedUser.Value.SendWorldMessage(user.Name, msg);
                                }
                            }
                        }
                        break;

                    case "/class":
                        {
                            var className = string.Join(" ", args, 1, args.Length - 1);
                            int classValue;
                            if (Hybrasyl.Constants.CLASSES.TryGetValue(className, out classValue))
                            {
                                user.Class = (Hybrasyl.Enums.Class)Hybrasyl.Constants.CLASSES[className];
                                user.SendMessage(String.Format("Class set to {0}", className.ToLower()), 0x1);
                            }
                            else
                            {
                                user.SendMessage("I know nothing about that class. Try again.", 0x1);
                            }
                        }
                        break;

                    case "/legend":
                        {
                            var icon = (LegendIcon)Enum.Parse(typeof(LegendIcon), args[1]);
                            var color = (LegendColor)Enum.Parse(typeof(LegendColor), args[2]);
                            var quantity = int.Parse(args[3]);
                            var datetime = DateTime.Parse(args[4]);

                            var legend = string.Join(" ", args, 5, args.Length - 5);
                            user.Legend.AddMark(icon, color, legend, datetime, string.Empty, true, quantity);
                        }
                        break;

                    case "/legendclear":
                        {
                            user.Legend.Clear();
                            user.SendSystemMessage("Legend has been cleared.");
                        }
                        break;

                    case "/level":
                        {
                            byte newLevel;
                            var level = string.Join(" ", args, 1, args.Length - 1);
                            if (!Byte.TryParse(level, out newLevel))
                                user.SendMessage("That's not a valid level, champ.", 0x1);
                            else
                            {
                                user.Level = newLevel > Constants.MAX_LEVEL ? (byte)Constants.MAX_LEVEL : newLevel;
                                user.UpdateAttributes(StatUpdateFlags.Full);
                                user.SendMessage(String.Format("Level changed to {0}", newLevel), 0x1);
                            }
                        }
                        break;

                    case "/attr":
                        {
                            if (args.Length != 3)
                            {
                                return;
                            }
                            byte newStat;
                            if (!Byte.TryParse(args[2], out newStat))
                            {
                                user.SendSystemMessage("That's not a valid value for an attribute, chief.");
                                return;
                            }

                            switch (args[1].ToLower())
                            {
                                case "str":
                                    user.BaseStr = newStat;
                                    break;

                                case "con":
                                    user.BaseCon = newStat;
                                    break;

                                case "dex":
                                    user.BaseDex = newStat;
                                    break;

                                case "wis":
                                    user.BaseWis = newStat;
                                    break;

                                case "int":
                                    user.BaseInt = newStat;
                                    break;

                                default:
                                    user.SendSystemMessage("Invalid attribute, sport.");
                                    break;
                            }
                            user.UpdateAttributes(StatUpdateFlags.Stats);
                        }
                        break;

                    case "/guild":
                        {
                            var guild = string.Join(" ", args, 1, args.Length - 1);
                            // TODO: GUILD SUPPORT
                            //user.guild = guild;
                            user.SendMessage(String.Format("Guild changed to {0}", guild), 0x1);
                        }
                        break;

                    case "/guildrank":
                        {
                            var guildrank = string.Join(" ", args, 1, args.Length - 1);
                            // TODO: GUILD SUPPORT
                            //user.GuildRank = guildrank;
                            user.SendMessage(String.Format("Guild rank changed to {0}", guildrank), 0x1);
                        }
                        break;

                    case "/title":
                        {
                            var title = string.Join(" ", args, 1, args.Length - 1);
                            // TODO: TITLE SUPPORT
                            //user.Title = title;
                            user.SendMessage(String.Format("Title changed to {0}", title), 0x1);
                        }
                        break;

                    case "/debug":
                        {
                            if (!user.IsPrivileged)
                                return;
                            user.SendMessage("Debugging enabled", 3);
                            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Debug;
                            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(
                                EventArgs.Empty);
                            Logger.InfoFormat("Debugging enabled by admin command");
                        }
                        break;

                    case "/nodebug":
                        {
                            if (!user.IsPrivileged)
                                return;
                            user.SendMessage("Debugging disabled", 3);
                            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Info;
                            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(
                                EventArgs.Empty);
                            Logger.InfoFormat("Debugging disabled by admin command");
                        }
                        break;

                    case "/gcm":
                        {
                            if (!user.IsPrivileged)
                                return;

                            var gcmContents = "Contents of Global Connection Manifest\n";
                            var userContents = "Contents of User Dictionary\n";
                            var ActiveUserContents = "Contents of ActiveUsers Concurrent Dictionary\n";
                            foreach (var pair in GlobalConnectionManifest.ConnectedClients)
                            {
                                var serverType = String.Empty;
                                switch (pair.Value.ServerType)
                                {
                                    case ServerTypes.Lobby:
                                        serverType = "Lobby";
                                        break;

                                    case ServerTypes.Login:
                                        serverType = "Login";
                                        break;

                                    default:
                                        serverType = "World";
                                        break;
                                }
                                try
                                {
                                    gcmContents = gcmContents + String.Format("{0}:{1} - {2}:{3}\n", pair.Key,
                                        ((IPEndPoint)pair.Value.Socket.RemoteEndPoint).Address.ToString(),
                                        ((IPEndPoint)pair.Value.Socket.RemoteEndPoint).Port, serverType);
                                }
                                catch
                                {
                                    gcmContents = gcmContents + String.Format("{0}:{1} disposed\n", pair.Key, serverType);
                                }
                            }
                            foreach (var tehuser in Users)
                            {
                                userContents = userContents + tehuser.Value.Name + "\n";
                            }
                            foreach (var tehotheruser in ActiveUsersByName)
                            {
                                ActiveUserContents = ActiveUserContents +
                                                     String.Format("{0}: {1}\n", tehotheruser.Value, tehotheruser.Key);
                            }

                            // Report to the end user
                            user.SendMessage(
                                String.Format("{0}\n\n{1}\n\n{2}", gcmContents, userContents, ActiveUserContents),
                                MessageTypes.SLATE_WITH_SCROLLBAR);
                        }
                        break;

                    case "/item":
                        {
                            int count;
                            string itemName;

                            Logger.DebugFormat("/item: Last argument is {0}", args.Last());
                            Regex integer = new Regex(@"^\d+$");

                            if (integer.IsMatch(args.Last()))
                            {
                                count = Convert.ToInt32(args.Last());
                                itemName = string.Join(" ", args, 1, args.Length - 2);
                                Logger.InfoFormat("Admin command: Creating item {0} with count {1}", itemName, count);
                            }
                            else
                            {
                                count = 1;
                                itemName = string.Join(" ", args, 1, args.Length - 1);
                            }

                            // HURR O(N) IS MY FRIEND
                            // change this to use itemcatalog pls
                            foreach (var template in Items)
                            {
                                if (template.Value.Name.Equals(itemName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    var item = CreateItem(template.Key);
                                    if (count > item.MaximumStack)
                                        item.Count = item.MaximumStack;
                                    else
                                        item.Count = count;
                                    Insert(item);
                                    user.AddItem(item);
                                }
                            }
                        }
                        break;

                    case "/magicval":
                        {
                            var valueName = args[1];
                            var value = args[2];
                            var property = typeof(User).GetProperty(valueName);
                            property.SetValue(user, Convert.ToByte(value));
                            user.SendSystemMessage(String.Format("Magic value {0} set to {1}", valueName, value));
                            user.UpdateAttributes(StatUpdateFlags.Full);
                        }
                        break;

                    case "/skill":
                        {
                            string skillName;

                            Logger.DebugFormat("/skill: Last argument is {0}", args.Last());
                            Regex integer = new Regex(@"^\d+$");

                            skillName = string.Join(" ", args, 1, args.Length - 1);

                            Castable skill = Skills.Where(x => x.Value.Name == skillName).FirstOrDefault().Value;
                            user.AddSkill(skill);
                        }
                        break;

                    case "/spell":
                        {
                            string spellName;

                            Logger.DebugFormat("/skill: Last argument is {0}", args.Last());
                            Regex integer = new Regex(@"^\d+$");

                            spellName = string.Join(" ", args, 1, args.Length - 1);

                            Castable spell = Spells.Where(x => x.Value.Name == spellName).FirstOrDefault().Value;
                            user.AddSpell(spell);
                        }
                        break;

                    case "/spawn":
                        {
                            string creatureName;
                            Logger.DebugFormat("/skill Last argument is {0}", args.Last());

                            creatureName = string.Join(" ", args, 1, args.Length - 1);

                            Creature creature = new Creature()
                            {
                                Sprite = 1,
                                World = Game.World,
                                Map = Game.World.Maps[500],
                                Level = 1,
                                DisplayText = "TestMob",
                                BaseHp = 100,
                                Hp = 100,
                                BaseMp = 1,
                                Name = "TestMob",
                                Id = 90210,
                                BaseStr = 3,
                                BaseCon = 3,
                                BaseDex = 3,
                                BaseInt = 3,
                                BaseWis = 3,
                                X = 50,
                                Y = 51
                            };
                            Game.World.Maps[500].InsertCreature(creature);
                            user.SendVisibleCreature(creature);
                        }
                        break;

                    case "/master":
                        {
                            //if (!user.IsPrivileged)
                            //    return;

                            user.IsMaster = !user.IsMaster;
                            user.SendMessage(user.IsMaster ? "Mastership granted" : "Mastership removed", 3);
                        }
                        break;

                    case "/mute":
                        {
                            if (!user.IsPrivileged)
                                return;
                            var charTarget = string.Join(" ", args, 1, args.Length - 1);
                            var userObj = FindUser(charTarget);
                            if (userObj != null)
                            {
                                userObj.IsMuted = true;
                                userObj.Save();
                                user.SendMessage(String.Format("{0} is now muted.", userObj.Name), 0x1);
                            }
                            else
                            {
                                user.SendMessage("That Aisling is not in Temuair.", 0x01);
                            }
                        }
                        break;

                    case "/time":
                        {
                            var time = HybrasylTime.Now();
                            user.SendMessage(time.ToString(), 0x1);
                        }
                        break;

                    case "/timeconvert":
                        {
                            var target = args[1].ToLower();
                            Logger.InfoFormat("timeconvert: {0}", target);

                            if (target == "aisling")
                            {
                                try
                                {
                                    var dateString = string.Join(" ", args, 2, args.Length - 2);
                                    var hybrasylTime = HybrasylTime.FromString(dateString);
                                    user.SendSystemMessage(HybrasylTime.ConvertToTerran(hybrasylTime).ToString("o"));
                                }
                                catch (Exception)
                                {
                                    user.SendSystemMessage("Your Aisling time could not be parsed!");
                                }
                            }
                            else if (target == "terran")
                            {
                                try
                                {
                                    var dateString = string.Join(" ", args, 2, args.Length - 2);
                                    var dateTime = DateTime.Parse(dateString);
                                    var hybrasylTime = HybrasylTime.ConvertToHybrasyl(dateTime);
                                    user.SendSystemMessage(hybrasylTime.ToString());
                                }
                                catch (Exception)
                                {
                                    user.SendSystemMessage(
                                        "Your terran time couldn't be parsed, or, you know, something else was wrong");
                                }
                            }
                            else
                            {
                                user.SendSystemMessage("Usage: /timeconvert (aisling|terran) <date>");
                            }
                        }
                        break;

                    case "/unmute":
                        {
                            if (!user.IsPrivileged)
                                return;
                            var charTarget = string.Join(" ", args, 1, args.Length - 1);
                            var userObj = FindUser(charTarget);
                            if (userObj != null)
                            {
                                userObj.IsMuted = false;
                                userObj.Save();
                                user.SendMessage(String.Format("{0} is now unmuted.", userObj.Name), 0x1);
                            }
                            else
                            {
                                user.SendMessage("That Aisling is not in Temuair.", 0x01);
                            }
                        }
                        break;

                    case "/reload":
                        {
                            if (!user.IsPrivileged)
                                return;
                            // Do nothing here for now
                            // This should reload warps, worldwarps, "item templates", worldmaps, and world map points.
                            // This should obviously use the new ControlMessage stuff.
                            user.SendMessage("This feature is not currently implemented.", 0x01);
                        }
                        break;

                    case "/shutdown":
                        {
                            if (!user.IsPrivileged)
                                return;
                            var password = args[1];
                            if (String.Equals(password, Constants.ShutdownPassword))
                            {
                                MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.ShutdownServer, user.Name));
                            }
                        }
                        break;

                    case "/scripting":
                        {
                            if (!user.IsPrivileged)
                                return;
                            // Valid scripting commands
                            // /scripting (reload|disable|enable|status) [scriptname]

                            if (args.Count() >= 3)
                            {
                                var script = ScriptProcessor.GetScript(args[2].Trim());
                                if (script != null)
                                {
                                    if (args[1].ToLower() == "reload")
                                    {
                                        script.Disabled = true;
                                        if (script.Load())
                                        {
                                            user.SendMessage(String.Format("Script {0}: reloaded", script.Name), 0x01);
                                            if (script.InstantiateScriptable())
                                                user.SendMessage(
                                                    String.Format("Script {0}: instances recreated", script.Name), 0x01);
                                        }
                                        else
                                        {
                                            user.SendMessage(
                                                String.Format("Script {0}: load error, consult status", script.Name), 0x01);
                                        }
                                    }
                                    else if (args[1].ToLower() == "enable")
                                    {
                                        script.Disabled = false;
                                        user.SendMessage(String.Format("Script {0}: enabled", script.Name), 0x01);
                                    }
                                    else if (args[1].ToLower() == "disable")
                                    {
                                        script.Disabled = true;
                                        user.SendMessage(String.Format("Script {0}: disabled", script.Name), 0x01);
                                    }
                                    else if (args[1].ToLower() == "status")
                                    {
                                        var scriptStatus = String.Format("{0}:", script.Name);
                                        String errorSummary = "--- Error Summary ---\n";

                                        if (script.Instance == null)
                                            scriptStatus = String.Format("{0} not instantiated,", scriptStatus);
                                        else
                                            scriptStatus = String.Format("{0} instantiated,", scriptStatus);
                                        if (script.Disabled)
                                            scriptStatus = String.Format("{0} disabled", scriptStatus);
                                        else
                                            scriptStatus = String.Format("{0} enabled", scriptStatus);

                                        if (script.LastRuntimeError == String.Empty &&
                                            script.CompilationError == String.Empty)
                                            errorSummary = String.Format("{0} no errors", errorSummary);
                                        else
                                        {
                                            if (script.CompilationError != String.Empty)
                                                errorSummary = String.Format("{0} compilation error: {1}", errorSummary,
                                                    script.CompilationError);
                                            if (script.LastRuntimeError != String.Empty)
                                                errorSummary = String.Format("{0} runtime error: {1}", errorSummary,
                                                    script.LastRuntimeError);
                                        }

                                        // Report to the end user
                                        user.SendMessage(String.Format("{0}\n\n{1}", scriptStatus, errorSummary),
                                            MessageTypes.SLATE_WITH_SCROLLBAR);
                                    }
                                }
                                else
                                {
                                    user.SendMessage(String.Format("Script {0} not found!", args[2]), 0x01);
                                }
                            }
                            else if (args.Count() == 2)
                            {
                                if (args[1].ToLower() == "status")
                                {
                                    // Display status information for all NPCs
                                    String statusReport = String.Empty;
                                    String errorSummary = "--- Error Summary ---\n";

                                    foreach (KeyValuePair<string, Script> entry in ScriptProcessor.Scripts)
                                    {
                                        var scriptStatus = String.Format("{0}:", entry.Key);
                                        var scriptErrors = String.Format("{0}:", entry.Key);
                                        if (entry.Value.Instance == null)
                                            scriptStatus = String.Format("{0} not instantiated,", scriptStatus);
                                        else
                                            scriptStatus = String.Format("{0} instantiated,", scriptStatus);
                                        if (entry.Value.Disabled)
                                            scriptStatus = String.Format("{0} disabled", scriptStatus);
                                        else
                                            scriptStatus = String.Format("{0} enabled", scriptStatus);

                                        if (entry.Value.LastRuntimeError == String.Empty &&
                                            entry.Value.CompilationError == String.Empty)
                                            scriptErrors = String.Format("{0} no errors", scriptErrors);
                                        else
                                        {
                                            if (entry.Value.CompilationError != String.Empty)
                                                scriptErrors = String.Format("{0} compilation error: {1}", scriptErrors,
                                                    entry.Value.CompilationError);
                                            if (entry.Value.LastRuntimeError != String.Empty)
                                                scriptErrors = String.Format("{0} runtime error: {1}", scriptErrors,
                                                    entry.Value.LastRuntimeError);
                                        }
                                        statusReport = String.Format("{0}\n{1}", statusReport, scriptStatus);
                                        errorSummary = String.Format("{0}\n{1}", errorSummary, scriptErrors);
                                    }
                                    // Report to the end user
                                    user.SendMessage(String.Format("{0}\n\n{1}", statusReport, errorSummary),
                                        MessageTypes.SLATE_WITH_SCROLLBAR);
                                }
                            }
                        }
                        break;

                    case "/rollchar":
                        {
                            // /rollchar <class> <level>
                            // Allows you to "roll a new character" of the desired Class and Level, to see his resulting HP and MP.

                            if (!user.IsPrivileged)
                                return;

                            string errorMessage = "Command format is: /rollchar <class> <level>";
                            byte level = 1;

                            if (args.Length != 3)
                            {
                                user.SendMessage(errorMessage, 0x1);
                            }
                            else if (!Enum.IsDefined(typeof(Enums.Class), args[1]))
                            {
                                user.SendMessage("Invalid class. " + errorMessage, 0x1);
                            }
                            else if (!byte.TryParse(args[2], out level) || level < 1 || level > Constants.MAX_LEVEL)
                            {
                                user.SendMessage("Invalid level. " + errorMessage, 0x1);
                            }
                            else
                            {
                                // Create a fake User, and level him up to the desired level

                                var testUser = new User(this, new Client());
                                testUser.Map = new Map();
                                testUser.BaseHp = 50;
                                testUser.BaseMp = 50;
                                testUser.Class = (Enums.Class)Enum.Parse(typeof(Enums.Class), args[1]);
                                testUser.Level = 1;

                                while (testUser.Level < level)
                                {
                                    testUser.GiveExperience(testUser.ExpToLevel);
                                }

                                user.SendMessage(string.Format("{0}, Level {1}, Hp: {2}, Mp: {3}", testUser.Class, testUser.Level, testUser.BaseHp, testUser.BaseMp), 0x01);
                            }
                        }
                        break;

                        #endregion world's biggest switch statement
                }
            }
            else
            {
                if (user.Dead)
                {
                    user.SendSystemMessage("Your voice is carried away by a sudden wind.");
                    return;
                }
                if (user.CheckSquelch(0x0e, message))
                {
                    Logger.DebugFormat("{1}: squelched (say/shout)", user.Name);
                    return;
                }

                if (isShout == 1)
                {
                    user.Shout(message);
                }
                else
                {
                    user.Say(message);
                }
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [ProhibitedCondition(PlayerCondition.Paralyzed)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x0F_UseSpell(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();
            var target = packet.ReadUInt32();

            user.UseSpell(slot, target);
            user.Status ^= PlayerCondition.Casting;
        }

        private void PacketHandler_0x0B_ClientExit(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var endSignal = packet.ReadByte();

            if (endSignal == 1)
            {
                var x4C = new ServerPacket(0x4C);
                x4C.WriteByte(0x01);
                x4C.WriteUInt16(0x00);
                user.Enqueue(x4C);
            }
            else
            {
                long connectionId;

                //user.Save();
                user.UpdateLogoffTime();
                user.Map.Remove(user);
                if (user.Grouped)
                {
                    user.Group.Remove(user);
                }
                Remove(user);
                DeleteUser(user.Name);
                user.SendRedirectAndLogoff(this, Game.Login, user.Name);

                if (ActiveUsersByName.TryRemove(user.Name, out connectionId))
                {
                    ((IDictionary)ActiveUsers).Remove(connectionId);
                }
                Logger.InfoFormat("cid {0}: {1} leaving world", connectionId, user.Name);
            }
        }

        private void PacketHandler_0x10_ClientJoin(Object obj, ClientPacket packet)
        {
            var connectionId = (long)obj;

            var seed = packet.ReadByte();
            var keyLength = packet.ReadByte();
            var key = packet.Read(keyLength);
            var name = packet.ReadString8();
            var id = packet.ReadUInt32();

            var redirect = ExpectedConnections[id];

            if (!redirect.Matches(name, key, seed)) return;

            ((IDictionary)ExpectedConnections).Remove(id);

            User loginUser;

            if (!TryGetUser(name, out loginUser)) return;

            loginUser.AssociateConnection(this, connectionId);
            loginUser.SetEncryptionParameters(key, seed, name);
            loginUser.UpdateLoginTime();
            loginUser.Inventory.RecalculateWeight();
            loginUser.Equipment.RecalculateWeight();
            loginUser.RecalculateBonuses();
            loginUser.UpdateAttributes(StatUpdateFlags.Full);
            loginUser.SendInventory();
            loginUser.SendEquipment();
            loginUser.SendSkills();
            loginUser.SendSpells();
            loginUser.SetCitizenship();

            Insert(loginUser);
            Logger.DebugFormat("Elapsed time since login: {0}", loginUser.SinceLastLogin);

            if (loginUser.Dead)
            {
                loginUser.Teleport("Chaotic Threshold", 10, 10);
            }
            else if(loginUser.Nation.SpawnPoints.Count != 0 &&
                loginUser.SinceLastLogin > Hybrasyl.Constants.NATION_SPAWN_TIMEOUT)
            {
                var spawnpoint = loginUser.Nation.SpawnPoints.First();
                loginUser.Teleport(spawnpoint.MapName, spawnpoint.X, spawnpoint.Y);
            }
            else if (Maps.ContainsKey(loginUser.Location.MapId))
            {
                loginUser.Teleport(loginUser.Location.MapId, (byte)loginUser.Location.X, (byte)loginUser.Location.Y);
            }
            else
            {
                // Handle any weird cases where a map someone exited on was deleted, etc
                // This "default" of Mileth should be set somewhere else
                loginUser.Teleport((ushort)500, (byte)50, (byte)50);
            }

            Logger.DebugFormat("Adding {0} to hash", loginUser.Name);
            AddUser(loginUser);
            ActiveUsers[connectionId] = loginUser;
            ActiveUsersByName[loginUser.Name] = connectionId;
            Logger.InfoFormat("cid {0}: {1} entering world", connectionId, loginUser.Name);
            Logger.InfoFormat($"{loginUser.SinceLastLoginString}");
            // If the user's never logged off before (new character), don't display this message.
            if (loginUser.Login.LastLogoff != default(DateTime))
            {
                loginUser.SendSystemMessage($"It has been {loginUser.SinceLastLoginString} since your last login.");
            }
            loginUser.SendSystemMessage(HybrasylTime.Now().ToString());
            loginUser.Reindex();
        }

        [ProhibitedCondition(PlayerCondition.Frozen)]
        private void PacketHandler_0x11_Turn(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var direction = packet.ReadByte();
            if (direction > 3) return;
            user.Turn((Direction)direction);
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [ProhibitedCondition(PlayerCondition.Paralyzed)]
        private void PacketHandler_0x13_Attack(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            user.AssailAttack(user.Direction);
        }

        private void PacketHandler_0x18_ShowPlayerList(Object obj, ClientPacket packet)
        {
            var me = (User)obj;

            var list = from user in Users.Values
                       orderby user.IsMaster descending, user.Level descending, user.BaseHp + user.BaseMp * 2 descending, user.Name ascending
                       select user;

            var listPacket = new ServerPacket(0x36);
            listPacket.WriteUInt16((ushort)list.Count());
            listPacket.WriteUInt16((ushort)list.Count());

            foreach (var user in list)
            {
                int levelDifference = Math.Abs((int)user.Level - me.Level);

                listPacket.WriteByte((byte)user.Class);
                // TODO: GUILD SUPPORT
                //if (!string.IsNullOrEmpty(me.Guild) && user.Guild == me.Guild) listPacket.WriteByte(84);
                if (levelDifference <= 5) listPacket.WriteByte(151);
                else listPacket.WriteByte(255);

                listPacket.WriteByte((byte)user.GroupStatus);
                listPacket.WriteString8(""); //user.Title);
                listPacket.WriteBoolean(user.IsMaster);
                listPacket.WriteString8(user.Name);
            }
            me.Enqueue(listPacket);
        }

        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x19_Whisper(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var size = packet.ReadByte();
            var target = Encoding.GetEncoding(949).GetString(packet.Read(size));
            var msgsize = packet.ReadByte();
            var message = Encoding.GetEncoding(949).GetString(packet.Read(msgsize));

            // "!!" is the special character sequence for group whisper. If this is the
            // target, the message should be sent as a group whisper instead of a standard
            // whisper.
            if (target == "!!")
            {
                user.SendGroupWhisper(message);
            }
            else
            {
                user.SendWhisper(target, message);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x1C_UseItem(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();

            Logger.DebugFormat("Updating slot {0}", slot);

            if (slot == 0 || slot > Constants.MAXIMUM_INVENTORY) return;

            var item = user.Inventory[slot];

            if (item == null) return;

            switch (item.ItemObjectType)
            {
                case Enums.ItemObjectType.CanUse:
                    if (item.Durability == 0)
                    {
                        user.SendSystemMessage("This item is too badly damaged to use.");
                        return;
                    }

                    item.Invoke(user);
                    if (item.Count == 0)
                        user.RemoveItem(slot);
                    else
                        user.SendItemUpdate(item, slot);
                    break;

                case Enums.ItemObjectType.CannotUse:
                    user.SendMessage("You can't use that.", 3);
                    break;

                case Enums.ItemObjectType.Equipment:
                    {
                        if (item.Durability == 0)
                        {
                            user.SendSystemMessage("This item is too badly damaged to use.");
                            return;
                        }
                        // Check item requirements here before we do anything rash
                        String message;
                        if (!item.CheckRequirements(user, out message))
                        {
                            // If an item can't be equipped, CheckRequirements will return false
                            // and also set the appropriate message for us via out
                            user.SendMessage(message, 3);
                            return;
                        }
                        Logger.DebugFormat("Equipping {0}", item.Name);
                        // Remove the item from inventory, but we don't decrement its count, as it still exists.
                        user.RemoveItem(slot);

                        // Handle gauntlet / ring special cases
                        if (item.EquipmentSlot == ClientItemSlots.Gauntlet)
                        {
                            Logger.DebugFormat("item is gauntlets");
                            // First, is the left arm slot occupied?
                            if (user.Equipment[ClientItemSlots.LArm] != null)
                            {
                                if (user.Equipment[ClientItemSlots.RArm] == null)
                                {
                                    // Right arm slot is empty; use it
                                    user.AddEquipment(item, ClientItemSlots.RArm);
                                }
                                else
                                {
                                    // Right arm slot is in use; replace LArm with item
                                    var olditem = user.Equipment[ClientItemSlots.LArm];
                                    user.RemoveEquipment(ClientItemSlots.LArm);
                                    user.AddItem(olditem, slot);
                                    user.AddEquipment(item, ClientItemSlots.LArm);
                                }
                            }
                            else
                            {
                                user.AddEquipment(item, ClientItemSlots.LArm);
                            }
                        }
                        else if (item.EquipmentSlot == ClientItemSlots.Ring)
                        {
                            Logger.DebugFormat("item is ring");

                            // First, is the left ring slot occupied?
                            if (user.Equipment[ClientItemSlots.LHand] != null)
                            {
                                if (user.Equipment[ClientItemSlots.RHand] == null)
                                {
                                    // Right ring slot is empty; use it
                                    user.AddEquipment(item, ClientItemSlots.RHand);
                                }
                                else
                                {
                                    // Right ring slot is in use; replace LHand with item
                                    var olditem = user.Equipment[ClientItemSlots.LHand];
                                    user.RemoveEquipment(ClientItemSlots.LHand);
                                    user.AddItem(olditem, slot);
                                    user.AddEquipment(item, ClientItemSlots.LHand);
                                }
                            }
                            else
                            {
                                user.AddEquipment(item, ClientItemSlots.LHand);
                            }
                        }
                        else if (item.EquipmentSlot == ClientItemSlots.FirstAcc ||
                                 item.EquipmentSlot == ClientItemSlots.SecondAcc ||
                                 item.EquipmentSlot == ClientItemSlots.ThirdAcc)
                        {
                            if (user.Equipment.FirstAcc == null)
                                user.AddEquipment(item, ClientItemSlots.FirstAcc);
                            else if (user.Equipment.SecondAcc == null)
                                user.AddEquipment(item, ClientItemSlots.SecondAcc);
                            else if (user.Equipment.ThirdAcc == null)
                                user.AddEquipment(item, ClientItemSlots.ThirdAcc);
                            else
                            {
                                // Remove first accessory
                                var oldItem = user.Equipment.FirstAcc;
                                user.RemoveEquipment(ClientItemSlots.FirstAcc);
                                user.AddEquipment(item, ClientItemSlots.FirstAcc);
                                user.AddItem(oldItem, slot);
                                user.Show();
                            }
                        }
                        else
                        {
                            var equipSlot = item.EquipmentSlot;
                            var oldItem = user.Equipment[equipSlot];

                            if (oldItem != null)
                            {
                                Logger.DebugFormat(" Attemping to equip {0}", item.Name);
                                Logger.DebugFormat("..which would unequip {0}", oldItem.Name);
                                Logger.DebugFormat("Player weight is currently {0}", user.CurrentWeight);
                                user.RemoveEquipment(equipSlot);
                                user.AddItem(oldItem, slot);
                                user.AddEquipment(item, equipSlot);
                                user.Show();
                                Logger.DebugFormat("Player weight is currently {0}", user.CurrentWeight);
                            }
                            else
                            {
                                Logger.DebugFormat("Attemping to equip {0}", item.Name);
                                user.AddEquipment(item, equipSlot);
                                user.Show();
                            }
                        }
                    }
                    break;
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x1D_Emote(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var emote = packet.ReadByte();
            if (emote <= 35)
            {
                emote += 9;
                user.Motion(emote, 120);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x24_DropGold(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var amount = packet.ReadUInt32();
            var x = packet.ReadInt16();
            var y = packet.ReadInt16();

            Logger.DebugFormat("{0} {1} {2}", amount, x, y);
            // Do a few sanity checks

            // Is the distance valid? (Can't drop things beyond
            // MAXIMUM_DROP_DISTANCE tiles away)
            if (Math.Abs(x - user.X) > Constants.PICKUP_DISTANCE ||
                Math.Abs(y - user.Y) > Constants.PICKUP_DISTANCE)
            {
                Logger.ErrorFormat("Request to drop gold exceeds maximum distance {0}",
                    Hybrasyl.Constants.MAXIMUM_DROP_DISTANCE);
                return;
            }

            // Does the amount in the packet exceed the
            // amount of gold the player has?  Are they trying to drop the item on something that
            // is impassable (i.e. a wall)?
            if ((amount > user.Gold) || (x >= user.Map.X) || (y >= user.Map.Y) ||
                (x < 0) || (y < 0) || (user.Map.IsWall[x, y]))
            {
                Logger.ErrorFormat("Amount {0} exceeds amount {1}, or {2},{3} is a wall, or {2},{3} is out of bounds",
                    amount, user.Gold, x, y);
                return;
            }

            var toDrop = new Gold(amount);
            user.RemoveGold(amount);

            Insert(toDrop);

            // Are we dropping an item onto a reactor?
            Objects.Reactor reactor;
            var coordinates = new Tuple<byte, byte>((byte)x, (byte)y);
            if (user.Map.Reactors.TryGetValue(coordinates, out reactor))
            {
                reactor.OnDrop(user, toDrop);
            }
            else
                user.Map.AddGold(x, y, toDrop);
        }

        private void PacketHandler_0x2D_PlayerInfo(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            user.SendProfile();
        }

        /**
         * Handle user-initiated grouping requests. There are a number of mechanisms in the client
         * that send this packet, but generally amount to one of three serverside actions:
         *    1) Request that the user join my group (stage 0x02).
         *    2) Leave the group I'm currently in (stage 0x02).
         *    3) Confirm that I'd like to accept a group request (stage 0x03).
         * The general flow here consists of the following steps:
         * Check to see if we should add the partner to the group, or potentially remove them
         *    1) if user and partner are already in the same group.
         *    2) Check to see if the partner is open for grouping. If not, fail.
         *    3) Sending a group request to the group you're already in == ungroup request in USDA.
         *    4) If the user's already grouped, they can't join this group.
         *    5) Send them a dialog and have them explicitly accept.
         *    6) If accepted, join group (see stage 0x03).
         */
        [ProhibitedCondition(PlayerCondition.InComa)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x2E_GroupRequest(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            // stage:
            //   0x02 = user is sending initial request to invitee
            //   0x03 = invitee responds with a "yes"
            byte stage = packet.ReadByte();
            User partner = FindUser(packet.ReadString8());

            // TODO: currently leaving five bytes on the table here. There's probably some
            // additional work that needs to happen though I haven't been able to determine
            // what those bytes represent yet...

            if (partner == null)
            {
                return;
            }

            switch (stage)
            {
                // Stage 0x02 means that a user is sending an initial request to the invitee.
                // That means we need to check whether the user is a valid candidate for
                // grouping, and send the confirmation dialog if so.
                case 0x02:
                    Logger.DebugFormat("{0} invites {1} to join a group.", user.Name, partner.Name);

                    // Remove the user from the group. Kinda logically weird beside all of this other stuff
                    // so it may be worth restructuring but it should be accurate as-is.
                    if (partner.Grouped && user.Grouped && partner.Group == user.Group)
                    {
                        Logger.DebugFormat("{0} leaving group.", user.Name);
                        user.Group.Remove(partner);
                        return;
                    }

                    // Now we know that we're trying to add this person to the group, not remove them.
                    // Let's find out if they're eligible and invite them if so.
                    if (partner.Grouped)
                    {
                        user.SendMessage(String.Format("{0} is already in a group.", partner.Name), MessageTypes.SYSTEM);
                        return;
                    }

                    if (!partner.Grouping)
                    {
                        user.SendMessage(String.Format("{0} is not accepting group invitations.", partner.Name), MessageTypes.SYSTEM);
                        return;
                    }

                    // Send partner a dialog asking whether they want to group (opcode 0x63).
                    ServerPacket response = new ServerPacket(0x63);
                    response.WriteByte((byte)0x01);
                    response.WriteString8(user.Name);
                    response.WriteByte(0);
                    response.WriteByte(0);

                    partner.Enqueue(response);
                    break;
                // Stage 0x03 means that the invitee has responded with a "yes" to the grouping
                // request. We need to add them to the original user's group. Note that in this
                // case the partner sent the original invitation.
                case 0x03:
                    Logger.Debug("Invitation accepted. Grouping.");
                    partner.InviteToGroup(user);
                    break;

                default:
                    Logger.Error("Unknown GroupRequest stage. No action taken.");
                    break;
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x2F_GroupToggle(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            // If the user is in a group, they must leave (in particular going from true to false,
            // but in no case should you be able to hold a group across this transition).
            if (user.Grouped)
            {
                user.Group.Remove(user);
            }

            user.Grouping = !user.Grouping;
            user.Save();

            // TODO: Is there any packet content that needs to be used on the server? It appears there
            // are extra bytes coming through but not sure what purpose they serve.
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x2A_DropGoldOnCreature(Object obj, ClientPacket packet)
        {
            var goldAmount = packet.ReadUInt32();
            var targetId = packet.ReadUInt32();

            var user = (User)obj;
            // If the object is a creature or an NPC, simply give them the item, otherwise,
            // initiate an exchange

            WorldObject target;
            if (!user.World.Objects.TryGetValue(targetId, out target))
                return;

            if (user.Map.Objects.Contains((VisibleObject)target))
            {
                if (target is User)
                {
                    // Initiate exchange and put gold in it
                    var playerTarget = (User)target;

                    // Pre-flight checks
                    if (!Exchange.StartConditionsValid(user, playerTarget))
                    {
                        user.SendSystemMessage("You can't do that.");
                        return;
                    }
                    if (!playerTarget.IsAvailableForExchange)
                    {
                        user.SendMessage("They can't do that right now.", MessageTypes.SYSTEM);
                        return;
                    }
                    if (!user.IsAvailableForExchange)
                    {
                        user.SendMessage("You can't do that right now.", MessageTypes.SYSTEM);
                    }
                    // Start exchange
                    var exchange = new Exchange(user, playerTarget);
                    exchange.StartExchange();
                    exchange.AddGold(user, goldAmount);
                }
                else if (target is Creature && user.IsInViewport((VisibleObject)target))
                {
                    // Give gold to Creature and go about our lives
                    var creature = (Creature)target;
                    creature.Gold += goldAmount;
                    user.Gold -= goldAmount;
                    user.UpdateAttributes(StatUpdateFlags.Stats);
                }
                else
                {
                    Logger.DebugFormat("user {0}: invalid drop target");
                }
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x29_DropItemOnCreature(Object obj, ClientPacket packet)
        {
            var itemSlot = packet.ReadByte();
            var targetId = packet.ReadUInt32();
            var quantity = packet.ReadByte();
            var user = (User)obj;

            // If the object is a creature or an NPC, simply give them the item, otherwise,
            // initiate an exchange

            WorldObject target;
            if (!user.World.Objects.TryGetValue(targetId, out target))
                return;

            if (user.Map.Objects.Contains((VisibleObject)target))
            {
                if (target is User)
                {
                    var playerTarget = (User)target;

                    // Pre-flight checks

                    if (!Exchange.StartConditionsValid(user, playerTarget))
                    {
                        user.SendSystemMessage("You can't do that.");
                        return;
                    }
                    if (!playerTarget.IsAvailableForExchange)
                    {
                        user.SendSystemMessage("They can't do that right now.");
                        return;
                    }
                    if (!user.IsAvailableForExchange)
                    {
                        user.SendSystemMessage("You can't do that right now.");
                    }
                    // Initiate exchange and put item in it
                    var exchange = new Exchange(user, playerTarget);
                    exchange.StartExchange();
                    if (user.Inventory[itemSlot] != null && user.Inventory[itemSlot].Count > 1)
                        user.SendExchangeQuantityPrompt(itemSlot);
                    else
                        exchange.AddItem(user, itemSlot, quantity);
                }
                else if (target is Creature && user.IsInViewport((VisibleObject)target))
                {
                    var creature = (Creature)target;
                    var item = user.Inventory[itemSlot];
                    if (item != null)
                    {
                        if (user.RemoveItem(itemSlot))
                        {
                            creature.Inventory.AddItem(item);
                        }
                        else
                            Logger.WarnFormat("0x29: Couldn't remove item from inventory...?");
                    }
                }
            }
        }

        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x30_MoveUIElement(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var window = packet.ReadByte();
            var oldSlot = packet.ReadByte();
            var newSlot = packet.ReadByte();

            // For right now we ignore the other cases (moving a skill or spell)

            //0 inv, 1 skills, 2 spells. are there others?

            if (window > 2)
                return;

            Logger.DebugFormat("Moving {0} to {1}", oldSlot, newSlot);

            //0 inv, 1 spellbook, 2 skillbook (WHAT FUCKING INTERN REVERSED THESE??).
            switch (window)
            {
                case 0:
                    {
                        var inventory = user.Inventory;
                        if (oldSlot == 0 || oldSlot > Constants.MAXIMUM_INVENTORY || newSlot == 0 || newSlot > Constants.MAXIMUM_INVENTORY || (inventory[oldSlot] == null && inventory[newSlot] == null)) return;
                        user.SwapItem(oldSlot, newSlot);
                        break;
                    }
                case 1:
                    {
                        var book = user.SpellBook;
                        if (oldSlot == 0 || oldSlot > Constants.MAXIMUM_BOOK || newSlot == 0 || newSlot > Constants.MAXIMUM_BOOK || (book[oldSlot] == null && book[newSlot] == null)) return;
                        user.SwapCastable(oldSlot, newSlot, book);
                        break;
                    }
                case 2:
                    {
                        var book = user.SkillBook;
                        if (oldSlot == 0 || oldSlot > Constants.MAXIMUM_BOOK || newSlot == 0 || newSlot > Constants.MAXIMUM_BOOK || (book[oldSlot] == null && book[newSlot] == null)) return;
                        user.SwapCastable(oldSlot, newSlot, book);
                        break;
                    }
               
                default:
                    break;
            }

            // Is the slot invalid? Does at least one of the slots contain an item?
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x3B_AccessMessages(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var response = new ServerPacket(0x31);
            var action = packet.ReadByte();

            switch (action)
            {
                case 0x01:
                    {
                        // Display board list
                        response.WriteByte(0x01);

                        // TODO: This has the potential to be a somewhat expensive operation, optimize this.
                        var boardList =
                            Messageboards.Values.Where(mb => mb.CheckAccessLevel(user.Name, BoardAccessLevel.Read));

                        // Mail is always the first board and has a fixed id of 0
                        response.WriteUInt16((ushort)(boardList.Count() + 1));
                        response.WriteUInt16(0);
                        response.WriteString8("Mail");
                        foreach (var board in boardList)
                        {
                            response.WriteUInt16((ushort)board.Id);
                            response.WriteString8(board.DisplayName);
                        }
                        response.TransmitDelay = 600; // This is so the 'w' key in the client works
                                                      // Without this, the messaging panel is a jittery piece of crap that never opens
                    }
                    break;

                case 0x02:
                    {
                        // Get message list
                        var boardId = packet.ReadUInt16();
                        var startPostId = packet.ReadInt16();

                        if (boardId == 0)
                        {
                            user.Enqueue(user.Mailbox.RenderToPacket());
                            return;
                        }
                        else
                        {
                            Board board;
                            if (MessageboardIndex.TryGetValue(boardId, out board))
                            {
                                user.Enqueue(board.RenderToPacket());
                                return;
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                case 0x03:
                    {
                        // Get message
                        var boardId = packet.ReadUInt16();
                        var postId = packet.ReadInt16();
                        var messageId = postId - 1;
                        var offset = packet.ReadSByte();
                        Message message = null;
                        var error = string.Empty;
                        if (boardId == 0)
                        {
                            // Mailbox access
                            switch (offset)
                            {
                                case 0:
                                    {
                                        // postId is the exact message
                                        if (postId >= 0 && postId <= user.Mailbox.Messages.Count)
                                            message = user.Mailbox.Messages[messageId];
                                        else
                                            error = "That post could not be found.";
                                        break;
                                    }
                                case 1:
                                    {
                                        // Client clicked "prev", which hilariously means "newer"
                                        // postId in this case is the next message
                                        if (postId > user.Mailbox.Messages.Count)
                                            error = "There are no newer messages.";
                                        else
                                        {
                                            var messageList = user.Mailbox.Messages.GetRange(messageId,
                                                user.Mailbox.Messages.Count - messageId);
                                            message = messageList.Find(m => m.Deleted == false);

                                            if (message == null)
                                                error = "There are no newer messages.";
                                        }
                                    }
                                    break;

                                case -1:
                                    {
                                        // Client clicked "next", which means "older"
                                        // postId is previous message
                                        if (postId < 0)
                                            error = "There are no older messages.";
                                        else
                                        {
                                            var messageList = user.Mailbox.Messages.GetRange(0, postId);
                                            messageList.Reverse();
                                            message = messageList.Find(m => m.Deleted == false);
                                            if (message == null)
                                                error = "There are no older messages.";
                                        }
                                    }
                                    break;

                                default:
                                    {
                                        error = "Invalid offset (nice try, chief)";
                                    }
                                    break;
                            }
                            if (message != null)
                            {
                                user.Enqueue(message.RenderToPacket());
                                message.Read = true;
                                user.UpdateAttributes(StatUpdateFlags.Secondary);
                                return;
                            }
                            response.WriteByte(0x06);
                            response.WriteBoolean(false);
                            response.WriteString8(error);
                        }
                        else
                        {
                            // Get board message
                            Board board;
                            if (MessageboardIndex.TryGetValue(boardId, out board))
                            {
                                // TODO: handle this better
                                if (!board.CheckAccessLevel(user.Name, BoardAccessLevel.Read))
                                    return;

                                switch (offset)
                                {
                                    case 0:
                                        {
                                            // postId is the exact message
                                            if (postId >= 0 && postId <= board.Messages.Count)
                                            {
                                                if (board.Messages[messageId].Deleted)
                                                    error = "There is no such message.";
                                                else
                                                    message = board.Messages[messageId];
                                            }
                                            else
                                                error = "That post could not be found.";
                                            break;
                                        }
                                    case 1:
                                        {
                                            // Client clicked "prev", which hilariously means "newer"
                                            // postId in this case is the next message
                                            if (postId > board.Messages.Count)
                                                error = "There are no newer messages.";
                                            else
                                            {
                                                var messageList = board.Messages.GetRange(messageId,
                                                    board.Messages.Count - messageId);
                                                message = messageList.Find(m => m.Deleted == false);

                                                if (message == null)
                                                    error = "There are no newer messages.";
                                            }
                                        }
                                        break;

                                    case -1:
                                        {
                                            // Client clicked "next", which means "older"
                                            // postId is previous message
                                            if (postId < 0)
                                                error = "There are no older messages.";
                                            else
                                            {
                                                var messageList = board.Messages.GetRange(0, postId);
                                                messageList.Reverse();
                                                message = messageList.Find(m => m.Deleted == false);
                                                if (message == null)
                                                    error = "There are no older messages.";
                                            }
                                        }
                                        break;

                                    default:
                                        {
                                            error = "Invalid offset (nice try, chief)";
                                        }
                                        break;
                                }
                                if (message != null)
                                {
                                    user.Enqueue(message.RenderToPacket());
                                    message.Read = true;
                                    return;
                                }
                                response.WriteByte(0x06);
                                response.WriteBoolean(false);
                                response.WriteString8(error);
                            }
                        }
                    }
                    break;
                // Send message
                case 0x04:
                    {
                        var boardId = packet.ReadUInt16();
                        var subject = packet.ReadString8();
                        var body = packet.ReadString16();
                        Board board;
                        response.WriteByte(0x06); // Generic board response
                        if (DateTime.Now.Ticks - user.LastMailboxMessageSent < Constants.SEND_MESSAGE_COOLDOWN)
                        {
                            response.WriteBoolean(false);
                            response.WriteString8("Try waiting a moment before sending another message.");
                        }
                        if (MessageboardIndex.TryGetValue(boardId, out board))
                        {
                            if (board.CheckAccessLevel(user.Name, BoardAccessLevel.Write))
                            {
                                if (board.ReceiveMessage(new Message(board.Name, user.Name, subject, body)))
                                {
                                    response.WriteBoolean(true);
                                    response.WriteString8("Your message has been sent.");
                                }
                                else
                                {
                                    if (board.IsLocked)
                                    {
                                        response.WriteBoolean(false);
                                        response.WriteString8(
                                            "This board is being cleaned by the Mundanes. Please try again later.");
                                    }
                                    else if (board.Full)
                                    {
                                        response.WriteBoolean(false);
                                        response.WriteString8(
                                            "This board has too many papers nailed to it. Please try again later.");
                                    }
                                }
                            }
                            else
                            {
                                response.WriteBoolean(false);
                                response.WriteString8(
                                    "A strange, ethereal force prohibits you from doing that.");
                            }
                        }
                        else
                        {
                            Logger.WarnFormat("boards: {0} tried to post to non-existent board {1}",
                                user.Name, boardId);
                            response.WriteBoolean(false);
                            response.WriteString8(
                                "...What would you say you're doing here?");
                        }
                    }
                    break;
                // Delete post
                case 0x05:
                    {
                        response.WriteByte(0x07); // Delete post response
                        var boardId = packet.ReadUInt16();
                        var postId = packet.ReadUInt16();
                        Board board;
                        if (MessageboardIndex.TryGetValue(boardId, out board))
                        {
                            if (user.IsPrivileged || board.CheckAccessLevel(user.Name, BoardAccessLevel.Moderate))
                            {
                                board.DeleteMessage(postId - 1);
                                response.WriteBoolean(true);
                                response.WriteString8("The message was destroyed.");
                            }
                            else
                            {
                                response.WriteBoolean(false);
                                response.WriteString8("You can't do that.");
                            }
                        }
                        else
                        {
                            Logger.WarnFormat("boards: {0} tried to post to non-existent board {1}",
                                user.Name, boardId);
                            response.WriteBoolean(false);
                            response.WriteString8(
                                "...What would you say you're doing here?");
                        }
                    }
                    break;

                case 0x06:
                    {
                        // Send mail (which one might argue, ye olde DOOMVAS protocol designers, is a type of message)

                        var boardId = packet.ReadUInt16();
                        var recipient = packet.ReadString8();
                        var subject = packet.ReadString8();
                        var body = packet.ReadString16();
                        response.WriteByte(0x06); // Send post response
                        User recipientUser;

                        if (Users.TryGetValue(recipient, out recipientUser))
                        {
                            try
                            {
                                if (recipientUser.Mailbox.ReceiveMessage(new Message(recipientUser.Name, user.Name, subject,
                                    body)))
                                {
                                    response.WriteBoolean(true); // Post was successful
                                    response.WriteString8("Your letter was sent.");
                                    Logger.InfoFormat("mail: {0} sent message to {1}", user.Name, recipientUser.Name);
                                    MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MailNotifyUser,
                                        recipientUser.Name));
                                }
                                else
                                {
                                    response.WriteBoolean(true);
                                    response.WriteString8("{0}'s mailbox is full. Your message was discarded. Sorry!");
                                }
                            }
                            catch (MessageStoreLocked)
                            {
                                response.WriteBoolean(true);
                                response.WriteString8("{0} cannot receive mail at this time. Sorry!");
                            }
                        }
                        else
                        {
                            response.WriteBoolean(true);
                            response.WriteString8("Sadly, no record of that person exists in the realm.");
                        }
                    }
                    break;

                case 0x07:
                    // Highlight message
                    {
                        // Highlight post (GM only)
                        var boardId = packet.ReadUInt16();
                        var postId = packet.ReadInt16();
                        response.WriteByte(0x08); // Highlight response
                        Board board;

                        if (!user.IsPrivileged)
                        {
                            response.WriteBoolean(false);
                            response.WriteString("You cannot highlight this message.");
                            Logger.WarnFormat("mail: {0} tried to highlight message {1} but isn't GM! Hijinx suspected.",
                                user.Name, postId);
                        }
                        if (MessageboardIndex.TryGetValue(boardId, out board))
                        {
                            board.Messages[postId - 1].Highlighted = true;
                            response.WriteBoolean(true);
                            response.WriteString8("The message was highlighted. Good work, chief.");
                        }
                        else
                        {
                            response.WriteBoolean(false);
                            response.WriteString8("...What would you say you're trying to do here?");
                        }
                    }
                    break;

                default:
                    {
                    }
                    break;
            }

            user.Enqueue(response);
        }


        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [ProhibitedCondition(PlayerCondition.Paralyzed)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x3E_UseSkill(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();

            user.UseSkill(slot);
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        private void PacketHandler_0x3F_MapPointClick(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var target = BitConverter.ToInt64(packet.Read(8), 0);
            Logger.DebugFormat("target bytes are: {0}, maybe", target);

            if (user.IsAtWorldMap)
            {
                MapPoint targetmap;
                if (MapPoints.TryGetValue(target, out targetmap))
                {
                    user.Teleport(targetmap.DestinationMap, targetmap.DestinationX, targetmap.DestinationY);
                }
                else
                {
                    Logger.ErrorFormat(String.Format("{0}: sent us a click to a non-existent map point!",
                        user.Name));
                }
            }
            else
            {
                Logger.ErrorFormat(String.Format("{0}: sent us an 0x3F outside of a map screen!",
                    user.Name));
            }
        }

        private void PacketHandler_0x38_Refresh(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            if (user.CheckSquelch(0x38, null))
            {
                Logger.InfoFormat("{0}: squelched (refresh)", user.Name);
                return;
            }
            user.Refresh();
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x39_NPCMainMenu(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            if (user.CheckSquelch(0x38, null))
            {
                Logger.InfoFormat("{0}: squelched (NPC main menu)", user.Name);
                return;
            }

            // We just ignore the header, because, really, what exactly is a 16-bit encryption
            // key plus CRC really doing for you
            var header = packet.ReadDialogHeader();
            var objectType = packet.ReadByte();
            var objectId = packet.ReadUInt32();
            var pursuitId = packet.ReadUInt16();

            Logger.DebugFormat("main menu packet: ObjectType {0}, ID {1}, pursuitID {2}",
                objectType, objectId, pursuitId);

            // Sanity checks
            WorldObject wobj;

            if (Game.World.Objects.TryGetValue(objectId, out wobj))
            {
                // Are we handling a global sequence?
                DialogSequence pursuit;
                VisibleObject clickTarget = wobj as VisibleObject;

                if (pursuitId < Constants.DIALOG_SEQUENCE_SHARED)
                {
                    // Does the sequence exist in the global catalog?
                    try
                    {
                        pursuit = Game.World.GlobalSequences[pursuitId];
                    }
                    catch
                    {
                        Logger.ErrorFormat("{0}: pursuit ID {1} doesn't exist in the global catalog?",
                            wobj.Name, pursuitId);
                        return;
                    }
                }
                else if (pursuitId >= Constants.DIALOG_SEQUENCE_HARDCODED)
                {
                    if (!(wobj is Merchant))
                    {
                        Logger.ErrorFormat("{0}: attempt to use hardcoded merchant menu item on non-merchant",
                            wobj.Name, pursuitId);
                        return;
                    }

                    var menuItem = (MerchantMenuItem)pursuitId;
                    var merchant = (Merchant)wobj;
                    MerchantMenuHandler handler;

                    if (!merchantMenuHandlers.TryGetValue(menuItem, out handler))
                    {
                        Logger.ErrorFormat("{0}: merchant menu item {1} doesn't exist?",
                            wobj.Name, menuItem);
                        return;
                    }

                    if (!merchant.Jobs.HasFlag(handler.RequiredJob))
                    {
                        Logger.ErrorFormat("{0}: merchant does not have required job {1}",
                            wobj.Name, handler.RequiredJob);
                        return;
                    }

                    handler.Callback(user, merchant, packet);
                    return;
                }
                else
                {
                    // This is a local pursuit
                    try
                    {
                        pursuit = clickTarget.Pursuits[pursuitId - Constants.DIALOG_SEQUENCE_SHARED];
                    }
                    catch
                    {
                        Logger.ErrorFormat("{0}: local pursuit {1} doesn't exist?", wobj.Name, pursuitId);
                        return;
                    }
                }
                Logger.DebugFormat("{0}: showing initial dialog for Pursuit {1} ({2})",
                    clickTarget.Name, pursuit.Id, pursuit.Name);
                user.DialogState.StartDialog(clickTarget, pursuit);
                pursuit.ShowTo(user, clickTarget);
            }
            else
            {
                Logger.WarnFormat("specified object ID {0} doesn't exist?", objectId);
                return;
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x3A_DialogUse(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            if (user.CheckSquelch(0x38, null))
            {
                Logger.InfoFormat("{0}: squelched (dialog use)", user.Name);
                return;
            }

            var header = packet.ReadDialogHeader();
            var objectType = packet.ReadByte();
            var objectID = packet.ReadUInt32();
            var pursuitID = packet.ReadUInt16();
            var pursuitIndex = packet.ReadUInt16();

            Logger.DebugFormat("objectType {0}, objectID {1}, pursuitID {2}, pursuitIndex {3}",
                objectType, objectID, pursuitID, pursuitIndex);

            Logger.DebugFormat("active dialog via state object: pursuitID {0}, pursuitIndex {1}",
                user.DialogState.CurrentPursuitId, user.DialogState.CurrentPursuitIndex);

            if (pursuitID == user.DialogState.CurrentPursuitId && pursuitIndex == user.DialogState.CurrentPursuitIndex)
            {
                // If we get a packet back with the same index and ID, the dialog has been closed.
                Logger.DebugFormat("Dialog closed, resetting dialog state");
                user.DialogState.EndDialog();
                return;
            }

            if ((pursuitIndex > user.DialogState.CurrentPursuitIndex + 1) ||
                (pursuitIndex < user.DialogState.CurrentPursuitIndex - 1))
            {
                Logger.ErrorFormat("Dialog index is outside of acceptable limits (next/prev)");
                return;
            }

            WorldObject wobj;

            if (user.World.Objects.TryGetValue(objectID, out wobj))
            {
                VisibleObject clickTarget = wobj as VisibleObject;
                // Was the previous button clicked? Handle that first
                if (pursuitIndex == user.DialogState.CurrentPursuitIndex - 1)
                {
                    Logger.DebugFormat("Handling prev: client passed index {0}, current index is {1}",
                        pursuitIndex, user.DialogState.CurrentPursuitIndex);

                    if (user.DialogState.SetDialogIndex(clickTarget, pursuitID, pursuitIndex))
                    {
                        user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                        return;
                    }
                }

                // Is the active dialog an input or options dialog?

                if (user.DialogState.ActiveDialog is OptionsDialog)
                {
                    var paramsLength = packet.ReadByte();
                    var option = packet.ReadByte();
                    var dialog = user.DialogState.ActiveDialog as OptionsDialog;
                    dialog.HandleResponse(user, option, clickTarget);
                }

                if (user.DialogState.ActiveDialog is TextDialog)
                {
                    var paramsLength = packet.ReadByte();
                    var response = packet.ReadString8();
                    var dialog = user.DialogState.ActiveDialog as TextDialog;
                    dialog.HandleResponse(user, response, clickTarget);
                }

                // Did the handling of a response result in our active dialog sequence changing? If so, exit.

                if (user.DialogState.CurrentPursuitId != pursuitID)
                {
                    Logger.DebugFormat("Dialog has changed, exiting");
                    return;
                }

                if (user.DialogState.SetDialogIndex(clickTarget, pursuitID, pursuitIndex))
                {
                    Logger.DebugFormat("Pursuit index is now {0}", pursuitIndex);
                    user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                    return;
                }
                else
                {
                    Logger.DebugFormat("Sending close packet");
                    var p = new ServerPacket(0x30);
                    p.WriteByte(0x0A);
                    p.WriteByte(0x00);
                    user.Enqueue(p);
                    user.DialogState.EndDialog();
                }
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x43_PointClick(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var clickType = packet.ReadByte();
            Rectangle commonViewport = user.GetViewport();
            // User has clicked an X,Y point
            if (clickType == 3)
            {
                var x = (byte)packet.ReadUInt16();
                var y = (byte)packet.ReadUInt16();
                var coords = new Tuple<byte, byte>(x, y);
                Logger.DebugFormat("coordinates were {0}, {1}", x, y);

                if (user.Map.Doors.ContainsKey(coords))
                {
                    if (user.Map.Doors[coords].Closed)
                        user.SendMessage("It's open.", 0x1);
                    else
                        user.SendMessage("It's closed.", 0x1);

                    user.Map.ToggleDoors(x, y);
                }
                else if (user.Map.Signposts.ContainsKey(coords))
                {
                    user.Map.Signposts[coords].OnClick(user);
                }
                else
                {
                    Logger.DebugFormat("User clicked {0}@{1},{2} but no door/signpost is present",
                        user.Map.Name, x, y);
                }
            }

            // User has clicked on another entity
            else if (clickType == 1)
            {
                var entityId = packet.ReadUInt32();
                Logger.DebugFormat("User {0} clicked ID {1}: ", user.Name, entityId);

                WorldObject clickTarget = new WorldObject();

                if (user.World.Objects.TryGetValue(entityId, out clickTarget))
                {
                    if (clickTarget is User || clickTarget is Merchant)
                    {
                        Type type = clickTarget.GetType();
                        MethodInfo methodInfo = type.GetMethod("OnClick");
                        methodInfo.Invoke(clickTarget, new[] { user });
                    }
                }
            }
            else
            {
                Logger.DebugFormat("Unsupported clickType {0}", clickType);
                Logger.DebugFormat("Packet follows:");
                packet.DumpPacket();
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x44_EquippedItemClick(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            // This packet is received when a client unequips an item from the detail (a) screen.

            var slot = packet.ReadByte();

            Logger.DebugFormat("Removing equipment from slot {0}", slot);
            var item = user.Equipment[slot];
            if (item != null)
            {
                Logger.DebugFormat("actually removing item");
                user.RemoveEquipment(slot);
                // Add our removed item to our first empty inventory slot
                Logger.DebugFormat("Player weight is currently {0}", user.CurrentWeight);
                Logger.DebugFormat("Adding item {0}, count {1} to inventory", item.Name, item.Count);
                user.AddItem(item);
                Logger.DebugFormat("Player weight is now {0}", user.CurrentWeight);
            }
            else
            {
                Logger.DebugFormat("Ignoring useless click on slot {0}", slot);
                return;
            }
        }

        private void PacketHandler_0x45_ByteHeartbeat(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            // Client sends 0x45 response in the reverse order of what the server sends...
            var byteB = packet.ReadByte();
            var byteA = packet.ReadByte();

            if (!user.IsHeartbeatValid(byteA, byteB))
            {
                Logger.InfoFormat("{0}: byte heartbeat not valid, disconnecting", user.Name);
                user.SendRedirectAndLogoff(Game.World, Game.Login, user.Name);
            }
            else
            {
                Logger.DebugFormat("{0}: byte heartbeat valid", user.Name);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x47_StatPoint(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            if (user.LevelPoints > 0)
            {
                switch (packet.ReadByte())
                {
                    case 0x01:
                        user.BaseStr++;
                        break;

                    case 0x04:
                        user.BaseInt++;
                        break;

                    case 0x08:
                        user.BaseWis++;
                        break;

                    case 0x10:
                        user.BaseCon++;
                        break;

                    case 0x02:
                        user.BaseDex++;
                        break;

                    default:
                        return;
                }

                user.LevelPoints--;
                user.UpdateAttributes(StatUpdateFlags.Primary);
            }
        }

        [ProhibitedCondition(PlayerCondition.InComa)]
        [ProhibitedCondition(PlayerCondition.Asleep)]
        [ProhibitedCondition(PlayerCondition.Frozen)]
        [RequiredCondition(PlayerCondition.Alive)]
        private void PacketHandler_0x4A_Trade(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var tradeStage = packet.ReadByte();

            if (tradeStage == 0 && user.ActiveExchange != null)
                return;

            if (tradeStage != 0 && user.ActiveExchange == null)
                return;

            if (user.ActiveExchange != null && !user.ActiveExchange.ConditionsValid)
                return;

            switch (tradeStage)
            {
                case 0x00:
                    {
                        // Starting trade
                        var x0PlayerId = packet.ReadInt32();

                        WorldObject target;
                        if (Objects.TryGetValue((uint)x0PlayerId, out target))
                        {
                            if (target is User)
                            {
                                var playerTarget = (User)target;

                                if (Exchange.StartConditionsValid(user, playerTarget))
                                {
                                    user.SendMessage("That can't be done right now.", MessageTypes.SYSTEM);
                                    return;
                                }
                                // Initiate exchange
                                var exchange = new Exchange(user, playerTarget);
                                exchange.StartExchange();
                            }
                        }
                    }
                    break;

                case 0x01:
                    // Add item to trade
                    {
                        // We ignore playerId because we only allow one exchange at a time and we
                        // keep track of the participants on both sides
                        var x1playerId = packet.ReadInt32();
                        var x1ItemSlot = packet.ReadByte();
                        if (user.Inventory[x1ItemSlot] != null && user.Inventory[x1ItemSlot].Count > 1)
                        {
                            // Send quantity request
                            user.SendExchangeQuantityPrompt(x1ItemSlot);
                        }
                        else
                            user.ActiveExchange.AddItem(user, x1ItemSlot);
                    }
                    break;

                case 0x02:
                    // Add item with quantity
                    var x2PlayerId = packet.ReadInt32();
                    var x2ItemSlot = packet.ReadByte();
                    var x2ItemQuantity = packet.ReadByte();
                    user.ActiveExchange.AddItem(user, x2ItemSlot, x2ItemQuantity);
                    break;

                case 0x03:
                    // Add gold to trade
                    var x3PlayerId = packet.ReadInt32();
                    var x3GoldQuantity = packet.ReadUInt32();
                    user.ActiveExchange.AddGold(user, x3GoldQuantity);
                    break;

                case 0x04:
                    // Cancel trade
                    Logger.Debug("Cancelling trade");
                    user.ActiveExchange.CancelExchange(user);
                    break;

                case 0x05:
                    // Confirm trade
                    Logger.Debug("Confirming trade");
                    user.ActiveExchange.ConfirmExchange(user);
                    break;

                default:
                    return;
            }
        }

        private void PacketHandler_0x4D_BeginCasting(object obj, ClientPacket packet)
        {
            var user = (User) obj;
            user.Status ^= PlayerCondition.Casting;
        }

        private void PacketHandler_0x4E_CastLine(object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var textLength = packet.ReadByte();
            var text = packet.Read(textLength);

            var x0D = new ServerPacketStructures.CastLine() {ChatType = 2, LineLength = textLength, LineText = Encoding.UTF8.GetString(text), TargetId = user.Id};
            var enqueue = x0D.Packet();
            user.Enqueue(enqueue);

        }

        private void PacketHandler_0x4F_ProfileTextPortrait(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var totalLength = packet.ReadUInt16();
            var portraitLength = packet.ReadUInt16();
            var portraitData = packet.Read(portraitLength);
            var profileText = packet.ReadString16();

            user.PortraitData = portraitData;
            user.ProfileText = profileText;
        }

        private void PacketHandler_0x75_TickHeartbeat(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var serverTick = packet.ReadInt32();
            var clientTick = packet.ReadInt32(); // Dunno what to do with this right now, so we just store it

            if (!user.IsHeartbeatValid(serverTick, clientTick))
            {
                Logger.InfoFormat("{0}: tick heartbeat not valid, disconnecting", user.Name);
                user.SendRedirectAndLogoff(Game.World, Game.Login, user.Name);
            }
            else
            {
                Logger.DebugFormat("{0}: tick heartbeat valid", user.Name);
            }
        }

        private void PacketHandler_0x79_Status(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var status = packet.ReadByte();
            if (status <= 7)
            {
                user.GroupStatus = (UserStatus)status;
            }
        }

        private void PacketHandler_0x7B_RequestMetafile(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var all = packet.ReadBoolean();

            if (all)
            {
                var x6F = new ServerPacket(0x6F);
                x6F.WriteBoolean(all);
                x6F.WriteUInt16((ushort)Metafiles.Count);
                foreach (var metafile in Metafiles.Values)
                {
                    x6F.WriteString8(metafile.Name);
                    x6F.WriteUInt32(metafile.Checksum);
                }
                user.Enqueue(x6F);
            }
            else
            {
                var name = packet.ReadString8();
                if (Metafiles.ContainsKey(name))
                {
                    var file = Metafiles[name];

                    var x6F = new ServerPacket(0x6F);
                    x6F.WriteBoolean(all);
                    x6F.WriteString8(file.Name);
                    x6F.WriteUInt32(file.Checksum);
                    x6F.WriteUInt16((ushort)file.Data.Length);
                    x6F.Write(file.Data);
                    user.Enqueue(x6F);
                }
            }
        }

        #endregion Packet Handlers

        #region Merchant Menu ItemObject Handlers

        private void MerchantMenuHandler_MainMenu(User user, Merchant merchant, ClientPacket packet)
        {
            merchant.DisplayPursuits(user);
        }

        private void MerchantMenuHandler_BuyItemMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowBuyMenu(merchant);
        }

        private void MerchantMenuHandler_SellItemMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowSellMenu(merchant);
        }

        private void MerchantMenuHandler_BuyItem(User user, Merchant merchant, ClientPacket packet)
        {
            string name = packet.ReadString8();

            if (!merchant.Inventory.ContainsKey(name))
            {
                user.ShowMerchantGoBack(merchant, "I do not sell that item.", MerchantMenuItem.BuyItemMenu);
                return;
            }

            var template = merchant.Inventory[name];

            if (template.Stackable)
            {
                user.ShowBuyMenuQuantity(merchant, name);
                return;
            }

            if (user.Gold < template.Properties.Physical.Value)
            {
                user.ShowMerchantGoBack(merchant, "You do not have enough gold.", MerchantMenuItem.BuyItemMenu);
                return;
            }

            if (user.CurrentWeight + template.Properties.Physical.Weight > user.MaximumWeight)
            {
                user.ShowMerchantGoBack(merchant, "That item is too heavy for you to carry.",
                    MerchantMenuItem.BuyItemMenu);
                return;
            }

            if (user.Inventory.IsFull)
            {
                user.ShowMerchantGoBack(merchant, "You cannot carry any more items.", MerchantMenuItem.BuyItemMenu);
                return;
            }

            user.RemoveGold(template.Properties.Physical.Value);
            var item = CreateItem(template.Id);
            Insert(item);
            user.AddItem(item);

            user.UpdateAttributes(StatUpdateFlags.Experience);
            user.ShowBuyMenu(merchant);
        }

        private void MerchantMenuHandler_BuyItemWithQuantity(User user, Merchant merchant, ClientPacket packet)
        {
            string name = packet.ReadString8();
            string qStr = packet.ReadString8();

            if (!merchant.Inventory.ContainsKey(name))
            {
                user.ShowMerchantGoBack(merchant, "I do not sell that item.", MerchantMenuItem.BuyItemMenu);
                return;
            }

            var template = merchant.Inventory[name];

            if (!template.Stackable) return;

            int quantity;
            if (!int.TryParse(qStr, out quantity) || quantity < 1)
            {
                user.ShowBuyMenuQuantity(merchant, name);
                return;
            }

            uint cost = (uint)(template.Properties.Physical.Value * quantity);

            if (user.Gold < cost)
            {
                user.ShowMerchantGoBack(merchant, "You do not have enough gold.", MerchantMenuItem.BuyItemMenu);
                return;
            }

            if (quantity > template.Properties.Stackable.Max)
            {
                user.ShowMerchantGoBack(merchant, string.Format("You cannot hold that many {0}.", name),
                    MerchantMenuItem.BuyItemMenu);
                return;
            }

            if (user.Inventory.Contains(name))
            {
                byte slot = user.Inventory.SlotOf(name);
                if (user.Inventory[slot].Count + quantity > template.Properties.Stackable.Max)
                {
                    user.ShowMerchantGoBack(merchant, string.Format("You cannot hold that many {0}.", name),
                        MerchantMenuItem.BuyItemMenu);
                    return;
                }
                user.IncreaseItem(slot, quantity);
            }
            else
            {
                if (user.Inventory.IsFull)
                {
                    user.ShowMerchantGoBack(merchant, "You cannot carry any more items.", MerchantMenuItem.BuyItemMenu);
                    return;
                }

                var item = CreateItem(template.Id, quantity);
                Insert(item);
                user.AddItem(item);
            }

            user.RemoveGold(cost);
            user.UpdateAttributes(StatUpdateFlags.Experience);
            user.ShowBuyMenu(merchant);
        }

        private void MerchantMenuHandler_SellItem(User user, Merchant merchant, ClientPacket packet)
        {
            byte slot = packet.ReadByte();

            var item = user.Inventory[slot];
            if (item == null) return;

            if (!merchant.Inventory.ContainsKey(item.Name))
            {
                user.ShowMerchantGoBack(merchant, "I do not want that item.", MerchantMenuItem.SellItemMenu);
                return;
            }

            if (item.Stackable && item.Count > 1)
            {
                user.ShowSellQuantity(merchant, slot);
                return;
            }

            user.ShowSellConfirm(merchant, slot, 1);
        }

        private void MerchantMenuHandler_SellItemWithQuantity(User user, Merchant merchant, ClientPacket packet)
        {
            packet.ReadByte();
            byte slot = packet.ReadByte();
            string qStr = packet.ReadString8();

            int quantity;
            if (!int.TryParse(qStr, out quantity) || quantity < 1)
            {
                user.ShowSellQuantity(merchant, slot);
                return;
            }

            var item = user.Inventory[slot];
            if (item == null || !item.Stackable) return;

            if (!merchant.Inventory.ContainsKey(item.Name))
            {
                user.ShowMerchantGoBack(merchant, "I do not want that item.", MerchantMenuItem.SellItemMenu);
                return;
            }

            if (item.Count < quantity)
            {
                user.ShowMerchantGoBack(merchant, "You don't have that many to sell.", MerchantMenuItem.SellItemMenu);
                return;
            }

            user.ShowSellConfirm(merchant, slot, quantity);
        }

        private void MerchantMenuHandler_SellItemConfirmation(User user, Merchant merchant, ClientPacket packet)
        {
            packet.ReadByte();
            byte slot = packet.ReadByte();
            byte quantity = packet.ReadByte();

            var item = user.Inventory[slot];
            if (item == null) return;

            if (!merchant.Inventory.ContainsKey(item.Name))
            {
                user.ShowMerchantGoBack(merchant, "I do not want that item.", MerchantMenuItem.SellItemMenu);
                return;
            }

            if (item.Count < quantity)
            {
                user.ShowMerchantGoBack(merchant, "You don't have that many to sell.", MerchantMenuItem.SellItemMenu);
                return;
            }

            uint profit = (uint)(Math.Round(item.Value * 0.50) * quantity);

            if (item.Stackable && quantity < item.Count)
                user.DecreaseItem(slot, quantity);
            else user.RemoveItem(slot);

            user.AddGold(profit);

            merchant.DisplayPursuits(user);
        }

        private void MerchantMenuHandler_LearnSkill(User user, Merchant merchant, ClientPacket packet)
        {
            
        }
        private void MerchantMenuHandler_LearnSkillAccept(User user, Merchant merchant, ClientPacket packet)
        {

        }

        private void MerchantMenuHandler_LearnSpell(User user, Merchant merchant, ClientPacket packet)
        {
            
        }
        private void MerchantMenuHandler_LearnSpellAccept(User user, Merchant merchant, ClientPacket packet)
        {

        }
        private void MerchantMenuHandler_ForgetSkill(User user, Merchant merchant, ClientPacket packet)
        {
            
        }
        private void MerchantMenuHandler_ForgetSkillAccept(User user, Merchant merchant, ClientPacket packet)
        {

        }
        private void MerchantMenuHandler_ForgetSpell(User user, Merchant merchant, ClientPacket packet)
        {
            
        }
        private void MerchantMenuHandler_ForgetSpellAccept(User user, Merchant merchant, ClientPacket packet)
        {

        }
        #endregion Merchant Menu ItemObject Handlers

        public void Insert(WorldObject obj)
        {
            if (obj is User)
            {
                AddUser((User)obj);
            }

            ++worldObjectID;
            obj.Id = worldObjectID;
            obj.World = this;
            obj.SendId();

            if (obj is ItemObject)
            {
                var itemscript = Game.World.ScriptProcessor.GetScript(obj.Name);
                if (itemscript != null)
                {
                    var clone = itemscript.Clone();
                    itemscript.AssociateScriptWithObject(obj);
                }
            }

            Objects.Add(worldObjectID, obj);
        }

        public void Remove(WorldObject obj)
        {
            if (obj is User)
            {
                DeleteUser(obj.Name);
            }
            Objects.Remove(obj.Id);
            obj.World = null;
            obj.Id = 0;
        }

        public void Update()
        {
        }

        public ItemObject CreateItem(int id, int quantity = 1)
        {
            if (Items.ContainsKey(id))
            {
                var item = new ItemObject(id, this);
                if (quantity > item.MaximumStack)
                    quantity = item.MaximumStack;
                item.Count = Math.Max(quantity, 1);
                return item;
            }
            else
            {
                return null;
            }
        }

        public bool TryGetItemTemplate(string name, Sex itemSex, out Item item)
        {
            var itemKey = new Tuple<Sex, String>(itemSex, name);
            return ItemCatalog.TryGetValue(itemKey, out item);
        }

        public bool TryGetItemTemplate(string name, out Item item)
        {
            // This is kinda gross
            var neutralKey = new Tuple<Sex, String>(Sex.Neutral, name);
            var femaleKey = new Tuple<Sex, String>(Sex.Female, name);
            var maleKey = new Tuple<Sex, String>(Sex.Male, name);

            return ItemCatalog.TryGetValue(neutralKey, out item) || ItemCatalog.TryGetValue(femaleKey, out item) || ItemCatalog.TryGetValue(maleKey, out item);
        }

        public object ScriptMethod(string name, params object[] args)
        {
            object result = null;

            if (Methods.ContainsKey(name))
            {
                var method = Methods[name];
                result = method.Invoke(null, args);
            }

            return result;
        }

        private void QueueConsumer()
        {
            while (!MessageQueue.IsCompleted)
            {
                if (StopToken.IsCancellationRequested)
                    return;
                // Process messages.
                HybrasylMessage message;
                User user;
                try
                {
                    message = MessageQueue.Take();
                }
                catch (InvalidOperationException)
                {
                    Logger.ErrorFormat("QUEUE CONSUMER: EXCEPTION RAISED");
                    continue;
                }

                if (message != null)
                {
                    if (message is HybrasylClientMessage)
                    {
                        var clientMessage = (HybrasylClientMessage)message;
                        var handler = PacketHandlers[clientMessage.Packet.Opcode];
                        try
                        {
                            if (ActiveUsers.TryGetValue(clientMessage.ConnectionId, out user))
                            {
                                // Check if the action is prohibited due to statuses
                                MethodBase method = handler.GetMethod();
                                bool ignore = false;
                                foreach (var prohibited in method.GetCustomAttributes(typeof(ProhibitedCondition), true))
                                {
                                    var prohibitedCondition = prohibited as ProhibitedCondition;
                                    if (prohibitedCondition == null) continue;
                                    Logger.InfoFormat($"{user.Status} : {prohibitedCondition.Condition} : {user.Status.HasFlag(prohibitedCondition.Condition)}");
                                    if (!user.Status.HasFlag(prohibitedCondition.Condition)) continue;
                                    user.SendSystemMessage(Constants.STATUS_RESTRICTION_MESSAGES[prohibitedCondition.Condition]);
                                    ignore = true;
                                }
                                foreach (var required in method.GetCustomAttributes(typeof(RequiredCondition), true))
                                {
                                    var requiredCondition = required as ProhibitedCondition;
                                    if (requiredCondition == null) continue;
                                    if (user.Status.HasFlag(requiredCondition.Condition)) continue;
                                    user.SendSystemMessage(Constants.STATUS_RESTRICTION_MESSAGES[requiredCondition.Condition]);
                                    ignore = true;
                                }

                                // If we are in an exchange, we should only receive exchange packets and the
                                // occasional heartbeat. If we receive anything else, just kill the exchange.
                                if (user.ActiveExchange != null && (clientMessage.Packet.Opcode != 0x4a &&
                                    clientMessage.Packet.Opcode != 0x45 && clientMessage.Packet.Opcode != 0x75))
                                    user.ActiveExchange.CancelExchange(user);
                                if (ignore)
                                {
                                    if (clientMessage.Packet.Opcode == 0x06) user.Refresh();
                                    continue;
                                }
                                // Last but not least, invoke the handler
                                handler.Invoke(user, clientMessage.Packet);
                            }
                            else if (clientMessage.Packet.Opcode == 0x10) // Handle special case of join world
                            {
                                PacketHandlers[0x10].Invoke(clientMessage.ConnectionId, clientMessage.Packet);
                            }
                            else
                            {
                                // We received a packet for a dead connection...?
                                Logger.WarnFormat(
                                    "Connection ID {0}: received packet, but seems to be dead connection?",
                                    clientMessage.ConnectionId);
                                continue;
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error("Exception encountered in packet handler!", e);
                        }
                    }
                    else if (message is HybrasylControlMessage)
                    {
                        //   try
                        // {
                        var controlMessage = (HybrasylControlMessage)message;
                        ControlMessageHandlers[controlMessage.Opcode].Invoke(controlMessage);
                        //}
                        //catch (Exception e)
                        // {
                        //   Logger.Error("Exception encountered in control message handler!", e);
                        //}
                    }
                }
            }
            Logger.WarnFormat("Message queue is complete..?");
        }

        public void StartQueueConsumer()
        {
            // Start our consumer
            ConsumerThread = new Thread(QueueConsumer);
            if (ConsumerThread.IsAlive) return;
            ConsumerThread.Start();
            Logger.InfoFormat("Consumer thread: started");
        }

        public void StopQueueConsumer()
        {
            // Mark the message queue as not accepting additions, which will result in thread termination
            MessageQueue.CompleteAdding();
        }

        public void StartTimers()
        {
            var jobList =
                Assembly.GetExecutingAssembly().GetTypes().ToList().Where(t => t.Namespace == "Hybrasyl.Jobs").ToList();

            foreach (var jobClass in jobList)
            {
                var executeMethod = jobClass.GetMethod("Execute");
                if (executeMethod != null)
                {
                    var aTimer = new System.Timers.Timer();
                    aTimer.Elapsed +=
                        (ElapsedEventHandler)Delegate.CreateDelegate(typeof(ElapsedEventHandler), executeMethod);
                    // Interval is set to whatever is in the class
                    var interval = jobClass.GetField("Interval").GetValue(null);

                    if (interval == null)
                    {
                        Logger.ErrorFormat("Job class {0} has no Interval defined! Job will not be scheduled.");
                        continue;
                    }

                    aTimer.Interval = ((int)interval) * 1000; // Interval is in ms; interval in Job classes is s

                    Logger.InfoFormat("Hybrasyl: timer loaded for job {0}: interval {1}", jobClass.Name, aTimer.Interval);
                    aTimer.Enabled = true;
                    aTimer.Start();
                }
                else
                {
                    Logger.ErrorFormat("Job class {0} has no Execute method! Job will not be scheduled.", jobClass.Name);
                }
            }
        }
    }
}