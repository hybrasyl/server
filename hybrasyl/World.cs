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
using Hybrasyl.Scripting;
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

            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
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

            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            using (MemoryStream memoryStream = new MemoryStream(stream))
            {
                T result = (T)binaryFormatter.Deserialize(memoryStream);
                return result;
            }
        }
    }

    public partial class World : Server
    {
        private static uint worldObjectID = 0;

        public static DateTime StartDate => Game.Config.Time != null ? Game.Config.Time.StartDate : Game.StartDate;

        public new static ILog Logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        
        public Dictionary<uint, WorldObject> Objects { get; set; }

        public Dictionary<string, string> Portraits { get; set; }
        public Strings Strings { get; set; }
        public WorldDataStore WorldData { set; get;  }
      
        public Nation DefaultNation
        {
            get
            {
                var nation = WorldData.Values<Nation>().FirstOrDefault(n => n.Default);
                return nation ?? WorldData.Values<Nation>().First();
            }
        }

        public List<DialogSequence> GlobalSequences { get; set; }
        public Dictionary<string, DialogSequence> GlobalSequencesCatalog { get; set; }
        private Dictionary<MerchantMenuItem, MerchantMenuHandler> merchantMenuHandlers;

        public Dictionary<Tuple<Sex, string>, Item> ItemCatalog { get; set; }
       // public Dictionary<string, Map> MapCatalog { get; set; }

        public ScriptProcessor ScriptProcessor { get; set; }

        public static BlockingCollection<HybrasylMessage> MessageQueue;
        public static ConcurrentDictionary<long, User> ActiveUsers { get; private set; }
        public ConcurrentDictionary<string, long> ActiveUsersByName { get; set; }

        private Thread ConsumerThread { get; set; }
        

        public Login Login { get; private set; }

        private static Random _random;

        private static Lazy<ConnectionMultiplexer> _lazyConnector;

        public static ConnectionMultiplexer DatastoreConnection => _lazyConnector.Value;

        #region Path helpers
        public static string DataDirectory => Constants.DataDirectory;

        public static string MapFileDirectory => Path.Combine(DataDirectory, "world", "mapfiles");

        public static string ScriptDirectory => Path.Combine(DataDirectory, "world", "scripts");

        public static string CastableDirectory => Path.Combine(DataDirectory, "world", "xml", "castables");
        public static string StatusDirectory => Path.Combine(DataDirectory, "world", "xml", "statuses");

        public static string ItemDirectory => Path.Combine(DataDirectory, "world", "xml", "items");

        public static string NationDirectory => Path.Combine(DataDirectory, "world", "xml", "nations");

        public static string MapDirectory => Path.Combine(DataDirectory, "world", "xml", "maps");

        public static string WorldMapDirectory => Path.Combine(DataDirectory, "world", "xml", "worldmaps");

        public static string CreatureDirectory => Path.Combine(DataDirectory, "world", "xml", "creatures");

        public static string SpawnGroupDirectory => Path.Combine(DataDirectory, "world", "xml", "spawngroups");

        public static string ItemVariantDirectory => Path.Combine(DataDirectory, "world", "xml", "itemvariants");

        public static string NpcsDirectory => Path.Combine(DataDirectory, "world", "xml", "npcs");

        public static string LocalizationDirectory => Path.Combine(DataDirectory, "world", "xml", "localization");
        #endregion

        public static bool TryGetUser(string name, out User userobj)
        {
            var jsonstring = (string)DatastoreConnection.GetDatabase().Get(User.GetStorageKey(name));
            if (jsonstring == null)
            {
                userobj = null;
                return false;
            }
            userobj = JsonConvert.DeserializeObject<User>(jsonstring);
            if (userobj == null)
            {
                Logger.FatalFormat("{0}: JSON object could not be deserialized!", name);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Register world throttles. This should eventually use XML configuration; for now it simply
        /// registers our hardcoded throttle values.
        /// </summary>
        public void RegisterWorldThrottles()
        {
            RegisterPacketThrottle(new GenericPacketThrottle(0x06, 250, 0, 500));  // Movement
           // RegisterThrottle(new SpeechThrottle(0x0e, 250, 3, 10000, 10000, 200, 250, 6, 2000, 4000, 200)); // speech
            RegisterPacketThrottle(new GenericPacketThrottle(0x3a, 600, 1000, 500));  // NPC use dialog
            RegisterPacketThrottle(new GenericPacketThrottle(0x38, 600, 0, 500));  // refresh (f5)
            RegisterPacketThrottle(new GenericPacketThrottle(0x39, 600, 1000, 500));  // NPC main menu
            RegisterPacketThrottle(new GenericPacketThrottle(0x13, 800, 0, 0));        // Assail
        }


        public World(int port, DataStore store)
            : base(port)
        {
            Objects = new Dictionary<uint, WorldObject>();
            Portraits = new Dictionary<string, string>();

            GlobalSequencesCatalog = new Dictionary<string, DialogSequence>();
            ItemCatalog = new Dictionary<Tuple<Sex, string>, Item>();
            //MapCatalog = new Dictionary<string, Map>();

            ScriptProcessor = new ScriptProcessor(this);
            MessageQueue = new BlockingCollection<HybrasylMessage>(new ConcurrentQueue<HybrasylMessage>());
            ActiveUsers = new ConcurrentDictionary<long, User>();
            ActiveUsersByName = new ConcurrentDictionary<string, long>();

            WorldData = new WorldDataStore();

            var datastoreConfig = new ConfigurationOptions()
            {
                EndPoints =
                {
                    {store.Host, store.Port}
                }
            };

            if (!string.IsNullOrEmpty(store.Password))
                datastoreConfig.Password = store.Password;

            _lazyConnector = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(datastoreConfig));
            _random = new Random();
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
            RegisterWorldThrottles();
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

            //Load strings
            foreach (var xml in Directory.GetFiles(LocalizationDirectory, "*.xml"))
            {              
                try
                {
                    Strings = Serializer.Deserialize(XmlReader.Create(xml), new Strings());
                    Logger.Debug("Localization strings loaded.");
                }
                catch (Exception e)
                {
                    Logger.Error($"Error parsing {xml}: {e}");
                }
            }

            //Load NPCs
            foreach (var xml in Directory.GetFiles(NpcsDirectory, "*.xml"))
            {
                try
                {
                    var npc = Serializer.Deserialize(XmlReader.Create(xml), new Creatures.Npc());
                    Logger.Debug($"NPCs: loaded {npc.Name}");
                    WorldData.Set(npc.Name, npc);
                }
                catch (Exception e)
                {
                    Logger.Error($"Error parsing {xml}: {e}");
                }
            }

            // Load maps
            foreach (var xml in Directory.GetFiles(MapDirectory, "*.xml"))
            {
                try
                {
                    Maps.Map newMap = Serializer.Deserialize(XmlReader.Create(xml), new Maps.Map());
                    var map = new Map(newMap, this);
                    //Maps.Add(map.Id, map);
                    //MapCatalog.Add(map.Name, map);
                    WorldData.SetWithIndex(map.Id, map, map.Name);
                    Logger.DebugFormat("Maps: Loaded {0}", map.Name);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            Logger.InfoFormat("Maps: {0} maps loaded", WorldData.Count<Map>());

            // Load nations
            foreach (var xml in Directory.GetFiles(NationDirectory, "*.xml"))
            {
                try
                {
                    var newNation = Serializer.Deserialize(XmlReader.Create(xml), new Nation());
                    Logger.DebugFormat("Nations: Loaded {0}", newNation.Name);
                    //Nations.Add(newNation.Name, newNation);
                    WorldData.Set(newNation.Name, newNation);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            // Ensure at least one nation and one map exist. Otherwise, things get a little weird
            if (WorldData.Count<Nation>() == 0)
            {
                Logger.FatalFormat("National data: at least one well-formed nation file must exist!");
                return false;
            }

            if (WorldData.Count<Map>() == 0)
            {
                Logger.FatalFormat("Map data: at least one well-formed map file must exist!");
                return false;
            }

            Logger.InfoFormat("National data: {0} nations loaded", WorldData.Count<Nation>());

            //Load Creatures
            foreach (var xml in Directory.GetFiles(CreatureDirectory, "*.xml"))
            {
                try
                {
                    var creature = Serializer.Deserialize(XmlReader.Create(xml), new Creatures.Creature());
                    Logger.DebugFormat("Creatures: loaded {0}", creature.Name);
                    WorldData.Set(creature.Name, creature);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }



            //Load SpawnGroups
            foreach (var xml in Directory.GetFiles(SpawnGroupDirectory, "*.xml"))
            {
                try
                {
                    var spawnGroup = Serializer.Deserialize(XmlReader.Create(xml), new SpawnGroup());
                    spawnGroup.Filename = Path.GetFileName(xml);
                    Logger.DebugFormat("SpawnGroup: loaded {0}", spawnGroup.GetHashCode());
                    WorldData.Set(spawnGroup.GetHashCode(), spawnGroup);


                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            // Load worldmaps
            foreach (var xml in Directory.GetFiles(WorldMapDirectory, "*.xml"))
            {
                try
                {
                    Maps.WorldMap newWorldMap = Serializer.Deserialize(XmlReader.Create(xml), new Maps.WorldMap());
                    var worldmap = new WorldMap(newWorldMap);
                    WorldData.Set(worldmap.Name, worldmap);
                    foreach (var point in worldmap.Points)
                    {
                        WorldData.Set(point.Id, point);
                    }
                    Logger.DebugFormat("World Maps: Loaded {0}", worldmap.Name);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            Logger.InfoFormat("World Maps: {0} world maps loaded", WorldData.Count<WorldMap>());

            // Load item variants
            foreach (var xml in Directory.GetFiles(ItemVariantDirectory, "*.xml"))
            {
                try
                {
                    Items.VariantGroup newGroup = Serializer.Deserialize(XmlReader.Create(xml), new Items.VariantGroup());
                    Logger.DebugFormat("Item variants: loaded {0}", newGroup.Name);
                    WorldData.Set(newGroup.Name, newGroup);

                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            Logger.InfoFormat("ItemObject variants: {0} variant sets loaded", WorldData.Values<VariantGroup>().Count());

            // Load items
            foreach (var xml in Directory.GetFiles(ItemDirectory, "*.xml"))
            {
                try
                {
                    Item newItem = Serializer.Deserialize(XmlReader.Create(xml), new Item());
                    Logger.DebugFormat("Items: loaded {0}, id {1}", newItem.Name, newItem.Id);
                    WorldData.SetWithIndex(newItem.Id, newItem,  newItem.Name);
                    // Handle some null cases; there's probably a nicer way to do this
                    if (newItem.Properties.StatModifiers.Combat == null) { newItem.Properties.StatModifiers.Combat = new StatModifierCombat(); }
                    if (newItem.Properties.StatModifiers.Element == null) { newItem.Properties.StatModifiers.Element = new StatModifierElement(); }
                    if (newItem.Properties.StatModifiers.Base == null) { newItem.Properties.StatModifiers.Base = new StatModifierBase(); }
                    if (newItem.Properties.Variants != null)
                    {
                        foreach (var targetGroup in newItem.Properties.Variants.Group)
                        {
                            foreach (var variant in WorldData.Get<VariantGroup>(targetGroup).Variant)
                            {
                                var variantItem = ResolveVariant(newItem, variant, targetGroup);
                                Logger.DebugFormat("ItemObject {0}: variantgroup {1}, subvariant {2}", variantItem.Name, targetGroup, variant.Name);
                                if (WorldData.ContainsKey<Item>(variantItem.Id))
                                {
                                    Logger.ErrorFormat("Item already exists with Key {0} : {1}. Cannot add {2}", variantItem.Id, WorldData.Get<Item>(variantItem.Id).Name, variantItem.Name);
                                }
                                WorldData.SetWithIndex(variantItem.Id, variantItem,
                                     new Tuple<Sex, string>(Sex.Neutral, variantItem.Name));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            foreach (var xml in Directory.GetFiles(StatusDirectory, "*.xml"))
            {
                try
                {
                    string name = string.Empty;
                    Statuses.Status newStatus = Serializer.Deserialize(XmlReader.Create(xml), new Statuses.Status());
                    WorldData.SetWithIndex(newStatus.Id, newStatus, newStatus.Name);
                    Logger.Debug($"Statuses: loaded {newStatus.Name}, id {newStatus.Id}");
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            
            }
            foreach (var xml in Directory.GetFiles(CastableDirectory, "*.xml"))
            {
                try
                {
                    string name = string.Empty;
                    Castables.Castable newCastable = Serializer.Deserialize(XmlReader.Create(xml), new Castables.Castable());
                    WorldData.SetWithIndex(newCastable.Id, newCastable, newCastable.Name);
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
                var jsonstring = (string)World.DatastoreConnection.GetDatabase().Get(key);
                var mailbox = JsonConvert.DeserializeObject<Mailbox>(jsonstring);
                var name = key.ToString().Split(':')[1].ToLower();
                if (name == string.Empty)
                {
                    Logger.Warn("Potentially corrupt mailbox data in Redis; ignoring");
                    continue;
                }
                //Mailboxes.Add(name, mailbox);
                WorldData.Set(name, mailbox);
            }

            // Load all boards
            foreach (var key in server.Keys(pattern: "Hybrasyl.Board*"))
            {
                Logger.InfoFormat("Loading board at {0}", key);
                var jsonstring = (string)World.DatastoreConnection.GetDatabase().Get(key);
                var messageboard = JsonConvert.DeserializeObject<Board>(jsonstring);
                var name = key.ToString().Split(':')[1];
                if (name == string.Empty)
                {
                    Logger.Warn("Potentially corrupt board data in Redis; ignoring");
                    continue;
                }
                // Messageboard IDs are fairly irrelevant and only matter to the client
                messageboard.Id = WorldData.Count<Board>() + 1;
                //Messageboards.Add(messageboard.Name, messageboard);
                WorldData.SetWithIndex(messageboard.Name, messageboard, messageboard.Id);
                //MessageboardIndex.Add(messageboard.Id, messageboard);
            }

            // Ensure global boards exist and are up to date with anything specified in the config

            if (Game.Config.Boards != null)
            {
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
            }
            return true;
        }

        public Item ResolveVariant(Item item, Items.Variant variant, string variantGroup)
        {
            var variantItem = item.Clone();

            variantItem.Name = $"{variant.Modifier} {item.Name}";
            Logger.Debug($"Processing variant: {variantItem.Name}");
            variantItem.Properties.Flags = variant.Properties.Flags;
                    
            variantItem.Properties.Physical.Value = variant.Properties.Physical.Value == 100 ? item.Properties.Physical.Value : Convert.ToUInt32(Math.Round(item.Properties.Physical.Value * (variant.Properties.Physical.Value * .01)));
            variantItem.Properties.Physical.Durability = variant.Properties.Physical.Durability == 100 ? item.Properties.Physical.Durability : Convert.ToUInt32(Math.Round(item.Properties.Physical.Durability * (variant.Properties.Physical.Durability * .01)));
            variantItem.Properties.Physical.Weight = variant.Properties.Physical.Weight == 100 ? item.Properties.Physical.Weight : Convert.ToInt32(Math.Round(item.Properties.Physical.Weight * (variant.Properties.Physical.Weight * .01)));

            // Ensure all our modifiable / referenced properties at least exist
            // TODO: this is pretty hacky
            if (variantItem.Properties.Restrictions.Level is null)
                variantItem.Properties.Restrictions.Level = new RestrictionsLevel();

            if (variantItem.Properties.StatModifiers is null)
                variantItem.Properties.StatModifiers = new StatModifiers()
                {
                    Base = new StatModifierBase(),
                    Element = new StatModifierElement(),
                    Combat = new StatModifierCombat()
                };

            if (variantItem.Properties.Damage is null)
            {
                variantItem.Properties.Damage = new Items.Damage()
                {
                    Large = new DamageLarge(),
                    Small = new DamageSmall()
                };
            }

            if (variantItem.Properties.Damage.Large is null)
                variantItem.Properties.Damage.Large = new DamageLarge();

            if (variantItem.Properties.Damage.Small is null)
                variantItem.Properties.Damage.Small = new DamageSmall();

            if (item.Properties.Damage is null)
            {
                item.Properties.Damage = new Items.Damage()
                {
                    Large = new DamageLarge(),
                    Small = new DamageSmall()
                };
            }

            switch (variantGroup.ToLower())
            {
                case "consecratable":
                    {
                        if (variant.Properties.Restrictions?.Level != null) variantItem.Properties.Restrictions.Level.Min += variant.Properties.Restrictions.Level.Min;
                        if (variant.Properties.StatModifiers?.Base != null)
                        {
                            variantItem.Properties.StatModifiers.Base.Dex += variant.Properties.StatModifiers.Base.Dex;
                            variantItem.Properties.StatModifiers.Base.Con += variant.Properties.StatModifiers.Base.Con;
                            variantItem.Properties.StatModifiers.Base.Str += variant.Properties.StatModifiers.Base.Str;
                            variantItem.Properties.StatModifiers.Base.Wis += variant.Properties.StatModifiers.Base.Wis;
                            variantItem.Properties.StatModifiers.Base.Int += variant.Properties.StatModifiers.Base.Int;
                        }
                        break;
                    }
                case "elemental":
                    {
                        if (variant.Properties.StatModifiers?.Element != null)
                        { 
                            variantItem.Properties.StatModifiers.Element.Offense = variant.Properties.StatModifiers.Element.Offense;
                            variantItem.Properties.StatModifiers.Element.Defense = variant.Properties.StatModifiers.Element.Defense;
                        }
                        break;
                    }
                case "enchantable":
                    {
                        if (variant.Properties.Restrictions?.Level != null)
                        {
                            variantItem.Properties.Restrictions.Level.Min += variant.Properties.Restrictions.Level.Min;
                        }
                        if (variant.Properties.StatModifiers?.Combat != null)
                        { 
                            variantItem.Properties.StatModifiers.Combat.Ac = (sbyte)(item.Properties.StatModifiers.Combat.Ac + variant.Properties.StatModifiers.Combat.Ac);
                            variantItem.Properties.StatModifiers.Combat.Dmg += variant.Properties.StatModifiers.Combat.Dmg;
                            variantItem.Properties.StatModifiers.Combat.Hit += variant.Properties.StatModifiers.Combat.Hit;
                            variantItem.Properties.StatModifiers.Combat.Mr += variant.Properties.StatModifiers.Combat.Mr;
                            variantItem.Properties.StatModifiers.Combat.Regen += variant.Properties.StatModifiers.Combat.Regen;
                        }
                        if (variant.Properties.StatModifiers?.Base != null)
                        {
                            variantItem.Properties.StatModifiers.Base.Dex += variant.Properties.StatModifiers.Base.Dex;
                            variantItem.Properties.StatModifiers.Base.Str += variant.Properties.StatModifiers.Base.Str;
                            variantItem.Properties.StatModifiers.Base.Wis += variant.Properties.StatModifiers.Base.Wis;
                            variantItem.Properties.StatModifiers.Base.Con += variant.Properties.StatModifiers.Base.Con;
                            variantItem.Properties.StatModifiers.Base.Int += variant.Properties.StatModifiers.Base.Int;
                            variantItem.Properties.StatModifiers.Base.Hp += variant.Properties.StatModifiers.Base.Hp;
                            variantItem.Properties.StatModifiers.Base.Mp += variant.Properties.StatModifiers.Base.Mp;
                        }
                        break;
                    }
                case "smithable":
                    {
                        if (variant.Properties.Restrictions?.Level != null)
                        {
                            variantItem.Properties.Restrictions.Level.Min += variant.Properties.Restrictions.Level.Min;
                        }
                        if (variant.Properties.Damage?.Large != null)
                        { 
                            variantItem.Properties.Damage.Large.Min = Convert.ToUInt16(Math.Round(item.Properties.Damage.Large.Min * (variant.Properties.Damage.Large.Min * .01)));
                            variantItem.Properties.Damage.Large.Max = Convert.ToUInt16(Math.Round(item.Properties.Damage.Large.Max * (variant.Properties.Damage.Large.Max * .01)));
                        }
                        if (variant.Properties.Damage?.Small != null)
                        { 
                            variantItem.Properties.Damage.Small.Min = Convert.ToUInt16(Math.Round(item.Properties.Damage.Small.Min * (variant.Properties.Damage.Small.Min * .01)));
                            variantItem.Properties.Damage.Small.Max = Convert.ToUInt16(Math.Round(item.Properties.Damage.Small.Max * (variant.Properties.Damage.Small.Max * .01)));
}
                        break;
                    }
                case "tailorable":
                    {
                        if (variant.Properties.Restrictions?.Level != null)
                        {
                            variantItem.Properties.Restrictions.Level.Min += variant.Properties.Restrictions.Level.Min;
                        }
                        if (variant.Properties.StatModifiers?.Combat != null)
                        { 
                            variantItem.Properties.StatModifiers.Combat.Ac = (sbyte)(item.Properties.StatModifiers.Combat.Ac + variant.Properties.StatModifiers.Combat.Ac);
                            variantItem.Properties.StatModifiers.Combat.Dmg += variant.Properties.StatModifiers.Combat.Dmg;
                            variantItem.Properties.StatModifiers.Combat.Hit += variant.Properties.StatModifiers.Combat.Hit;
                            variantItem.Properties.StatModifiers.Combat.Mr += variant.Properties.StatModifiers.Combat.Mr;
                            variantItem.Properties.StatModifiers.Combat.Regen += variant.Properties.StatModifiers.Combat.Regen;
                        }
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
            if (WorldData.ContainsKey<Mailbox>(mailboxName)) return WorldData.Get<Mailbox>(mailboxName);
            WorldData.Set<Mailbox>(mailboxName, new Mailbox(mailboxName));
            WorldData.Get<Mailbox>(mailboxName).Save();
            Logger.InfoFormat("Mailbox: Creating mailbox for {0}", name);
            return WorldData.Get<Mailbox>(mailboxName);
        }

        public Board GetBoard(string name)
        {
            if (WorldData.ContainsKey<Board>(name)) return WorldData.Get<Board>(name);
            var newBoard = new Board(name) { Id = WorldData.Values<Board>().Count() + 1 };
            WorldData.SetWithIndex<Board>(name, newBoard, newBoard.Id);
            newBoard.Save();
            Logger.InfoFormat("Board: Creating {0}", name);
            return WorldData.Get<Board>(name);
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
            foreach (var item in WorldData.Values<Item>())
            {
                iteminfo0.Nodes.Add(new MetafileNode(item.Name, item.Properties.Restrictions?.Level?.Min ?? 1, (int)(item.Properties.Restrictions?.@Class ?? Items.Class.Peasant),
                    item.Properties.Physical.Weight, item.Properties.Vendor?.ShopTab ?? string.Empty, item.Properties.Vendor?.Description ?? string.Empty));
            }
            WorldData.Set(iteminfo0.Name, iteminfo0.Compile());

            #endregion ItemInfo

            #region SClass

            for (int i = 1; i <= 5; ++i)
            {
                var sclass = new Metafile("SClass" + i);
                sclass.Nodes.Add("Skill");
                foreach (var skill in WorldData.Values<Castable>().Where(x => x.Type.Contains("skill")))
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
                foreach (var spell in WorldData.Values<Castable>().Where(x => x.Type.Contains("spell")))
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
                WorldData.Set(sclass.Name, sclass.Compile());
            }

            #endregion SClass

            #region NPCIllust

            var npcillust = new Metafile("NPCIllust");
            foreach (var npc in WorldData.Values<Npc>()) // change to merchants that have a portrait rather than all
            {
                if (npc.Appearance.Portrait != null)
                {
                    npcillust.Nodes.Add(new MetafileNode(npc.Name, npc.Appearance.Portrait /* portrait filename */));
                }
            }
            WorldData.Set(npcillust.Name, npcillust.Compile());

            #endregion NPCIllust

            #region NationDesc

            var nationdesc = new Metafile("NationDesc");
            foreach (var nation in WorldData.Values<Nation>())
            {
                Logger.DebugFormat("Adding flag {0} for nation {1}", nation.Flag, nation.Name);
                nationdesc.Nodes.Add(new MetafileNode("nation_" + nation.Flag, nation.Name));
            }
            WorldData.Set(nationdesc.Name, nationdesc.Compile());

            #endregion NationDesc
        }

        public void CompileScripts()
        {
            // Scan each directory for *.lua files
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
                        if (Path.GetExtension(file) == ".lua")
                        {
                            var scriptname = Path.GetFileName(file);
                            Logger.InfoFormat("Loading script {0}\\{1}", dir, scriptname);
                            var script = new Script(file, ScriptProcessor);
                            ScriptProcessor.RegisterScript(script);
                            if (dir == "common")
                                script.Run();
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
            ControlMessageHandlers[ControlOpcodes.MonolithControl] = ControlMessage_MonolithControl;
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
                //{MerchantMenuItem.BuyItem, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItem)},
                {
                    MerchantMenuItem.BuyItemQuantity,
                    new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItemWithQuantity)
                },
                {
                  MerchantMenuItem.BuyItemAccept, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_BuyItemAccept)  
                },
                {
                    MerchantMenuItem.SellItemMenu, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemMenu)
                },
                { MerchantMenuItem.SellItem, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItem)},
                {
                    MerchantMenuItem.SellItemQuantity, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemWithQuantity)
                },
                {
                    MerchantMenuItem.SellItemConfirm, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemConfirmation)
                },
                {
                    MerchantMenuItem.SellItemAccept, new MerchantMenuHandler(MerchantJob.Vend, MerchantMenuHandler_SellItemAccept)
                },
                {
                    MerchantMenuItem.LearnSkillMenu, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkillMenu)
                },
                {
                    MerchantMenuItem.LearnSpellMenu, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpellMenu)
                },
                {
                    MerchantMenuItem.ForgetSkillMenu, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_ForgetSkillMenu)
                },
                {
                    MerchantMenuItem.ForgetSpellMenu, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_ForgetSpellMenu)
                },
                {
                    MerchantMenuItem.LearnSkill, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkill)
                },
                {
                    MerchantMenuItem.LearnSpell, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpell)
                },
                {
                    MerchantMenuItem.ForgetSkill, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_ForgetSkill)
                },
                {
                    MerchantMenuItem.ForgetSpell, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_ForgetSpell)
                },
                {
                    MerchantMenuItem.LearnSkillAccept, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkillAccept)
                },
                {
                    MerchantMenuItem.LearnSkillAgree, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkillAgree)
                },
                {
                    MerchantMenuItem.LearnSkillDisagree, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_LearnSkillDisagree)
                },
                {
                    MerchantMenuItem.LearnSpellAccept, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpellAccept)
                },
                {
                    MerchantMenuItem.ForgetSkillAccept, new MerchantMenuHandler(MerchantJob.Skills, MerchantMenuHandler_ForgetSkillAccept)
                },
                {
                    MerchantMenuItem.ForgetSpellAccept, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_ForgetSpellAccept)
                },
                {
                    MerchantMenuItem.LearnSpellAgree, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpellAgree)
                },
                {
                    MerchantMenuItem.LearnSpellDisagree, new MerchantMenuHandler(MerchantJob.Spells, MerchantMenuHandler_LearnSpellDisagree)
                },
                {
                    MerchantMenuItem.SendParcelMenu, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcelMenu)
                },
                {
                    MerchantMenuItem.SendParcelAccept, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcelAccept)
                },
                {
                    MerchantMenuItem.SendParcel, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcel)
                },
                {
                    MerchantMenuItem.SendParcelRecipient, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcelRecipient)
                },
                {
                    MerchantMenuItem.SendParcelFailure, new MerchantMenuHandler(MerchantJob.Post, MerchantMenuHandler_SendParcelFailure)
                },

            };
        }

        #endregion Set Handlers

        public void DeleteUser(string username)
        {
            WorldData.Remove<User>(username);
        }

        public void AddUser(User userobj)
        {
            WorldData.Set(userobj.Name, userobj);
        }

        public User FindUser(string username)
        {
            return WorldData.Get<User>(username);
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
                user.Map?.Remove(user);
                user.Group?.Remove(user);
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
            if (WorldData.TryGetValue(userName, out user))
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
            if (WorldData.TryGetValue(userName, out user))
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
            if (WorldData.TryGetValue(userName, out user))
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

        private void ControlMessage_MonolithControl(HybrasylControlMessage message)
        {

            var monster = (Monster) message.Arguments[0];
            var map = (Map) message.Arguments[1];

            if (monster.IsHostile)
            {
                var entityTree = map.EntityTree.GetObjects(monster.GetViewport());
                var hasPlayer = entityTree.Any(x => x is User);

                if (hasPlayer)
                {
                    //get players
                    var players = entityTree.OfType<User>();

                    //get closest
                    var closest =
                        players.OrderBy(x => Math.Sqrt((Math.Pow(monster.X - x.X, 2) + Math.Pow(monster.Y - x.Y, 2))))
                            .FirstOrDefault();

                    if (closest != null)
                    {

                        //pathfind or cast if far away
                        var distanceX = (int)Math.Sqrt(Math.Pow(monster.X - closest.X, 2));
                        var distanceY = (int)Math.Sqrt(Math.Pow(monster.Y - closest.Y, 2));
                        if (distanceX >= 1 && distanceY >= 1)
                        {
                            var nextAction = _random.Next(1, 6);

                            if (nextAction > 1)
                            {
                                //pathfind;
                                if (distanceX > distanceY)
                                {
                                    monster.Walk(monster.X > closest.X ? Direction.West : Direction.East);
                                }
                                else
                                {
                                    //movey
                                    monster.Walk(monster.Y > closest.Y ? Direction.North : Direction.South);
                                }

                                if (distanceX == distanceY)
                                {
                                    var next = _random.Next(0, 2);

                                    if (next == 0)
                                    {
                                        monster.Walk(monster.X > closest.X ? Direction.West : Direction.East);
                                    }
                                    else
                                    {
                                        monster.Walk(monster.Y > closest.Y ? Direction.North : Direction.South);
                                    }
                                }
                            }
                            else
                            {
                                //cast
                                if (monster.CanCast)
                                {
                                    monster.Cast(closest);
                                }
                            }
                        }
                        else
                        {
                            //check facing and attack or cast

                            var nextAction = _random.Next(1, 6);
                            if (nextAction > 1)
                            {
                                var facing = monster.CheckFacing(monster.Direction, closest);
                                if (facing)
                                {
                                    monster.AssailAttack(monster.Direction, closest);
                                }
                            }
                            else
                            {
                                if (monster.CanCast)
                                {
                                    monster.Cast(closest);
                                }
                            }
                        }
                    }
                }
            }
            if (monster.ShouldWander)
            {
                var nextAction = _random.Next(0, 2);

                if (nextAction == 1)
                {
                    var nextMove = _random.Next(0, 4);
                    monster.Walk((Direction)nextMove);
                }
                else
                {
                    var nextMove = _random.Next(0, 4);
                    monster.Turn((Direction)nextMove);
                }
            }
        }

        #endregion Control Message Handlers

        

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

            //if (!merchant.Inventory.ContainsKey(name))
            //{
            //    user.ShowMerchantGoBack(merchant, "I do not sell that item.", MerchantMenuItem.BuyItemMenu);
            //    return;
            //}

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

            user.ShowBuyMenuQuantity(merchant, name);
        }

        private void MerchantMenuHandler_BuyItemAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowBuyItem(merchant);
        }

        private void MerchantMenuHandler_SellItem(User user, Merchant merchant, ClientPacket packet)
        {
            byte slot = packet.ReadByte();

            var item = user.Inventory[slot];

            if (item.Stackable && item.Count > 1)
            {
                user.ShowSellQuantity(merchant, slot);
                return;
            }

            user.ShowSellConfirm(merchant, slot);
        }

        private void MerchantMenuHandler_SellItemWithQuantity(User user, Merchant merchant, ClientPacket packet)
        {
            byte slot = packet.ReadByte();
            byte quantity = packet.ReadByte();

            
            if (quantity < 1)
            {
                user.ShowSellQuantity(merchant, slot);
                return;
            }

            var item = user.Inventory[slot];
            if (item == null || !item.Stackable) return;

            //if (!merchant.Inventory.ContainsKey(item.Name))
            //{
            //    user.ShowMerchantGoBack(merchant, "I do not want that item.", MerchantMenuItem.SellItemMenu);
            //    return;
            //}

            //if (item.Count < quantity)
            //{
            //    user.ShowMerchantGoBack(merchant, "You don't have that many to sell.", MerchantMenuItem.SellItemMenu);
            //    return;
            //}

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

        private void MerchantMenuHandler_SellItemAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.SellItemAccept(merchant);
        }

        private void MerchantMenuHandler_LearnSkillMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSkillMenu(merchant);
        }
        private void MerchantMenuHandler_LearnSkill(User user, Merchant merchant, ClientPacket packet)
        {
            var skillName = packet.ReadString8(); //skill name
            var skill = WorldData.GetByIndex<Castable>(skillName);
            user.ShowLearnSkill(merchant, skill);
        }
        private void MerchantMenuHandler_LearnSkillAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSkillAccept(merchant);
        }

        private void MerchantMenuHandler_LearnSkillAgree(User user, Merchant merchant, ClientPacket packet)
        {
            
            user.ShowLearnSkillAgree(merchant);
        }

        private void MerchantMenuHandler_LearnSkillDisagree(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSkillDisagree(merchant);
        }

        private void MerchantMenuHandler_LearnSpellMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSpellMenu(merchant);
        }
        private void MerchantMenuHandler_LearnSpell(User user, Merchant merchant, ClientPacket packet)
        {
            var spellName = packet.ReadString8();
            var spell = WorldData.GetByIndex<Castable>(spellName);
            user.ShowLearnSpell(merchant, spell);
        }
        private void MerchantMenuHandler_LearnSpellAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSpellAccept(merchant);
        }
        private void MerchantMenuHandler_LearnSpellAgree(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSpellAgree(merchant);
        }
        private void MerchantMenuHandler_LearnSpellDisagree(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowLearnSpellDisagree(merchant);
        }

        private void MerchantMenuHandler_ForgetSkillMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowForgetSkillMenu(merchant);
        }
        private void MerchantMenuHandler_ForgetSkill(User user, Merchant merchant, ClientPacket packet)
        {

        }
        private void MerchantMenuHandler_ForgetSkillAccept(User user, Merchant merchant, ClientPacket packet)
        {
            var slot = packet.ReadByte();
            
            user.ShowForgetSkillAccept(merchant, slot);
        }
        private void MerchantMenuHandler_ForgetSpellMenu(User user, Merchant merchant, ClientPacket packet)
        {
            
        }
        private void MerchantMenuHandler_ForgetSpell(User user, Merchant merchant, ClientPacket packet)
        {

        }
        private void MerchantMenuHandler_ForgetSpellAccept(User user, Merchant merchant, ClientPacket packet)
        {

        }

        private void MerchantMenuHandler_SendParcelMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowMerchantSendParcel(merchant);
        }
        private void MerchantMenuHandler_SendParcelRecipient(User user, Merchant merchant, ClientPacket packet)
        {
            var item = packet.ReadByte();
            var itemObj = user.Inventory[item];
            user.ShowMerchantSendParcelRecipient(merchant, itemObj);
        }
        private void MerchantMenuHandler_SendParcel(User user, Merchant merchant, ClientPacket packet)
        {

        }

        private void MerchantMenuHandler_SendParcelFailure(User user, Merchant merchant, ClientPacket packet)
        {
            
        }
        private void MerchantMenuHandler_SendParcelAccept(User user, Merchant merchant, ClientPacket packet)
        {
            var recipient = packet.ReadString8();
            user.ShowMerchantSendParcelAccept(merchant, recipient);
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
                Script itemscript;
                if (Game.World.ScriptProcessor.TryGetScript(obj.Name, out itemscript))
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
            if (WorldData.ContainsKey<Item>(id))
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
            var itemKey = new Tuple<Sex, string>(itemSex, name);
            return ItemCatalog.TryGetValue(itemKey, out item);
        }

        public bool TryGetItemTemplate(string name, out Item item)
        {
            // This is kinda gross
            var neutralKey = new Tuple<Sex, string>(Sex.Neutral, name);
            var femaleKey = new Tuple<Sex, string>(Sex.Female, name);
            var maleKey = new Tuple<Sex, string>(Sex.Male, name);

            return ItemCatalog.TryGetValue(neutralKey, out item) || ItemCatalog.TryGetValue(femaleKey, out item) || ItemCatalog.TryGetValue(maleKey, out item);
        }

        public object ScriptMethod(string name, params object[] args)
        {
            object result = null;
        
            if (WorldData.ContainsKey<MethodInfo>(name))
            {
                var method = WorldData.Get<MethodInfo>(name);
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
                                MethodBase method = handler.GetMethodInfo();
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
            //Start our secondary thread
            //SecondaryConsumer = new Thread(QueueConsumer);
            //if (SecondaryConsumer.IsAlive) return;
            //SecondaryConsumer.Start();
            //Logger.InfoFormat("Secondary Consumer thread: started");
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