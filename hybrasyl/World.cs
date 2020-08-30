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
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Messaging;
using Hybrasyl.Objects;
using Hybrasyl.Plugins;
using Hybrasyl.Scripting;
using Hybrasyl.Utility;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Schema;


namespace Hybrasyl
{
    public static class SampleStackExchangeRedisExtensions
    {
        public static T Get<T>(this IDatabase cache, string key) => Deserialize<T>(cache.StringGet(key));

        public static object Get(this IDatabase cache, string key) => Deserialize<object>(cache.StringGet(key));

        public static void Set(this IDatabase cache, string key, object value) => cache.StringSet(key, Serialize(value));

        private static byte[] Serialize(object o, ObjectCreationHandling handling = ObjectCreationHandling.Replace, 
            PreserveReferencesHandling refHandling = PreserveReferencesHandling.All)
        {
            if (o == null)
            {
                return null;
            }
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ObjectCreationHandling = handling;
            settings.PreserveReferencesHandling = refHandling;

            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(o, settings));
        }

        private static T Deserialize<T>(byte[] stream)
        {
            if (stream == null)
            {
                return default(T);
            }
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(stream));
        }
    }


    public partial class World : Server
    {
        private static uint worldObjectID = 0;

        private Dictionary<Xml.MessageType, List<IMessageHandler>> MessagePlugins = new Dictionary<Xml.MessageType, List<IMessageHandler>>();

        private object _lock = new object();
        public static DateTime StartDate => Game.Config.Time != null ? Game.Config.Time.ServerStart.Value : Game.StartDate;
        public Dictionary<uint, WorldObject> Objects { get; set; }

        public Dictionary<string, string> Portraits { get; set; }
        public Xml.LocalizedStrings Strings { get; set; }
        public WorldDataStore WorldData { set; get; }

        public Xml.Nation DefaultNation
        {
            get
            {
                var nation = WorldData.Values<Xml.Nation>().FirstOrDefault(n => n.Default);
                return nation ?? WorldData.Values<Xml.Nation>().First();
            }
        }

        public MultiIndexDictionary<uint, string, DialogSequence> GlobalSequences { get; set; }
        private Dictionary<MerchantMenuItem, MerchantMenuHandler> merchantMenuHandlers;

        public Dictionary<Tuple<Xml.Gender, string>, Xml.Item> ItemCatalog { get; set; }
        // public Dictionary<string, Map> MapCatalog { get; set; }

        public ScriptProcessor ScriptProcessor { get; set; }

        public static BlockingCollection<HybrasylMessage> MessageQueue;
        public static BlockingCollection<HybrasylMessage> ControlMessageQueue;
        public static ConcurrentDictionary<long, User> ActiveUsers { get; private set; }
        public ConcurrentDictionary<string, long> ActiveUsersByName { get; set; }

        public ConcurrentDictionary<Tuple<UInt32, UInt32>, AsyncDialogRequest> ActiveAsyncDialogs { get; set; }

        private Thread ConsumerThread { get; set; }
        private Thread ControlConsumerThread { get; set; }

        public Login Login { get; private set; }

        private static Random _random;

        private static Lazy<ConnectionMultiplexer> _lazyConnector;

        public static ConnectionMultiplexer DatastoreConnection => _lazyConnector.Value;

        public static ChatCommandHandler CommandHandler;

        public bool DebugEnabled { get; set; }

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

        public static string LootSetDirectory => Path.Combine(DataDirectory, "world", "xml", "lootsets");

        public static string ItemVariantDirectory => Path.Combine(DataDirectory, "world", "xml", "itemvariants");

        public static string NpcsDirectory => Path.Combine(DataDirectory, "world", "xml", "npcs");

        public static string LocalizationDirectory => Path.Combine(DataDirectory, "world", "xml", "localization");
        public static string ElementDirectory => Path.Combine(DataDirectory, "world", "xml", "elements");
        #endregion

        public HashSet<Creature> ActiveStatuses = new HashSet<Creature>();

        public static bool TryGetUser(string name, out User userobj)
        {
            //            userobj = JsonConvert.DeserializeObject<User>(jsonstring, settings);
            userobj = DatastoreConnection.GetDatabase().Get<User>(User.GetStorageKey(name));

            if (userobj == null)
            {
                GameLog.Fatal("{Name}: JSON object could not be deserialized!", name);
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
            RegisterPacketThrottle(new GenericPacketThrottle(0x3a, 100, 1000, 500));  // NPC use dialog
            RegisterPacketThrottle(new GenericPacketThrottle(0x38, 600, 0, 500));  // refresh (f5)
            RegisterPacketThrottle(new GenericPacketThrottle(0x39, 200, 1000, 500));  // NPC main menu
            RegisterPacketThrottle(new GenericPacketThrottle(0x13, 800, 0, 0));        // Assail
        }


        public World(int port, Xml.DataStore store)
            : base(port)
        {
            Objects = new Dictionary<uint, WorldObject>();
            Portraits = new Dictionary<string, string>();

            GlobalSequences = new MultiIndexDictionary<uint, string, DialogSequence>();
            ItemCatalog = new Dictionary<Tuple<Xml.Gender, string>, Xml.Item>();

            ScriptProcessor = new ScriptProcessor(this);
            MessageQueue = new BlockingCollection<HybrasylMessage>(new ConcurrentQueue<HybrasylMessage>());
            ControlMessageQueue = new BlockingCollection<HybrasylMessage>(new ConcurrentQueue<HybrasylMessage>());
            ActiveUsers = new ConcurrentDictionary<long, User>();
            ActiveUsersByName = new ConcurrentDictionary<string, long>();

            ActiveAsyncDialogs = new ConcurrentDictionary<Tuple<UInt32, UInt32>, AsyncDialogRequest>();

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
            CommandHandler = new ChatCommandHandler();
            DebugEnabled = false;
        }

        public bool ToggleDebug()
        {
            DebugEnabled = !DebugEnabled;
            if (DebugEnabled)
                Game.LevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
            else
                Game.LevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
            return DebugEnabled;
        }
        public bool InitWorld()
        {
            CompileScripts(); // We compile scripts first so that all future operations requiring scripts work
            if (!LoadData())
            {
                GameLog.Fatal("There were errors loading basic world data. Hybrasyl has halted.");
                GameLog.Fatal("Please fix the errors and try to restart the server again.");
                return false;
            }
            LoadMetafiles();
            SetPacketHandlers();
            SetControlMessageHandlers();
            SetMerchantMenuHandlers();
            RegisterWorldThrottles();
            LoadPlugins();
            GameLog.InfoFormat("Hybrasyl server ready");
            return true;
        }

        public void EnqueueStatusCheck(Creature obj) =>
            ActiveStatuses.Add(obj);

        public void RemoveStatusCheck(Creature obj) => ActiveStatuses.Remove(obj);

        internal void RegisterGlobalSequence(DialogSequence sequence)
        {
            if (GlobalSequences.Count > Constants.DIALOG_SEQUENCE_SHARED)
            {
                GameLog.Error($"Maximum number of global sequences exceeded - registation request for {sequence.Name} ignored!");
                return;
            }
            sequence.Id = (uint)GlobalSequences.Count + 1;
            // Global sequences obviously always have IDs
            GlobalSequences.Add((uint)sequence.Id, sequence.Name, sequence);
        }

        public bool PlayerExists(string name)
        {
            var redis = DatastoreConnection.GetDatabase();
            return redis.KeyExists(User.GetStorageKey(name));
        }

        /// <summary>
        /// Load all messaging plugins, based on server config.
        /// </summary>
        /// <returns></returns>
        public void LoadPlugins()
        {
            // TODO: make more dynamic as we add plugin types
            if (Game.Config.Plugins?.Message != null)
            {
                foreach (var plugin in Game.Config.Plugins.Message)
                {
                    var config = new SimpleConfiguration(plugin.Configuration);
                    if (!MessagePlugins.ContainsKey(plugin.Type))
                        MessagePlugins.Add(plugin.Type, new List<IMessageHandler>());
                    // Instantiate handler
                    var type = Assembly.GetExecutingAssembly().GetType(plugin.Name);
                    if (type == null)
                    {
                        GameLog.Error("LoadPlugins: plugin {plugin} not found in assembly, ignoring", plugin.Name);
                        continue;
                    }
                    if (type.GetInterface(typeof(IMessageHandler).FullName) != null)
                    {
                        try
                        {
                            var pluginInstance = Activator.CreateInstance(type) as IMessageHandler;
                            pluginInstance.Initialize(config);
                            pluginInstance.SetTargets(plugin.Targets);
                            MessagePlugins[plugin.Type].Add(pluginInstance);
                        }
                        catch (Exception e)
                        {
                            GameLog.Error("LoadPlugins: plugin {plugin} failed to initialize: {e}", type.FullName, e);                           
                        }

                    }
                    else
                        GameLog.Error("LoadPlugins: specified plugin {plugin} doesn't implement IMessageHandler interface", type.FullName);
                    GameLog.Info("LoadPlugins: Message plugin {plugin} loaded successfully", type.FullName);
                }
            }
        }

        public static string[] GetXmlFiles(string Path)
        {
            var ret = new List<string>();
            try
            {
                if (Directory.Exists(Path))
                {
                    var wef = new List<string>();

                    foreach (var asdf in Directory.GetFiles(Path, "*.xml", SearchOption.AllDirectories))
                        wef.Add(asdf.Replace(Path, ""));
                    
                    return Directory.GetFiles(Path, "*.xml", SearchOption.AllDirectories).Where(e => !e.Replace(Path, "").StartsWith("\\_")).ToArray();
                }
            }
            catch (Exception e)
            {
                GameLog.Error("Data directory {dir} not found or not accessible: {e}", Path, e);
            }
            return ret.ToArray();
        }

        private bool LoadData()
        {
            // You'll notice some inconsistencies here in that we use both wrapper classes and
            // native XML classes for Hybrasyl objects. This is unfortunate and should be
            // refactored later, but it is way too much work to do now (e.g. maps, etc).

            //Load strings
            foreach (var xml in GetXmlFiles(LocalizationDirectory))
            {
                try
                {
                    Strings = Xml.LocalizedStrings.LoadFromFile(xml);
                    GameLog.Debug("Localization strings loaded.");
                }
                catch (Exception e)
                {
                    GameLog.Error($"Error parsing {xml}: {e}");
                }
            }

            //Load NPCs
            foreach (var xml in GetXmlFiles(NpcsDirectory))
            {
                try
                {
                    var npc = Xml.Npc.LoadFromFile(xml);
                    GameLog.Debug($"NPCs: loaded {npc.Name}");
                    WorldData.Set(npc.Name, npc);
                }
                catch (Exception e)
                {
                    GameLog.Error($"Error parsing {xml}: {e}");
                }
            }

            // Load maps
            foreach (var xml in GetXmlFiles(MapDirectory))
            {
                try
                {
                    Xml.Map newMap = Xml.Map.LoadFromFile(xml);
                    var map = new Map(newMap, this);
                    if (!WorldData.SetWithIndex(map.Id, map, map.Name))
                        GameLog.ErrorFormat("SetWithIndex fail for {map.Name}..?");
                    GameLog.Info("Maps: Loaded {filename} ({mapname})", Path.GetFileName(xml), map.Name);
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            GameLog.InfoFormat("Maps: {0} maps loaded", WorldData.Count<Map>());

            // Load nations
            foreach (var xml in GetXmlFiles(NationDirectory))
            {
                try
                {
                    var newNation = Xml.Nation.LoadFromFile(xml);
                    GameLog.DebugFormat("Nations: Loaded {0}", newNation.Name);
                    WorldData.Set(newNation.Name, newNation);
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            // Ensure at least one nation and one map exist. Otherwise, things get a little weird
            if (WorldData.Count<Xml.Nation>() == 0)
            {
                GameLog.Fatal("National data: at least one well-formed nation file must exist!");
                return false;
            }

            if (WorldData.Count<Map>() == 0)
            {
                GameLog.Fatal("Map data: at least one well-formed map file must exist!");
                return false;
            }

            GameLog.InfoFormat("National data: {0} nations loaded", WorldData.Count<Xml.Nation>());

            //Load Creatures
            foreach (var xml in GetXmlFiles(CreatureDirectory))
            {
                try
                {
                    var creature = Xml.Creature.LoadFromFile(xml);
                    GameLog.DebugFormat("Creatures: loaded {0}", creature.Name);
                    WorldData.Set(creature.Name, creature);
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            GameLog.InfoFormat("Creatures: {0} creatures loaded", WorldData.Count<Xml.Creature>());

            //Load SpawnGroups
            foreach (var xml in GetXmlFiles(SpawnGroupDirectory))
            {
                try
                {
                    var spawnGroup = Xml.SpawnGroup.LoadFromFile(xml);
                    spawnGroup.Filename = Path.GetFileNameWithoutExtension(xml);
                    GameLog.InfoFormat("SpawnGroup: loaded {0}", spawnGroup.Filename);
                    WorldData.SetWithIndex(spawnGroup.GetHashCode(), spawnGroup, spawnGroup.Filename);
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("SpawnGroup: Error parsing {0}: {1}", xml, e);
                }
            }

            GameLog.InfoFormat("Spawngroups: {0} spawngroups loaded", WorldData.Count<Xml.SpawnGroup>());

            //Load LootSets
            foreach (var xml in GetXmlFiles(LootSetDirectory))
            {
                try
                {
                    var lootSet = Xml.LootSet.LoadFromFile(xml);

                    GameLog.DebugFormat("LootSets: loaded {0}", lootSet.Name);
                    WorldData.SetWithIndex(lootSet.GetHashCode(), lootSet, lootSet.Name);
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            GameLog.InfoFormat("Loot Sets: {0} loot sets loaded", WorldData.Count<Xml.LootSet>());

            // Load worldmaps
            foreach (var xml in GetXmlFiles(WorldMapDirectory))
            {
                try
                {
                    Xml.WorldMap newWorldMap = Xml.WorldMap.LoadFromFile(xml);
                    var worldmap = new WorldMap(newWorldMap);
                    WorldData.Set(worldmap.Name, worldmap);
                    foreach (var point in worldmap.Points)
                    {
                        WorldData.Set(point.Id, point);
                    }
                    GameLog.DebugFormat("World Maps: Loaded {0}", worldmap.Name);
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            GameLog.InfoFormat("World Maps: {0} world maps loaded", WorldData.Count<WorldMap>());

            // Load item variants
            foreach (var xml in GetXmlFiles(ItemVariantDirectory))
            {
                try
                {
                    Xml.VariantGroup newGroup = Xml.VariantGroup.LoadFromFile(xml);
                    GameLog.DebugFormat("Item variants: loaded {0}", newGroup.Name);
                    WorldData.Set(newGroup.Name, newGroup);

                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            GameLog.InfoFormat("ItemObject variants: {0} variant sets loaded", WorldData.Values<Xml.VariantGroup>().Count());

            // Load items
            foreach (var xml in GetXmlFiles(ItemDirectory))
            {
                try
                {
                    Xml.Item newItem = Xml.Item.LoadFromFile(xml);
                    var variants = new Dictionary<string, List<Xml.Item>>();

                    GameLog.DebugFormat("Items: loaded {0}, id {1}", newItem.Name, newItem.Id);
                    if (newItem.Properties.Variants != null)
                    {
                        foreach (var targetGroup in newItem.Properties.Variants.Group)
                        {
                            variants[targetGroup] = new List<Xml.Item>();
                            foreach (var variant in WorldData.Get<Xml.VariantGroup>(targetGroup).Variant)
                            {
                                var variantItem = ResolveVariant(newItem, variant, targetGroup);
                                GameLog.InfoFormat("ItemObject {0}: variantgroup {1}, subvariant {2}", variantItem.Name, targetGroup, variant.Name);
                                if (WorldData.ContainsKey<Xml.Item>(variantItem.Id))
                                {
                                    GameLog.ErrorFormat("Item already exists with Key {0} : {1}. Cannot add {2}", variantItem.Id, WorldData.Get<Xml.Item>(variantItem.Id).Name, variantItem.Name);
                                }
                                WorldData.SetWithIndex(variantItem.Id, variantItem, variantItem.Name);
                                variants[targetGroup].Add(variantItem);
                            }
                        }
                    }
                    newItem.Variants = variants;
                    WorldData.SetWithIndex(newItem.Id, newItem, newItem.Name);

                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            foreach (var xml in GetXmlFiles(StatusDirectory))
            {
                try
                {
                    string name = string.Empty;
                    Xml.Status newStatus = Xml.Status.LoadFromFile(xml);
                    WorldData.Set(newStatus.Name, newStatus);
                    GameLog.Warning($"Statuses: loaded {newStatus.Name}, id {newStatus.Id}");
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }

            }

            GameLog.InfoFormat("Statuses: {0} statuses loaded", WorldData.Values<Xml.Status>().Count());

            foreach (var xml in GetXmlFiles(CastableDirectory))
            {
                try
                {
                    string name = string.Empty;
                    Xml.Castable newCastable = Xml.Castable.LoadFromFile(xml);
                    WorldData.SetWithIndex(newCastable.Id, newCastable, newCastable.Name);
                    GameLog.InfoFormat("Castables: loaded {0}, id {1}", newCastable.Name, newCastable.Id);
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }

            GameLog.InfoFormat("Castables: {0} castables loaded", WorldData.Values<Xml.Castable>().Count());

            //load element tables
            foreach (var xml in GetXmlFiles(ElementDirectory))
            { 
                try
                {
                    //currently only support one table
                    Xml.ElementTable table = Xml.ElementTable.LoadFromFile(xml);
                    WorldData.Set("ElementTable", table);
                    foreach (var source in table.Source)
                    {
                        foreach(var target in source.Target)
                        {
                            GameLog.InfoFormat($"ElementTable: loaded element {source.Element}, target {target.Element}, multiplier {target.Multiplier}");
                        }
                        
                    }
                    
                }
                catch (Exception e)
                {
                    GameLog.ErrorFormat("Error parsing {0}: {1}", xml, e);
                }
            }


                // Load data from Redis
                // Load mailboxes
                var server = World.DatastoreConnection.GetServer(World.DatastoreConnection.GetEndPoints()[0]);
            foreach (var key in server.Keys(pattern: "Hybrasyl.Mailbox*"))
            {
                GameLog.InfoFormat("Loading mailbox at {0}", key);
                var mailbox = DatastoreConnection.GetDatabase().Get<Mailbox>(key);
                var name = key.ToString().Split(':')[1].ToLower();
                if (name == string.Empty)
                {
                    GameLog.Warning("Potentially corrupt mailbox data in Redis; ignoring");
                    continue;
                }
                //Mailboxes.Add(name, mailbox);
                WorldData.Set(name, mailbox);
            }

            //Load Vaults
            foreach(var key in server.Keys(pattern: "Hybrasyl.Vault*"))
            {
                GameLog.InfoFormat($"Loading vault with key {key}");
                var vault = DatastoreConnection.GetDatabase().Get<Vault>(key);
                WorldData.Set(vault.OwnerUuid, vault);
            }

            foreach (var key in server.Keys(pattern: "Hybrasyl.GuildVault*"))
            {
                GameLog.InfoFormat($"Loading vault with key {key}");
                var vault = DatastoreConnection.GetDatabase().Get<GuildVault>(key);
                WorldData.Set(vault.OwnerUuid, vault);
            }

            // Load all boards
            foreach (var key in server.Keys(pattern: "Hybrasyl.Board*"))
            {
                GameLog.InfoFormat("Loading board at {0}", key);
                var messageboard = DatastoreConnection.GetDatabase().Get<Board>(key);
                var name = key.ToString().Split(':')[1];
                if (name == string.Empty)
                {
                    GameLog.Warning("Potentially corrupt board data in Redis; ignoring");
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
                    GameLog.InfoFormat("Boards: Global board {0} initialized", globalboard.Name);
                    WorldData.SetWithIndex(board.Name, board, board.Id);
                    board.Save();
                }
            }
            else
            {
                // If no boards are configured we set up a global default, moderated by the users specified
                // in <Privileged>
                var board = GetBoard("Hybrasyl");
                board.DisplayName = "Hybrasyl Global Board";
                if (Game.Config.Access != null)
                {
                    foreach (var moderator in Game.Config.Access.PrivilegedUsers)
                        board.SetAccessLevel(moderator, BoardAccessLevel.Moderate);
                    WorldData.SetWithIndex(board.Name, board, board.Id);
                    board.Save();
                }
            }
            return true;
        }

        public Xml.Item ResolveVariant(Xml.Item item, Xml.Variant variant, string variantGroup)
        {
            // Ensure all our modifiable / referenced properties at least exist
            // TODO: this is pretty hacky
            if (item.Properties.Physical is null)
                item.Properties.Physical = new Xml.Physical();
            if (item.Properties.StatModifiers is null)
                item.Properties.StatModifiers = new Xml.ItemStatModifiers();
            if (item.Properties.StatModifiers.Base is null)
                item.Properties.StatModifiers.Base = new Xml.StatModifierBase();
            if (item.Properties.StatModifiers.Combat is null)
                item.Properties.StatModifiers.Combat = new Xml.StatModifierCombat();
            if (item.Properties.Restrictions is null)
                item.Properties.Restrictions = new Xml.ItemRestrictions();
            if (item.Properties.Restrictions.Level is null)
                item.Properties.Restrictions.Level = new Xml.RestrictionsLevel();
            if (item.Properties.StatModifiers.Element is null)
                item.Properties.StatModifiers.Element = new Xml.StatModifierElement();
            if (item.Properties.Damage is null)
                item.Properties.Damage = new Xml.ItemDamage();
            if (item.Properties.Damage.Small is null)
                item.Properties.Damage.Small = new Xml.ItemDamageSmall();
            if (item.Properties.Damage.Large is null)
                item.Properties.Damage.Large = new Xml.ItemDamageLarge();

            var variantItem = item.Clone();

            variantItem.Name = $"{variant.Modifier} {item.Name}";
            variantItem.ParentItem = item;
            variantItem.IsVariant = true;
            GameLog.Debug($"Processing variant: {variantItem.Name}");

            if (variant.Properties.Flags != 0)
                variantItem.Properties.Flags = variant.Properties.Flags;

            variantItem.Properties.Physical.Value =  Convert.ToUInt32(Math.Round(item.Properties.Physical.Value * (variant.Properties.Physical.Value * .01)));
            variantItem.Properties.Physical.Durability = Convert.ToUInt32(Math.Round(item.Properties.Physical.Durability * (variant.Properties.Physical.Durability * .01)));
            variantItem.Properties.Physical.Weight =  Convert.ToInt32(Math.Round(item.Properties.Physical.Weight * (variant.Properties.Physical.Weight * .01)));

            switch (variantGroup.ToLower())
            {
                case "consecratable":
                    {
                        if (variant.Properties.Restrictions?.Level != null) 
                            variantItem.Properties.Restrictions.Level.Min += variant.Properties.Restrictions.Level.Min;
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
                        if ((variantItem.Properties?.Equipment?.Slot ?? Xml.EquipmentSlot.None) == Xml.EquipmentSlot.Waist)
                            variantItem.Properties.StatModifiers.Element.Defense = variant.Properties.StatModifiers.Element.Defense;
                        else if ((variantItem.Properties?.Equipment?.Slot ?? Xml.EquipmentSlot.None) == Xml.EquipmentSlot.Necklace)
                            variantItem.Properties.StatModifiers.Element.Offense = variant.Properties.StatModifiers.Element.Offense;
                        else if (variant.Properties.StatModifiers?.Element != null)
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
                            variantItem.Properties.StatModifiers.Combat.Ac += variant.Properties.StatModifiers.Combat.Ac;
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

        public Vault GetVault(string uuid)
        {

            if (WorldData.ContainsKey<Vault>(uuid))
            {
                WorldData.Get<Vault>(uuid).Save();
                return WorldData.Get<Vault>(uuid);
            }
            WorldData.Set<Vault>(uuid, new Vault(uuid));
            WorldData.Get<Vault>(uuid).Save();
            GameLog.InfoFormat("Vault: Creating vault for {0}", uuid);
            return WorldData.Get<Vault>(uuid);
        }
        public GuildVault GetGuildVault(string uuid)
        {

            if (WorldData.ContainsKey<GuildVault>(uuid))
            {
                WorldData.Get<GuildVault>(uuid).Save();
                return WorldData.Get<GuildVault>(uuid);
            }
            WorldData.Set<GuildVault>(uuid, new GuildVault(uuid));
            WorldData.Get<GuildVault>(uuid).Save();
            GameLog.InfoFormat("Vault: Creating vault for {0}", uuid);
            return WorldData.Get<GuildVault>(uuid);
        }

        public Mailbox GetMailbox(string name)
        {
            var mailboxName = name.ToLower();
            if (WorldData.ContainsKey<Mailbox>(mailboxName)) return WorldData.Get<Mailbox>(mailboxName);
            WorldData.Set<Mailbox>(mailboxName, new Mailbox(mailboxName));
            WorldData.Get<Mailbox>(mailboxName).Save();
            GameLog.InfoFormat("Mailbox: Creating mailbox for {0}", name);
            return WorldData.Get<Mailbox>(mailboxName);
        }

        public Board GetBoard(string name)
        {
            if (WorldData.ContainsKey<Board>(name)) return WorldData.Get<Board>(name);
            var newBoard = new Board(name) { Id = WorldData.Values<Board>().Count() + 1 };
            WorldData.SetWithIndex<Board>(name, newBoard, newBoard.Id);
            newBoard.Save();
            GameLog.InfoFormat("Board: Creating {0}", name);
            return WorldData.Get<Board>(name);
        }

        private static void ValidationCallBack(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Warning)
                GameLog.WarningFormat("XML warning: {0}", args.Message);
            else
                GameLog.ErrorFormat("XML ERROR: {0}", args.Message);
        }

        private void LoadMetafiles()
        {
            // these might be better suited in LoadData as the database is being read, but only items are in database atm

            #region ItemInfo
            var itmIndex = 0;
            for (var i = 0; i < (int)(WorldData.Values<Xml.Item>().Count() / 600); i++)
            {
                var iteminfo = new Metafile($"ItemInfo{i}");
                // TODO: split items into multiple ItemInfo files (DA does ~700 each)
                var items = WorldData.Values<Xml.Item>().OrderBy(x => x.Name).ToArray();
                for(var j = 0 + itmIndex; j< (600 + itmIndex); j++)
                {
                    if (j == items.Length) break;
                    var item = items[j];
                    var level = item.Properties.Restrictions?.Level?.Min ?? 1;
                    var xclass = item.Properties.Restrictions?.Class ?? Xml.Class.Peasant;
                    var nclass = xclass.ToString("g").Replace("Peasant","All");
                    var weight = item.Properties.Physical.Weight;
                    var tab = item.Properties.Vendor?.ShopTab ?? "Junk";
                    var defaultDesc = "";
                    if (item.BonusAc != 0) defaultDesc += $"{(item.BonusAc > 0 ? "+" + item.BonusAc.ToString() : item.BonusAc.ToString())} AC \n";
                    if (item.BonusStr != 0) defaultDesc += $"{(item.BonusStr > 0 ? "+" + item.BonusStr.ToString() : item.BonusStr.ToString())} STR \n";
                    if (item.BonusInt != 0) defaultDesc += $"{(item.BonusInt > 0 ? "+" + item.BonusInt.ToString() : item.BonusInt.ToString())} INT \n";
                    if (item.BonusWis != 0) defaultDesc += $"{(item.BonusWis > 0 ? "+" + item.BonusWis.ToString() : item.BonusWis.ToString())} WIS \n";
                    if (item.BonusCon != 0) defaultDesc += $"{(item.BonusCon > 0 ? "+" + item.BonusCon.ToString() : item.BonusCon.ToString())} CON \n";
                    if (item.BonusDex != 0) defaultDesc += $"{(item.BonusDex > 0 ? "+" + item.BonusDex.ToString() : item.BonusDex.ToString())} DEX \n";
                    if (item.BonusHit != 0) defaultDesc += $"{(item.BonusHit > 0 ? "+" + item.BonusHit.ToString() : item.BonusHit.ToString())} HIT \n";
                    if (item.BonusDmg != 0) defaultDesc += $"{(item.BonusDmg > 0 ? "+" + item.BonusDmg.ToString() : item.BonusDmg.ToString())} DMG \n";
                    if (item.BonusMr != 0) defaultDesc += $"{(item.BonusMr > 0 ? "+" + item.BonusMr.ToString() : item.BonusMr.ToString())} MR \n";
                    if (item.BonusHp != 0) defaultDesc += $"{(item.BonusHp > 0 ? "+" + item.BonusHp.ToString() : item.BonusHp.ToString())} HP \n";
                    if (item.BonusMp != 0) defaultDesc += $"{(item.BonusMp > 0 ? "+" + item.BonusMp.ToString() : item.BonusMp.ToString())} MP \n";
                    if (defaultDesc.Length > 0) defaultDesc.Remove(defaultDesc.Length - 2);

                    var desc = "";
                    if (item.Properties.Vendor?.Description == null || item.Properties.Vendor?.Description == "item")
                    {
                        desc = defaultDesc;
                    }
                    else
                    {
                        desc = item.Properties.Vendor?.Description;
                    }
                    //var desc = item.Properties.Vendor?.Description != "item" ? item.Properties.Vendor?.Description : defaultDesc;
                    //var desc = item.Properties.Vendor?.Description ?? $"{nclass},Lev{level},Wt{weight}";
                    
                    iteminfo.Nodes.Add(new MetafileNode(item.Name, level, (int)xclass, weight, tab, desc));
                }
                WorldData.Set(iteminfo.Name, iteminfo.Compile());
                itmIndex += 600;
            }
            #endregion ItemInfo

            #region SClass
            for (int i = 1; i <= 5; ++i)
            {
                var sclass = new Metafile("SClass" + i);
                
                List<Xml.Castable> skills = null;
                List<Xml.Castable> spells = null;
                Xml.Class @class = (Xml.Class)i;

                skills = WorldData.Values<Xml.Castable>().Where(x => (x.Book == Xml.Book.PrimarySkill || x.Book == Xml.Book.SecondarySkill || x.Book == Xml.Book.UtilitySkill) && (x.Class.Contains(@class))).OrderBy(x => x.Requirements.FirstOrDefault(y => y.Class.Contains(@class)) == null ? 1 : x.Requirements.FirstOrDefault(y => y.Class.Contains(@class)).Level?.Min ?? 1).ThenBy(x => x.Name).ToList();
                spells = WorldData.Values<Xml.Castable>().Where(x => (x.Book == Xml.Book.PrimarySpell || x.Book == Xml.Book.SecondarySpell || x.Book == Xml.Book.UtilitySpell) && (x.Class.Contains(@class))).OrderBy(x => x.Requirements.FirstOrDefault(y => y.Class.Contains(@class)) == null ? 1 : x.Requirements.FirstOrDefault(y => y.Class.Contains(@class)).Level?.Min ?? 1).ThenBy(x => x.Name).ToList();

                var ignoreSpells = spells.Where(x => x.Categories.Any(x => x.Value == "Politics" || x.Value == "Religion" || x.Value == "Profession")).ToList();
                var ignoreSkills = skills.Where(x => x.Categories.Any(x => x.Value == "Politics" || x.Value == "Religion" || x.Value == "Profession")).ToList();

                foreach (var spell in ignoreSpells)
                {
                    spells.Remove(spell);
                }
                foreach (var skill in ignoreSkills)
                {
                    skills.Remove(skill);
                }

                sclass.Nodes.Add("");
                sclass.Nodes.Add("Skill");
                foreach (var skill in skills)
                {
                    var desc = "";
                    if(skill.Descriptions.Any(x => x.Class.Contains(@class)))
                    {
                        desc = skill.Descriptions.FirstOrDefault(x => x.Class.Contains(@class)).Value;
                    }
                    else if(skill.Descriptions.Any(x => x.Class.Contains(Xml.Class.Peasant)))
                    {
                        desc = skill.Descriptions.FirstOrDefault(x => x.Class.Contains(Xml.Class.Peasant)).Value;
                    }
                    
                    if(desc == null)
                    {
                        desc = "";
                    }

                    var requirements = skill.Requirements.FirstOrDefault(x => x.Class.Contains(@class));
                    if (requirements == null)
                    {
                        requirements = skill.Requirements.FirstOrDefault(x => x.Class.Contains(Xml.Class.Peasant));
                    }

                    List<Xml.LearnPrerequisite> prereqs = null;
                    if(requirements != null)
                    {
                        prereqs = requirements.Prerequisites;
                    }
                    else
                    {
                        requirements = new Xml.Requirement();
                    }

                    if(requirements.Level == null)
                    {
                        requirements.Level = new Xml.ClassRequirementLevel();
                        requirements.Level.Min = 0;
                    }

                    
                    var prereq1 = "0";
                    var prereq1level = "0";
                    var prereq2 = "0";
                    var prereq2level = "0";
                    if(prereqs != null)
                    {
                        if(prereqs.Count <= 2 && prereqs.Count > 0)
                        {
                            if (prereqs[0] != null)
                            {
                                prereq1 = prereqs[0].Value;
                                prereq1level = $"{ prereqs[0].Level}";
                            }
                            if (prereqs.Count == 2)
                            {
                                if (prereqs[1] != null)
                                {
                                    prereq2 = prereqs[1].Value;
                                    prereq2level = $"{ prereqs[1].Level}";
                                }
                            }
                        }
                    }

                    sclass.Nodes.Add(new MetafileNode(skill.Name,
                        string.Format("{0}/{1}/{2}",requirements.Level.Min == 0 ? 1 : requirements.Level.Min, 0, requirements.Ab != null ? (requirements.Ab.Min == 0 ? 1 : requirements.Ab.Min) : 0), // req level, master (0/1), req ab
                        string.Format("{0}/{1}/{2}", 0, 0, 0), // skill icon, x position (defunct), y position (defunct)
                        string.Format("{0}/{1}/{2}/{3}/{4}", 
                                    requirements?.Physical == null ? 3 : requirements.Physical.Str, 
                                    requirements?.Physical == null ? 3 : requirements.Physical.Int, 
                                    requirements?.Physical == null ? 3 : requirements.Physical.Wis, 
                                    requirements?.Physical == null ? 3 : requirements.Physical.Dex, 
                                    requirements?.Physical == null ? 3 : requirements.Physical.Con),
                        // str, int, wis, dex, con (not a typo, dex before con)
                        string.Format("{0}/{1}", prereq1, prereq1level), // req skill 1 (skill name or 0 for none), req skill 1 level
                        string.Format("{0}/{1}", prereq2, prereq2level), // req skill 2 (skill name or 0 for none), req skill 2 level
                        desc
                        ));
                }
                sclass.Nodes.Add(new MetafileNode("Skill_End", ""));
                sclass.Nodes.Add("");
                sclass.Nodes.Add("Spell");
                foreach (var spell in spells)
                // placeholder; change to skills where class == i, are learnable from trainer, and sort by level
                {
                    var desc = "";
                    if (spell.Descriptions.Any(x => x.Class.Contains(@class)))
                    {
                        desc = spell.Descriptions.FirstOrDefault(x => x.Class.Contains(@class)).Value;
                    }
                    else if (spell.Descriptions.Any(x => x.Class.Contains(Xml.Class.Peasant)))
                    {
                        desc = spell.Descriptions.FirstOrDefault(x => x.Class.Contains(Xml.Class.Peasant)).Value;
                    }

                    if (desc == null)
                    {
                        desc = "";
                    }

                    var requirements = spell.Requirements.FirstOrDefault(x => x.Class.Contains(@class));
                    if (requirements == null)
                    {
                        requirements = spell.Requirements.FirstOrDefault(x => x.Class.Contains(Xml.Class.Peasant));
                    }

                    List<Xml.LearnPrerequisite> prereqs = null;
                    if (requirements != null)
                    {
                        prereqs = requirements.Prerequisites;
                    }
                    else
                    {
                        requirements = new Xml.Requirement();
                    }

                    if (requirements.Level == null)
                    {
                        requirements.Level = new Xml.ClassRequirementLevel();
                        requirements.Level.Min = 0;
                    }

                    var prereq1 = "0";
                    var prereq1level = "0";
                    var prereq2 = "0";
                    var prereq2level = "0";
                    if (prereqs != null)
                    {
                        if (prereqs.Count <= 2 && prereqs.Count > 0)
                        {
                            if (prereqs[0] != null)
                            {
                                prereq1 = prereqs[0].Value;
                                prereq1level = $"{ prereqs[0].Level}";
                            }
                            if (prereqs.Count == 2)
                            {
                                if (prereqs[1] != null)
                                {
                                    prereq2 = prereqs[1].Value;
                                    prereq2level = $"{ prereqs[1].Level}";
                                }
                            }
                        }
                    }

                    sclass.Nodes.Add(new MetafileNode(spell.Name,
                        string.Format("{0}/{1}/{2}", requirements.Level.Min == 0 ? 1 : requirements.Level.Min, 0, requirements.Ab != null ? (requirements.Ab.Min == 0 ? 1 : requirements.Ab.Min) : 0), // req level, master (0/1), req ab
                        string.Format("{0}/{1}/{2}", 0, 0, 0), // spell icon, x position (defunct), y position (defunct)
                        string.Format("{0}/{1}/{2}/{3}/{4}", requirements?.Physical == null ? 3 : requirements.Physical.Str, requirements?.Physical == null ? 3 : requirements.Physical.Dex, requirements?.Physical == null ? 3 : requirements.Physical.Int, requirements?.Physical == null ? 3 : requirements.Physical.Wis, requirements?.Physical == null ? 3 : requirements.Physical.Con),
                        // str, dex, int, wis, con (not a typo, dex after str)
                        string.Format("{0}/{1}", prereq1, prereq1level), // req spell 1 (spell name or 0 for none), req skill 1 level
                        string.Format("{0}/{1}", prereq2, prereq2level), // req spell 2 (spell name or 0 for none), req skill 2 level
                        desc
                        ));
                }
                sclass.Nodes.Add(new MetafileNode("Spell_End", ""));
                WorldData.Set(sclass.Name, sclass.Compile());
            }

            #endregion SClass

            #region NPCIllust

            var npcillust = new Metafile("NPCIllust");
            foreach (var npc in WorldData.Values<Xml.Npc>()) // change to merchants that have a portrait rather than all
            {
                if (npc.Appearance.Portrait != null)
                {
                    npcillust.Nodes.Add(new MetafileNode(npc.Name, npc.Appearance.Portrait /* portrait filename */));
                    GameLog.Info("metafile: set {Name} to {Portrait}", npc.Name, npc.Appearance.Portrait);
                }
            }
            WorldData.Set(npcillust.Name, npcillust.Compile());

            #endregion NPCIllust

            #region NationDesc

            var nationdesc = new Metafile("NationDesc");
            foreach (var nation in WorldData.Values<Xml.Nation>())
            {
                GameLog.DebugFormat("Adding flag {0} for nation {1}", nation.Flag, nation.Name);
                nationdesc.Nodes.Add(new MetafileNode("nation_" + nation.Flag, nation.Name));
            }
            WorldData.Set(nationdesc.Name, nationdesc.Compile());

            #endregion NationDesc
        }

        public void CompileScripts()
        {
            // Scan each directory for *.lua files
            foreach (var file in Directory.GetFiles(ScriptDirectory, "*.lua", SearchOption.AllDirectories))
            {
                var path = file.Replace(ScriptDirectory, "");
                var scriptname = Path.GetFileName(file);
                if (path.StartsWith("_"))
                    continue;
                GameLog.Info($"Loading script: {path}");
                try
                {
                    var script = new Scripting.Script(file, ScriptProcessor);
                    ScriptProcessor.RegisterScript(script);
                    if (path.StartsWith("common"))
                        script.Run();
                }
                catch (Exception e)
                {
                    GameLog.Error($"Script {scriptname}: Registration failed: {e.ToString()}");                
                }
            }
        }

        public IMessageHandler ResolveMessagingPlugin(Xml.MessageType type, Plugins.Message message)
        {
            // Do we have a plugin that would handle this message?
            if (MessagePlugins.TryGetValue(type, out List<IMessageHandler> pluginList))
            {
                foreach (var plugin in pluginList)
                {
                    if (plugin.WillHandle(message.Recipient))
                        return plugin;
                }
            }
            return null;
        }

        #region Set Handlers

        public void SetControlMessageHandlers()
        {
            // ST: secondary threads
            // PT: primary thread
            ControlMessageHandlers[ControlOpcodes.CleanupUser] = ControlMessage_CleanupUser; // PT
            ControlMessageHandlers[ControlOpcodes.SaveUser] = ControlMessage_SaveUser; // ST + user lock
            ControlMessageHandlers[ControlOpcodes.ShutdownServer] = ControlMessage_ShutdownServer; // ST/PT
            ControlMessageHandlers[ControlOpcodes.RegenUser] = ControlMessage_RegenerateUser; // ST + creature lock
            ControlMessageHandlers[ControlOpcodes.LogoffUser] = ControlMessage_LogoffUser; // PT
            ControlMessageHandlers[ControlOpcodes.MailNotifyUser] = ControlMessage_MailNotifyUser; // ST + creature lock
            ControlMessageHandlers[ControlOpcodes.StatusTick] = ControlMessage_StatusTick; // ST + creature lock
            ControlMessageHandlers[ControlOpcodes.MonolithSpawn] = ControlMessage_SpawnMonster; // ST + map lock?
            ControlMessageHandlers[ControlOpcodes.MonolithControl] = ControlMessage_MonolithControl; // ST + map lock?
            ControlMessageHandlers[ControlOpcodes.TriggerRefresh] = ControlMessage_TriggerRefresh; // ST
            ControlMessageHandlers[ControlOpcodes.HandleDeath] = ControlMessage_HandleDeath; // ST + user/map locks
            ControlMessageHandlers[ControlOpcodes.DialogRequest] = ControlMessage_DialogRequest;
        }

        public void SetPacketHandlers()
        {
            // ST: secondary threads
            // PT: primary thread
            PacketHandlers[0x05] = PacketHandler_0x05_RequestMap; // ST
            PacketHandlers[0x06] = PacketHandler_0x06_Walk;  // ST + map lock
            PacketHandlers[0x07] = PacketHandler_0x07_PickupItem; // ST + map lock
            PacketHandlers[0x08] = PacketHandler_0x08_DropItem; // ST + map lock
            PacketHandlers[0x0B] = PacketHandler_0x0B_ClientExit; // primary thread 
            PacketHandlers[0x0C] = PacketHandler_0X0C_PutGround;
            PacketHandlers[0x0E] = PacketHandler_0x0E_Talk; // ST
            PacketHandlers[0x0F] = PacketHandler_0x0F_UseSpell; // PT
            PacketHandlers[0x10] = PacketHandler_0x10_ClientJoin; // PT
            PacketHandlers[0x11] = PacketHandler_0x11_Turn; // ST + user lock
            PacketHandlers[0x13] = PacketHandler_0x13_Attack; // PT
            PacketHandlers[0x18] = PacketHandler_0x18_ShowPlayerList; // ST
            PacketHandlers[0x19] = PacketHandler_0x19_Whisper; // ST
            PacketHandlers[0x1B] = PacketHandler_0x1B_Settings; // either
            PacketHandlers[0x1C] = PacketHandler_0x1C_UseItem; // PT
            PacketHandlers[0x1D] = PacketHandler_0x1D_Emote; // ST
            PacketHandlers[0x24] = PacketHandler_0x24_DropGold; // ST + map lock
            PacketHandlers[0x29] = PacketHandler_0x29_DropItemOnCreature; // ST + map/user lock
            PacketHandlers[0x2A] = PacketHandler_0x2A_DropGoldOnCreature; // ST + map/user lock
            PacketHandlers[0x2D] = PacketHandler_0x2D_PlayerInfo; // ST
            PacketHandlers[0x2E] = PacketHandler_0x2E_GroupRequest; // PT
            PacketHandlers[0x2F] = PacketHandler_0x2F_GroupToggle; // PT
            PacketHandlers[0x30] = PacketHandler_0x30_MoveUIElement; // ST + user lock
            PacketHandlers[0x38] = PacketHandler_0x38_Refresh; // ST
            PacketHandlers[0x39] = PacketHandler_0x39_NPCMainMenu; // PT
            PacketHandlers[0x3A] = PacketHandler_0x3A_DialogUse; // PT
            PacketHandlers[0x3B] = PacketHandler_0x3B_AccessMessages; // ST
            PacketHandlers[0x3E] = PacketHandler_0x3E_UseSkill; // PT
            PacketHandlers[0x3F] = PacketHandler_0x3F_MapPointClick; // ST
            PacketHandlers[0x43] = PacketHandler_0x43_PointClick; // ST?
            PacketHandlers[0x44] = PacketHandler_0x44_EquippedItemClick; // PT
            PacketHandlers[0x45] = PacketHandler_0x45_ByteHeartbeat; // ST
            PacketHandlers[0x47] = PacketHandler_0x47_StatPoint; // ST + user lock
            PacketHandlers[0x4a] = PacketHandler_0x4A_Trade; // PT
            PacketHandlers[0x4D] = PacketHandler_0x4D_BeginCasting; // PT
            PacketHandlers[0x4E] = PacketHandler_0x4E_CastLine; // PT
            PacketHandlers[0x4F] = PacketHandler_0x4F_ProfileTextPortrait; // ST
            PacketHandlers[0x75] = PacketHandler_0x75_TickHeartbeat; // ST + user lock
            PacketHandlers[0x79] = PacketHandler_0x79_Status; // ST
            PacketHandlers[0x7B] = PacketHandler_0x7B_RequestMetafile; // ST
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
                {
                    MerchantMenuItem.DepositItem, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_DepositItem)
                },
                {
                    MerchantMenuItem.DepositItemMenu, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_DepositItemMenu)
                },
                {
                    MerchantMenuItem.DepositItemQuantity, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_DepositItemQuantity)
                },
                {
                    MerchantMenuItem.DepositGoldMenu, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_DepositGoldMenu)
                },
                {
                    MerchantMenuItem.WithdrawGoldMenu, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_WithdrawGoldMenu)
                },
                {
                    MerchantMenuItem.WithdrawItem, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_WithdrawItem)
                },
                {
                    MerchantMenuItem.WithdrawItemMenu, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_WithdrawItemMenu)
                },
                {
                    MerchantMenuItem.WithdrawItemQuantity, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_WithdrawItemQuantity)
                },
                {
                    MerchantMenuItem.WithdrawGoldQuantity, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_WithdrawGoldQuantity)
                },
                {
                    MerchantMenuItem.DepositGoldQuantity, new MerchantMenuHandler(MerchantJob.Bank, MerchantMenuHandler_DepositGoldQuantity)
                },
                {
                    MerchantMenuItem.RepairItemMenu, new MerchantMenuHandler(MerchantJob.Repair, MerchantMenuHandler_RepairItemMenu)
                },
                {
                    MerchantMenuItem.RepairItem, new MerchantMenuHandler(MerchantJob.Repair, MerchantMenuHandler_RepairItem)
                },
                {
                    MerchantMenuItem.RepairItemAccept, new MerchantMenuHandler(MerchantJob.Repair, MerchantMenuHandler_RepairItemAccept)
                },
                {
                    MerchantMenuItem.RepairAllItems, new MerchantMenuHandler(MerchantJob.Repair, MerchantMenuHandler_RepairAllItems)
                },
                {
                    MerchantMenuItem.RepairAllItemsAccept, new MerchantMenuHandler(MerchantJob.Repair, MerchantMenuHandler_RepairAllItemsAccept)
                },
            };
        }

        #endregion Set Handlers

        public void DeleteUser(string username) => WorldData.Remove<User>(username);

        public void AddUser(User userobj) => WorldData.Set(userobj.Name, userobj);

        public bool TryGetActiveUser(string name, out User user) => WorldData.TryGetValue(name, out user);

        public bool UserConnected(string name) => ActiveUsersByName.ContainsKey(name);

        public bool TryAsyncDialog(VisibleObject invoker, User invokee, DialogSequence startSequence)
        {
            var request = new AsyncDialogRequest(startSequence, invoker, invokee);
            if (request.CheckRequest())
            {
                var key = new Tuple<UInt32, UInt32>(invoker.Id, invokee.Id);
                if (ActiveAsyncDialogs.TryAdd(key, request))
                {
                    ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.DialogRequest, request));
                    return true;
                }
                else
                {
                    Log.Error($"Async dialog: enqueue request failed for {invoker.Name} -> {invokee.Name}, already exists?");
                    return false;
                }
            }
            else
                Log.Warning($"Async dialog: request denied for {invoker.Name} -> {invokee.Name}, status checks failed");
            return false;
        }

        public void CompleteAsyncDialog(AsyncDialogRequest request)
        {

        }
        public override void Shutdown()
        {
            GameLog.WarningFormat("Shutdown initiated, disconnecting {0} active users", ActiveUsers.Count);

            Active = false;
            foreach (var connection in ActiveUsers)
            {
                var user = connection.Value;
                user.Logoff(true);
            }
            Listener?.Close();
            GameLog.Warning("World: Shutdown complete");
        }

        #region Control Message Handlers

        private void ControlMessage_CleanupUser(HybrasylControlMessage message)
        {
            // clean up after a broken connection
            var connectionId = (long)message.Arguments[0];
            User user;
            if (ActiveUsers.TryRemove(connectionId, out user))
            {
                GameLog.InfoFormat("cid {0}: closed, player {1} removed", connectionId, user.Name);
                if (user.ActiveExchange != null)
                    user.ActiveExchange.CancelExchange(user);
                ((IDictionary)ActiveUsersByName).Remove(user.Name);
                user.UpdateLogoffTime();
                user.Map?.Remove(user);
                user.Group?.Remove(user);
                Remove(user);
                GameLog.DebugFormat("cid {0}: {1} cleaned up successfully", user.Name);
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
                double fixedRegenBuff = Math.Min(user.Stats.Regen * 0.0015, 0.15);
                fixedRegenBuff = Math.Max(fixedRegenBuff, 0.125);
                if (user.Stats.Hp != user.Stats.MaximumHp)
                {
                    hpRegen = (uint)Math.Min(user.Stats.MaximumHp * (0.1 * Math.Max(user.Stats.Con, (user.Stats.Con - user.Stats.Level)) * 0.01),
                        user.Stats.MaximumHp * 0.20);
                    hpRegen = hpRegen + (uint)(fixedRegenBuff * user.Stats.MaximumHp);
                }
                if (user.Stats.Mp != user.Stats.MaximumMp)
                {
                    mpRegen = (uint)Math.Min(user.Stats.MaximumMp * (0.1 * Math.Max(user.Stats.Int, (user.Stats.Int - user.Stats.Level)) * 0.01),
                        user.Stats.MaximumMp * 0.20);
                    mpRegen = mpRegen + (uint)(fixedRegenBuff * user.Stats.MaximumMp);
                }
                GameLog.DebugFormat("User {0}: regen HP {1}, MP {2}", user.Name,
                    hpRegen, mpRegen);

                user.Stats.Hp = Math.Min(user.Stats.Hp + hpRegen, user.Stats.MaximumHp);
                user.Stats.Mp = Math.Min(user.Stats.Mp + mpRegen, user.Stats.MaximumMp);
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
                GameLog.DebugFormat("Saving user {0}", user.Name);
                user.Save();
            }
            else
            {
                GameLog.WarningFormat("Tried to save user associated with connection ID {0} but user doesn't exist",
                    connectionId);
            }
        }

        private void ControlMessage_ShutdownServer(HybrasylControlMessage message)
        {
            // Initiate an orderly shutdown
            var userName = (string)message.Arguments[0];
            GameLog.WarningFormat("Server shutdown request initiated by {0}", userName);
            // Chaos is Rising Up, yo.
            foreach (var connection in ActiveUsers)
            {
                var user = connection.Value;
                user.SendMessage("Chaos is rising up. Please re-enter in a few minutes.",
                    MessageTypes.SYSTEM_WITH_OVERHEAD);
            }

            // Actually shut down the server. This terminates the listener loop in Game.
            if (Game.IsActive())
                Game.ToggleActive();

            GameLog.WarningFormat("Server has begun shutdown");
        }

        private void ControlMessage_LogoffUser(HybrasylControlMessage message)
        {
            // Log off the specified user
            var userName = (string)message.Arguments[0];
            GameLog.WarningFormat("{0}: forcing logoff", userName);
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
            GameLog.DebugFormat("mail: attempting to notify {0} of new mail", userName);
            User user;
            if (WorldData.TryGetValue(userName, out user))
            {
                user.UpdateAttributes(StatUpdateFlags.Secondary);
                GameLog.DebugFormat("mail: notification to {0} sent", userName);
            }
            else
            {
                GameLog.DebugFormat("mail: notification to {0} failed, not logged in?", userName);
            }
        }

        private void ControlMessage_StatusTick(HybrasylControlMessage message)
        {
            var objectId = (uint)message.Arguments[0];
            if (Objects.TryGetValue(objectId, out WorldObject wobj))
            {
                if (wobj is Creature creature)
                    creature.ProcessStatusTicks();
                else
                    GameLog.Error($"Status tick on non-creature? {wobj.Name}");
            }
        }

        
        private void ControlMessage_TriggerRefresh(HybrasylControlMessage message)
        {
            var connectionId = (long)message.Arguments[0];
            if (ActiveUsers.TryGetValue(connectionId, out User user))
                user.Refresh();
        }

        private void ControlMessage_DialogRequest(HybrasylControlMessage message)
        {
            var asyncDialog = (AsyncDialogRequest)message.Arguments[0];
            asyncDialog.ShowTo();
        }

        private void ControlMessage_HandleDeath(HybrasylControlMessage message)
        {
            var creature = (Objects.Creature)message.Arguments[0];
            if (creature is User) { (creature as User).OnDeath(); }
            if (creature is Monster) { (creature as Monster).OnDeath(); }
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

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, Xml.CreatureCondition.Paralyze, PlayerFlags.InDialog)]
        private void PacketHandler_0x06_Walk(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var direction = packet.ReadByte();
            if (direction > 3) return;
            user.Condition.Casting = false;
            user.Walk((Xml.Direction)direction);
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x07_PickupItem(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();
            var x = packet.ReadInt16();
            var y = packet.ReadInt16();

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

            // Are we picking up an item from a reactor tile? 
            // If so, we remove the item from the map and pass it onto the reactor
            // for handling. Note that if the reactor does something stupid, the
            // item is probably going to be lost forever. 
            // We do it this way to provide maximum flexibility to scripts 
            // (for instance: a reactor that destroys items outright, or damages them
            // before being picked up, etc)
            Reactor reactor;
            var coordinates = new Tuple<byte, byte>((byte)x, (byte)y);
            if (user.Map.Reactors.TryGetValue(coordinates, out reactor))
            {
                // Remove the item from the map
                if (pickupObject is Gold)
                    user.Map.RemoveGold(pickupObject as Gold);
                else
                    user.Map.Remove(pickupObject as ItemObject);
                // Hopefully the reactor will DTRT
                reactor.OnTake(user, pickupObject);
                return;
            }

            // If the add is successful, remove the item from the map quadtree
            if (pickupObject is Gold)
            {
                var gold = (Gold)pickupObject;
                if (user.AddGold(gold))
                {
                    GameLog.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
                        gold.Name, gold.Amount, user.Map.Name, x, y);
                    user.Map.RemoveGold(gold);
                }
            }
            else if (pickupObject is ItemObject)
            {
                var item = (ItemObject)pickupObject;
                if (item.UniqueInventory && user.Inventory.Contains(item.TemplateId))
                {
                    user.SendMessage(string.Format("You can't carry any more of those.", item.Name), 3);
                    return;
                }

                item.DeathPileOwner = string.Empty;
                item.ItemDropAllowedLooters = new List<string>();
                item.ItemDropTime = null;
                item.ItemDropType = ItemDropType.Normal;

                if (item.Stackable && user.Inventory.Contains(item.TemplateId))
                {
                    byte existingSlot = user.Inventory.SlotOf(item.TemplateId);
                    var existingItem = user.Inventory[existingSlot];

                    int maxCanGive = existingItem.MaximumStack - existingItem.Count;
                    int quantity = Math.Min(item.Count, maxCanGive);

                    item.Count -= quantity;
                    existingItem.Count += quantity;

                    GameLog.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
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
                    GameLog.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
                        item.Name, item.Count, user.Map.Name, x, y);
                    user.Map.Remove(item);
                    user.AddItem(item, slot);
                }
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x08_DropItem(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();
            var x = packet.ReadInt16();
            var y = packet.ReadInt16();
            var count = packet.ReadInt32();

            GameLog.DebugFormat("{0} {1} {2} {3}", slot, x, y, count);

            // Do a few sanity checks
            //
            // Is the distance valid? (Can't drop things beyond
            // MAXIMUM_DROP_DISTANCE tiles away)
            if (Math.Abs(x - user.X) > Constants.PICKUP_DISTANCE ||
                Math.Abs(y - user.Y) > Constants.PICKUP_DISTANCE)
            {
                GameLog.ErrorFormat("Request to drop item exceeds maximum distance {0}",
                    Hybrasyl.Constants.MAXIMUM_DROP_DISTANCE);
                return;
            }

            // Is this a valid slot?
            if ((slot == 0) || (slot > Hybrasyl.Constants.MAXIMUM_INVENTORY))
            {
                GameLog.ErrorFormat("Slot not valid. Aborting");
                return;
            }

            // Does the player actually have an item in the slot? Does the count in the packet exceed the
            // count in the player's inventory?  Are they trying to drop the item on something that
            // is impassable (i.e. a wall)?
            if ((user.Inventory[slot] == null) || (count > user.Inventory[slot].Count) ||
                (user.Map.IsWall[x, y] == true) || !user.Map.IsValidPoint(x, y))
            {
                GameLog.ErrorFormat(
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
            }
            else
            {
                user.RemoveItem(slot);
            }
            // Item is being dropped and is "in the world" again
            Insert(toDrop);

            // This is a normal item, not part of a loot anything
            toDrop.ItemDropTime = DateTime.Now;
            toDrop.ItemDropType = ItemDropType.Normal;
            // Are we dropping an item onto a reactor?
            Reactor reactor;
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
            var cmdPrefix = Game.Config.Handlers?.Chat?.CommandPrefix ?? "/";

            string argString;
            string cmd;
            if (message.StartsWith(cmdPrefix) && (Game.Config.Handlers?.Chat?.CommandsEnabled ?? true))
            {
                // Strip prefix first
                var prefixRemoved = message.Remove(0, message.IndexOf(cmdPrefix) + cmdPrefix.Length);
                if (message.IndexOf(' ') != -1)
                    cmd = prefixRemoved.Remove(message.IndexOf(' ') - 1);
                else
                    cmd = prefixRemoved;
                if (cmd.Length + cmdPrefix.Length != message.Length)
                    argString = prefixRemoved.Remove(prefixRemoved.IndexOf(cmd), cmd.Length).Trim();
                else
                    argString = string.Empty;
                GameLog.Info($"{cmd}: {argString}");
                CommandHandler.Handle(user, cmd, argString);
            }
            else
            {
                if (!user.Condition.Alive)
                {
                    user.SendSystemMessage("Your voice is carried away by a sudden wind.");
                    return;
                }

                if (isShout == 1)
                    user.Shout(message);
                else
                    user.Say(message);
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, Xml.CreatureCondition.Paralyze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x0F_UseSpell(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();
            var target = packet.ReadUInt32();
            user.UseSpell(slot, target);
            user.Condition.Casting = false;
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

                // Remove any active async dialog sessions
                foreach (var dialog in ActiveAsyncDialogs.Keys.Where(key => key.Item1 == user.Id || key.Item2 == user.Id))
                {
                    if (ActiveAsyncDialogs.TryRemove(dialog, out AsyncDialogRequest request))
                        request.End();
                }

                if (ActiveUsersByName.TryRemove(user.Name, out connectionId))
                {
                    ((IDictionary)ActiveUsers).Remove(connectionId);
                }
                GameLog.InfoFormat("cid {0}: {1} leaving world", connectionId, user.Name);
            }
        }

        private void PacketHandler_0X0C_PutGround(object obj, ClientPacket packet)
        {
            //do nothing. only here to remove the stupid spam.
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
            loginUser.ReapplyStatuses();
            loginUser.SetCitizenship();
            loginUser.ChrysalisMark();

            // Clear conditions and dialog states
            loginUser.Condition.Casting = false;

            Insert(loginUser);
            GameLog.DebugFormat("Elapsed time since login: {0}", loginUser.SinceLastLogin);

            if (!loginUser.Condition.Alive)
            {
                loginUser.Teleport("Chaotic Threshold", 10, 10);
            }
            else if (loginUser.Login.FirstLogin)
            {
                Xml.NewPlayer handler = Game.Config.Handlers?.NewPlayer;
                var targetmap = WorldData.First<Map>();
                if (handler != null)
                {
                    Xml.StartMap startmap = handler.GetStartMap();
                    loginUser.Login.FirstLogin = false;
                    if (WorldData.TryGetValueByIndex(startmap.Value, out Map map))
                        loginUser.Teleport(map.Id, startmap.X, startmap.Y);
                    else
                    {
                        // Teleport user to the center of the first known map and hope for the best
                        loginUser.Teleport(targetmap.Id, (byte)(targetmap.X / 2), (byte)(targetmap.Y / 2));
                        GameLog.Error($"{loginUser.Name} first login: map {startmap.Value} not found, using first available map {targetmap.Name}. Safety not guaranteed.");
                    }
                }
                else
                {
                    loginUser.Teleport(targetmap.Id, (byte)(targetmap.X / 2), (byte)(targetmap.Y / 2));
                    GameLog.Error($"{loginUser.Name} first login: start map config missing, using first available map {targetmap.Name}. Safety not guaranteed.");
                }
            }
            else if (loginUser.Nation.SpawnPoints.Count != 0 &&
                loginUser.SinceLastLogin > Hybrasyl.Constants.NATION_SPAWN_TIMEOUT)
            {
                var spawnpoint = loginUser.Nation.RandomSpawnPoint;
                if (spawnpoint != null && !string.IsNullOrEmpty(spawnpoint.MapName)) loginUser.Teleport(spawnpoint.MapName, spawnpoint.X, spawnpoint.Y);
                else loginUser.Teleport((ushort)500, (byte)50, (byte)(50));
            }
            else if (WorldData.ContainsKey<Map>(loginUser.Location.MapId))
            {
                loginUser.Teleport(loginUser.Location.MapId, (byte)loginUser.Location.X, (byte)loginUser.Location.Y);
            }
            else
            {
                // Handle any weird cases where a map someone exited on was deleted, etc
                // This "default" of Mileth should be set somewhere else
                loginUser.Teleport((ushort)500, (byte)50, (byte)50);
            }

            GameLog.DebugFormat("Adding {0} to hash", loginUser.Name);
            AddUser(loginUser);
            ActiveUsers[connectionId] = loginUser;
            ActiveUsersByName[loginUser.Name] = connectionId;
            GameLog.InfoFormat("cid {0}: {1} entering world", connectionId, loginUser.Name);
            GameLog.InfoFormat($"{loginUser.SinceLastLoginstring}");
            // If the user's never logged off before (new character), don't display this message.
            if (loginUser.Login.LastLogoff != default(DateTime))
            {
                loginUser.SendSystemMessage($"It has been {loginUser.SinceLastLoginstring} since your last login.");
            }
            loginUser.SendSystemMessage(HybrasylTime.Now.ToString());
            loginUser.Reindex();
        }

        [Prohibited(Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        private void PacketHandler_0x11_Turn(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var direction = packet.ReadByte();
            if (direction > 3) return;
            user.Condition.Casting = false;
            user.Turn((Xml.Direction)direction);
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, Xml.CreatureCondition.Paralyze, PlayerFlags.InDialog)]
        private void PacketHandler_0x13_Attack(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            user.AssailAttack(user.Direction);
        }

        private void PacketHandler_0x18_ShowPlayerList(Object obj, ClientPacket packet)
        {
            var me = (User)obj;

            var list = from user in ActiveUsers.Values
                       orderby user.IsMaster descending, user.Stats.Level descending, user.Stats.BaseHp + user.Stats.BaseMp * 2 descending, user.Name ascending
                       select user;

            var listPacket = new ServerPacket(0x36);
            listPacket.WriteUInt16((ushort)list.Count());
            listPacket.WriteUInt16((ushort)list.Count());

            foreach (var user in list)
            {
                int levelDifference = Math.Abs((int)user.Stats.Level - me.Stats.Level);

                listPacket.WriteByte((byte)user.Class);
                if (!string.IsNullOrEmpty(me.GuildUuid) && user.GuildUuid == me.GuildUuid) listPacket.WriteByte(84);
                else if (levelDifference <= 5) listPacket.WriteByte(151);
                else listPacket.WriteByte(255);

                listPacket.WriteByte((byte)user.GroupStatus);
                listPacket.WriteString8(user.Title);
                listPacket.WriteBoolean(user.IsMaster);
                listPacket.WriteString8(user.Name);
            }
            me.Enqueue(listPacket);
        }

        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x19_Whisper(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var size = packet.ReadByte();
            var target = Encoding.ASCII.GetString(packet.Read(size));
            var msgsize = packet.ReadByte();
            var message = Encoding.ASCII.GetString(packet.Read(msgsize));

            // "!!" is the special character sequence for group whisper. If this is the
            // target, the message should be sent as a group whisper instead of a standard
            // whisper.
            if (target == "!!")
            {
                user.SendGroupWhisper(message);
            }
            else if (target == "$")
            {
                if (!user.IsPrivileged)
                {
                    user.SendSystemMessage("Forbidden.");
                    return;
                }

                Scripting.Script script;

                if (!ScriptProcessor.TryGetScript($"{user.Name}-repl.lua", out script) || message.ToLower().Contains("--clear--"))
                {
                    // Make new magic script if needed
                    if (ScriptProcessor.TryGetScript("repl.lua", out Scripting.Script newScript))
                    {
                        newScript.Name = $"{user.Name}-repl.lua";
                        ScriptProcessor.RegisterScript(newScript);
                        newScript.Execute($"init('{user.Name}')", user);
                        user.DisplayIncomingWhisper("$", "Eval environment ready");
                        return;
                    }
                    else
                    {
                        user.SendSystemMessage("repl.lua needs to exist as a script first");
                        return;
                    }
                }
                user.DisplayOutgoingWhisper("$", message);
                // Tack on return here so we actually get the DynValue out
                var ret = script.Execute($"return {message}", user);
                if (!ret)
                {
                    var strs = script.LastRuntimeError.Split(50);
                    foreach (var str in strs)
                        user.DisplayIncomingWhisper("$", $"Err: {str}");
                }
                else
                {
                    if (script.LastReturnValue == DynValue.Nil || script.LastReturnValue == DynValue.Void)
                        user.DisplayIncomingWhisper("$", "Ret: nil (OK)");
                    // this is deeply annoying and stupid
                    else if (script.LastReturnValue.Type == DataType.Boolean)
                        user.DisplayIncomingWhisper("$", $"Ret: {script.LastReturnValue.Boolean.ToString()}");
                    else
                        user.DisplayIncomingWhisper("$", $"Ret: {script.LastReturnValue.CastToString()}");

                }
                return;

            }
            else
                user.SendWhisper(target, message);
        }

        private void PacketHandler_0x1B_Settings(Object obj, ClientPacket packet)
        {
            // TODO: future expansion
            var settingNumber = packet.ReadByte();
            var user = obj as User;
            // Only seven of these are usable by the client (1-6, and 8), 
            // the seventh one is sent to keep the ordering consistent but does nothing
            var settings = new List<byte>() { 1, 2, 3, 4, 5, 6, 7, 8 };
            if (settingNumber == 0)
            {
                // Send all settings
                foreach (var x in settings)
                {
                    if (!user.ClientSettings.ContainsKey(x))
                        user.ClientSettings[x] = false;
                }
                // for the record this is a very strange usage of a message packet
                var settingsString = string.Join(" \t", settings.Select(x => string.Format("Setting {0}: {1}", x, user.ClientSettings[x])));
                var x0a = new ServerPacketStructures.SettingsMessage()
                {
                    DisplayString = settingsString,
                    Number = 0
                };
                var settingsPacket = x0a.Packet();
                user.Enqueue(settingsPacket);

            }
            else
            {
                // Set individual setting
                if (!user.ClientSettings.ContainsKey(settingNumber))
                    user.ClientSettings[settingNumber] = false;
                else
                    user.ClientSettings[settingNumber] = !user.ClientSettings[settingNumber];
                var displayString = $"Setting {settingNumber}: {user.ClientSettings[settingNumber]}";
                var x0a = new ServerPacketStructures.SettingsMessage() { DisplayString = displayString, Number = settingNumber };
                var settingspacket = x0a.Packet();
                user.Enqueue(settingspacket);
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x1C_UseItem(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();

            GameLog.DebugFormat("Updating slot {0}", slot);

            if (slot == 0 || slot > Constants.MAXIMUM_INVENTORY) return;

            var item = user.Inventory[slot];

            if (item == null) return;

            switch (item.ItemObjectType)
            {
                case Enums.ItemObjectType.CanUse:
                    if (item.Durability == 0 && !((item?.EquipmentSlot ?? ClientItemSlots.None) == ClientItemSlots.None))
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
                        string message;
                        if (!item.CheckRequirements(user, out message))
                        {
                            // If an item can't be equipped, CheckRequirements will return false
                            // and also set the appropriate message for us via out
                            user.SendMessage(message, 3);
                            return;
                        }
                        GameLog.DebugFormat("Equipping {0}", item.Name);
                        // Remove the item from inventory, but we don't decrement its count, as it still exists.
                        user.RemoveItem(slot);

                        // Handle gauntlet / ring special cases
                        if (item.EquipmentSlot == ClientItemSlots.Gauntlet)
                        {
                            GameLog.DebugFormat("item is gauntlets");
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
                            GameLog.DebugFormat("item is ring");

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
                                GameLog.DebugFormat(" Attemping to equip {0}", item.Name);
                                GameLog.DebugFormat("..which would unequip {0}", oldItem.Name);
                                GameLog.DebugFormat("Player weight is currently {0}", user.CurrentWeight);
                                user.RemoveEquipment(equipSlot);
                                user.AddItem(oldItem, slot);
                                user.AddEquipment(item, equipSlot);
                                user.Show();
                                GameLog.DebugFormat("Player weight is currently {0}", user.CurrentWeight);
                            }
                            else
                            {
                                GameLog.DebugFormat("Attemping to equip {0}", item.Name);
                                user.AddEquipment(item, equipSlot);
                                user.Show();
                            }
                        }
                    }
                    break;
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze)]
        [Required(PlayerFlags.Alive)]
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

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x24_DropGold(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var amount = packet.ReadUInt32();
            var x = packet.ReadInt16();
            var y = packet.ReadInt16();

            GameLog.DebugFormat("{0} {1} {2}", amount, x, y);
            // Do a few sanity checks

            // Is the distance valid? (Can't drop things beyond
            // MAXIMUM_DROP_DISTANCE tiles away)
            if (Math.Abs(x - user.X) > Constants.PICKUP_DISTANCE ||
                Math.Abs(y - user.Y) > Constants.PICKUP_DISTANCE)
            {
                GameLog.ErrorFormat("Request to drop gold exceeds maximum distance {0}",
                    Hybrasyl.Constants.MAXIMUM_DROP_DISTANCE);
                return;
            }

            // Does the amount in the packet exceed the
            // amount of gold the player has?  Are they trying to drop the item on something that
            // is impassable (i.e. a wall)?
            if ((amount > user.Gold) || (x >= user.Map.X) || (y >= user.Map.Y) ||
                (x < 0) || (y < 0) || (user.Map.IsWall[x, y]))
            {
                GameLog.ErrorFormat("Amount {0} exceeds amount {1}, or {2},{3} is a wall, or {2},{3} is out of bounds",
                    amount, user.Gold, x, y);
                return;
            }

            var toDrop = new Gold(amount);
            user.RemoveGold(amount);

            Insert(toDrop);

            // This is a normal item, not part of a loot/death pile
            toDrop.ItemDropTime = DateTime.Now;
            toDrop.ItemDropType = ItemDropType.Normal;

            // Are we dropping an item onto a reactor?
            Reactor reactor;
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
            //this handler also handles group management pane

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
        [Prohibited(Xml.CreatureCondition.Coma, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x2E_GroupRequest(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            // stage:
            //   0x02 = user is sending initial request to invitee
            //   0x03 = invitee responds with a "yes"
            byte stage = packet.ReadByte();

            if (!TryGetActiveUser(packet.ReadString8(), out User partner))
                return;

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
                    GameLog.DebugFormat("{0} invites {1} to join a group.", user.Name, partner.Name);

                    // Remove the user from the group. Kinda logically weird beside all of this other stuff
                    // so it may be worth restructuring but it should be accurate as-is.
                    if (partner.Grouped && user.Grouped && partner.Group == user.Group)
                    {
                        GameLog.DebugFormat("{0} leaving group.", user.Name);
                        user.Group.Remove(partner);
                        return;
                    }

                    // Now we know that we're trying to add this person to the group, not remove them.
                    // Let's find out if they're eligible and invite them if so.
                    if (partner.Grouped)
                    {
                        user.SendMessage(string.Format("{0} is already in a group.", partner.Name), MessageTypes.SYSTEM);
                        return;
                    }

                    if (!partner.Grouping)
                    {
                        user.SendMessage(string.Format("{0} is not accepting group invitations.", partner.Name), MessageTypes.SYSTEM);
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
                    GameLog.Debug("Invitation accepted. Grouping.");
                    partner.InviteToGroup(user);
                    break;

                default:
                    GameLog.Error("Unknown GroupRequest stage. No action taken.");
                    break;
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
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

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
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
                else if (target is Objects.Creature && user.IsInViewport((VisibleObject)target))
                {
                    // Give gold to Creature and go about our lives
                    var creature = (Objects.Creature)target;
                    creature.Gold += goldAmount;
                    user.Gold -= goldAmount;
                    user.UpdateAttributes(StatUpdateFlags.Stats);
                }
                else
                {
                    GameLog.DebugFormat("user {0}: invalid drop target");
                }
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
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
                else if (target is Objects.Creature && user.IsInViewport((VisibleObject)target))
                {
                    var creature = (Objects.Creature)target;
                    var item = user.Inventory[itemSlot];
                    if (item != null)
                    {
                        if (user.RemoveItem(itemSlot))
                        {
                            creature.Inventory.AddItem(item);
                        }
                        else
                            GameLog.WarningFormat("0x29: Couldn't remove item from inventory...?");
                    }
                }
            }
        }

        [Required(PlayerFlags.Alive)]
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

            GameLog.DebugFormat("Moving {0} to {1}", oldSlot, newSlot);

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

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x3B_AccessMessages(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var response = new ServerPacket(0x31);
            var action = packet.ReadByte();

            // The moment we get a 3B packet, we assume a user is "in a board"
            user.Condition.Flags = user.Condition.Flags | PlayerFlags.InBoard;

            switch (action)
            {
                case 0x01:
                    {
                        // Display board list
                        response.WriteByte(0x01);

                        // TODO: This has the potential to be a somewhat expensive operation, optimize this.
                        var boardList =
                            WorldData.Values<Board>().Where(mb => mb.Global && mb.CheckAccessLevel(user.Name, BoardAccessLevel.Read));

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
                            if (WorldData.TryGetValueByIndex<Board>(boardId, out board))
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
                            if (WorldData.TryGetValue<Board>(boardId, out board))
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
                        if (WorldData.TryGetValue(boardId, out board))
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
                            GameLog.WarningFormat("boards: {0} tried to post to non-existent board {1}",
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
                        if (WorldData.TryGetValue(boardId, out board))
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
                            GameLog.WarningFormat("boards: {0} tried to post to non-existent board {1}",
                                user.Name, boardId);
                            response.WriteBoolean(false);
                            response.WriteString8(
                                "...What would you say you're doing here?");
                        }
                    }
                    break;

                case 0x06:
                    {
                        // TODO: refactor big switch statement
                        //
                        // Send mail (which one might argue, ye olde DOOMVAS protocol designers, is a type of message)

                        var boardId = packet.ReadUInt16();
                        var recipient = packet.ReadString8();
                        var subject = packet.ReadString8();
                        var body = packet.ReadString16();
                        bool continueProcessing = true;

                        // Handle plugin response
                        var plugin = ResolveMessagingPlugin(Xml.MessageType.Mail, new Plugins.Message(Xml.MessageType.Mail, user.Name, recipient, subject, body));

                        response.WriteByte(0x06); // Send post response

                        if (plugin is IProcessingMessageHandler pmh)
                        {
                            var msg = new Plugins.Message(Xml.MessageType.Mail, user.Name, recipient, subject, body);
                            var resp = pmh.Process(msg);
                            if (!pmh.Passthrough)
                            {
                                // Plugin is "last destination" for message
                                continueProcessing = false;
                                response.WriteBoolean(resp.Success); 
                                response.WriteString8(resp.PluginResponse);
                            }
                            else if (resp.Transformed)
                            {
                                // Update message if transformed, and keep going
                                recipient = resp.Message.Recipient;
                                subject = resp.Message.Subject;
                                body = resp.Message.Text;                              
                            }
                        }

                        User recipientUser;

                        if (WorldData.TryGetValue(recipient, out recipientUser) && continueProcessing)
                        {
                            try
                            {
                                if (recipientUser.Mailbox.ReceiveMessage(new Message(recipientUser.Name, user.Name, subject,
                                    body)))
                                {
                                    response.WriteBoolean(true); // Post was successful
                                    response.WriteString8("Your letter was sent.");
                                    GameLog.InfoFormat("mail: {0} sent message to {1}", user.Name, recipientUser.Name);
                                    ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MailNotifyUser,
                                        recipientUser.Name));
                                }
                                else
                                {
                                    response.WriteBoolean(true);
                                    response.WriteString8("{0}'s mailbox is full. Your message was discarded. Sorry!");
                                }
                            }
                            catch (MessageStoreLocked e)
                            {
                                Game.ReportException(e);
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
                            GameLog.WarningFormat("mail: {0} tried to highlight message {1} but isn't GM! Hijinx suspected.",
                                user.Name, postId);
                        }
                        if (WorldData.TryGetValue(boardId, out board))
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

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, Xml.CreatureCondition.Paralyze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x3E_UseSkill(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var slot = packet.ReadByte();

            user.UseSkill(slot);
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        private void PacketHandler_0x3F_MapPointClick(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var target = BitConverter.ToInt64(packet.Read(8), 0);
            GameLog.DebugFormat("target bytes are: {0}, maybe", target);

            if (user.IsAtWorldMap)
            {
                MapPoint targetmap;
                if (WorldData.TryGetValue<MapPoint>(target, out targetmap))
                {
                    user.Teleport(targetmap.DestinationMap, targetmap.DestinationX, targetmap.DestinationY);
                }
                else
                {
                    GameLog.ErrorFormat(string.Format("{0}: sent us a click to a non-existent map point!",
                        user.Name));
                }
            }
            else
            {
                GameLog.ErrorFormat(string.Format("{0}: sent us an 0x3F outside of a map screen!",
                    user.Name));
            }
        }

        [Prohibited(PlayerFlags.InDialog)]
        private void PacketHandler_0x38_Refresh(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            user.Condition.Casting = false;
            user.Refresh();
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze)]
        private void PacketHandler_0x39_NPCMainMenu(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            // We just ignore the header, because, really, what exactly is a 16-bit encryption
            // key plus CRC really doing for you
            var header = packet.ReadDialogHeader();
            var objectType = packet.ReadByte();
            var objectId = packet.ReadUInt32();
            var pursuitId = packet.ReadUInt16();

            GameLog.DebugFormat("main menu packet: ObjectType {0}, ID {1}, pursuitID {2}",
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
                    if (!GlobalSequences.TryGetValue(pursuitId, out pursuit))
                    {
                        GameLog.ErrorFormat("{0}: pursuit ID {1} doesn't exist in the global catalog?",
                            wobj.Name, pursuitId);
                        return;
                    }
                }
                else if (pursuitId >= Constants.DIALOG_SEQUENCE_HARDCODED)
                {
                    if (!(wobj is Merchant))
                    {
                        GameLog.ErrorFormat("{0}: attempt to use hardcoded merchant menu item on non-merchant",
                            wobj.Name, pursuitId);
                        return;
                    }

                    var menuItem = (MerchantMenuItem)pursuitId;
                    var merchant = (Merchant)wobj;
                    MerchantMenuHandler handler;

                    if (!merchantMenuHandlers.TryGetValue(menuItem, out handler))
                    {
                        GameLog.ErrorFormat("{0}: merchant menu item {1} doesn't exist?",
                            wobj.Name, menuItem);
                        return;
                    }

                    if (!merchant.Jobs.HasFlag(handler.RequiredJob))
                    {
                        GameLog.ErrorFormat("{0}: merchant does not have required job {1}",
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
                        GameLog.ErrorFormat("{0}: local pursuit {1} doesn't exist?", wobj.Name, pursuitId);
                        return;
                    }
                }
                GameLog.DebugFormat("{0}: showing initial dialog for Pursuit {1} ({2})",
                    clickTarget.Name, pursuit.Id, pursuit.Name);
                user.DialogState.StartDialog(clickTarget, pursuit);
                pursuit.ShowTo(user, clickTarget);
            }
            else
            {
                GameLog.WarningFormat("specified object ID {0} doesn't exist?", objectId);
                return;
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze)]
        private void PacketHandler_0x3A_DialogUse(Object obj, ClientPacket packet)
        {
            var user = (User)obj;

            var header = packet.ReadDialogHeader();
            var objectType = packet.ReadByte();
            var objectID = packet.ReadUInt32();
            var pursuitID = packet.ReadUInt16();
            var pursuitIndex = packet.ReadUInt16();

            GameLog.DebugFormat($"0x3A   user: {user.Name} objectType {objectType} objectID {objectID} pursuitID {pursuitID} pursuitIndex {pursuitIndex}");

            GameLog.DebugFormat("0x3A   DialogState: previous {prev}, current {cur}, pursuitIndex {pidx}",
                user.DialogState.PreviousPursuitId?.ToString() ?? "null",
                user.DialogState.CurrentPursuitId, 
                user.DialogState.CurrentPursuitIndex);

            AsyncDialogRequest request = null;
            VisibleObject source = null;

            // Is this an async dialog session (either one in progress, or one starting)
            if (objectID == UInt32.MaxValue)
            {
                // TODO: optimize
                var asynckeys = ActiveAsyncDialogs.Keys.Where(key => key.Item1 == user.Id || key.Item2 == user.Id);
                if (asynckeys.Count() == 1)
                {
                    if (!ActiveAsyncDialogs.TryGetValue(asynckeys.First(), out request))
                    {
                        GameLog.Error("WARNING: {Name}: Async count was nonzero but session could not be found", user.Name);
                        return;
                    }
                    // If we are processing an asynchronous dialog request, make sure the source is set
                    // to the other side of the session so it can be sent to callbacks
                    source = request.Invokee.Name == user.Name ? request.Invoker : request.Invokee;
                }
                else if (asynckeys.Count() > 1)
                {
                    GameLog.Fatal("WARNING: Multiple async sessions for {Name} detected", user.Name);
                    return;
                }
            }

            if (pursuitID == user.DialogState.CurrentPursuitId && pursuitIndex == user.DialogState.CurrentPursuitIndex)
            {
                // If we get a packet back with the same index and ID, the dialog has been closed.
                GameLog.DebugFormat("Dialog closed, resetting dialog state");
                user.ClearDialogState();

                // Check for open async dialogs that need to be closed. The async dialog session will be removed as well,
                // but only if it's complete (both sides have clicked "Close" at some point)
                if (request != null)
                    // Close our side of the session
                    request.Close(user.Id);
                else
                    return;
            }

            if ((pursuitIndex > user.DialogState.CurrentPursuitIndex + 1) ||
                (pursuitIndex < user.DialogState.CurrentPursuitIndex - 1))
            {
                GameLog.ErrorFormat("Dialog index is outside of acceptable limits (next/prev)");
                return;
            }

            WorldObject wobj;

            if (user.World.Objects.TryGetValue(objectID, out wobj) || objectID == UInt32.MaxValue)
            {
                VisibleObject clickTarget = wobj as VisibleObject;

                // Was the previous button clicked? Handle that first
                if (pursuitIndex == user.DialogState.CurrentPursuitIndex - 1)
                {
                    GameLog.DebugFormat("Handling prev: client passed index {0}, current index is {1}",
                        pursuitIndex, user.DialogState.CurrentPursuitIndex);

                    if (user.DialogState.SetDialogIndex(clickTarget, pursuitID, pursuitIndex))
                    {
                        user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                        return;
                    }
                }

                // Is the active dialog an input or options dialog?
                // If so, we handle that first, as the response / callback / handler 
                // needs to be able to handle the response (which chould change the active sequence),
                // and then we need to potentially display the next dialog in sequence.

                var currPursuitId = user.DialogState.CurrentPursuitId;
                var currPursuitIndex = user.DialogState.CurrentPursuitIndex;
                var currMerchantId = user.DialogState.CurrentMerchantId;

                if (user.DialogState.ActiveDialog is OptionsDialog)
                {
                    var paramsLength = packet.ReadByte();
                    var option = packet.ReadByte();
                    var dialog = user.DialogState.ActiveDialog as OptionsDialog;

                    // If an error occurred in handling the response, it's generally safest to 
                    // simply bail out 
                    if (!dialog.HandleResponse(user, option, clickTarget, source))
                    {
                        user.ClearDialogState();
                        return;
                    }

                    // Did the response cause the current sequence or dialog id to change? 
                    // If so, simply return; otherwise, continue to process next dialog                   
                    if (user.DialogState.CurrentMerchantId != currMerchantId ||
                        user.DialogState.CurrentPursuitId != currPursuitId ||
                        user.DialogState.CurrentPursuitIndex != currPursuitIndex)
                        return;
                }

                // This logic is effectively identical to OptionsDialog
                if (user.DialogState.ActiveDialog is TextDialog)
                {
                    var paramsLength = packet.ReadByte();
                    var response = packet.ReadString8();
                    var dialog = user.DialogState.ActiveDialog as TextDialog;
                    if (!dialog.HandleResponse(user, response, clickTarget, source))
                    {
                        user.ClearDialogState();
                        return;
                    }
                    if (user.DialogState.CurrentMerchantId != currMerchantId ||
                        user.DialogState.CurrentPursuitId != currPursuitId ||
                        user.DialogState.CurrentPursuitIndex != currPursuitIndex)
                        return;
                }

                if (user.DialogState.ActiveDialog is null)
                {
                    // The response handler could have closed the dialog, or done Goddess knows what
                    // to the state. We check here, and if the dialog state is null (the result of
                    // calling EndDialog() we send a close packet.
                    user.ClearDialogState();
                    return;
                }

                // Did the user click next on the last dialog in a sequence?
                //
                // If the last dialog is a JumpDialog or FunctionDialog, just ShowTo it; it'll handle the rest.
                // Otherwise, either close the dialog or go to the main menu (main menu by 
                // default).

                if (user.DialogState.ActiveDialogSequence.Dialogs.Count() == pursuitIndex)
                {
                    if (user.DialogState.ActiveDialog is JumpDialog)
                    {
                        user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                        return;
                    }
                    if (user.DialogState.ActiveDialog is FunctionDialog)
                    {
                        var currpid = user.DialogState.CurrentPursuitId;
                        user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                        // Check to see if a script function changed the active dialog.
                        // If it did, we don't need to send a close dialog packet.
                        if (user.DialogState.CurrentPursuitId == currpid)
                        {
                            GameLog.DebugFormat("Sending close packet");
                            user.SendCloseDialog();
                        }
                        return;
                    }
                    if (user.DialogState.ActiveDialogSequence.CloseOnEnd)
                    {
                        GameLog.DebugFormat("Sending close packet");
                        user.ClearDialogState();
                        return;
                    }
                    else
                    {
                        // If this is an NPC or reactor (and has a click target), then display main menu
                        if (clickTarget != null)
                            clickTarget.DisplayPursuits(user);
                    }
                    // Either way down here, reset the dialog state since we're done with the sequence
                    user.DialogState.EndDialog();
                    // If this is an asynchronous dialog, and we've reached here, also close the dialog
                    if (request != null)
                    {
                        request.Close(user.Id);
                        user.SendCloseDialog();
                    }
                    return;
                }

                // Are we transitioning between two dialog sequences? If so, show the first dialog from
                // the new sequence and make sure we clear the previous state.
                if (user.DialogState.PreviousPursuitId == pursuitID)
                {
                    user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                    user.DialogState.PreviousPursuitId = null;
                    return;
                }

                // Did the handling of a response result in our active dialog sequence changing? If so, exit.
                if (user.DialogState.CurrentPursuitId != pursuitID)
                {
                    GameLog.ErrorFormat("Dialog has changed, exiting");
                    return;
                }

                // TODO: improve this logic
                // Handle function dialogs in between us and the next real dialog (or the end)
                if (user.DialogState.SetDialogIndex(clickTarget, pursuitID, pursuitIndex))
                {
                    while (user.DialogState.ActiveDialog is FunctionDialog)
                    {
                        var currpid = user.DialogState.CurrentPursuitId;
                        // ShowTo and go
                        user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                        // Check to see we're still in the same sequence.
                        if (currpid != user.DialogState.CurrentPursuitId)
                            return;
                        pursuitIndex++;
                        if (!user.DialogState.SetDialogIndex(clickTarget, pursuitID, pursuitIndex))
                        {
                            // We're at the end of our rope
                            user.SendCloseDialog();
                            //GameLog.Info("Dialog: closed by while loop");
                            return;
                        }
                    }
                    GameLog.DebugFormat("Pursuit index is now {0}", pursuitIndex);

                    user.DialogState.ActiveDialog.ShowTo(user, clickTarget);
                    return;
                }
                else
                {
                    GameLog.DebugFormat("Sending close packet");
                    //GameLog.Info("Dialog: closed by SetDialogIndex == false");
                    user.SendCloseDialog();
                    user.DialogState.EndDialog();
                }
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        private void PacketHandler_0x43_PointClick(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var clickType = packet.ReadByte();
            Rectangle commonViewport = user.GetViewport();

            // N.B. We handle dead checks here rather than at the Required attribute level due to some 
            // edge cases

            // User has clicked an X,Y point
            if (clickType == 3)
            {
                var x = (byte)packet.ReadUInt16();
                var y = (byte)packet.ReadUInt16();
                var coords = new Tuple<byte, byte>(x, y);
                GameLog.DebugFormat("coordinates were {0}, {1}", x, y);

                if (user.Map.Doors.ContainsKey(coords))
                {
                    if (!user.Condition.Alive)
                    {
                        user.SendSystemMessage("You try, but your hands pass right through it.");
                        return;
                    }
                    if (user.Map.Doors[coords].Closed)
                        user.SendSystemMessage("It's open.");
                    else
                        user.SendSystemMessage("It's closed.");

                    user.Map.ToggleDoors(x, y);
                }
                else if (user.Map.Signposts.ContainsKey(coords))
                {
                    user.Map.Signposts[coords].OnClick(user);
                }
                else
                {
                    GameLog.DebugFormat("User clicked {0}@{1},{2} but no door/signpost is present",
                        user.Map.Name, x, y);
                }
            }

            // User has clicked on another entity
            else if (clickType == 1)
            {
                var entityId = packet.ReadUInt32();
                GameLog.DebugFormat("User {0} clicked ID {1}: ", user.Name, entityId);

                WorldObject clickTarget = new WorldObject();

                if (user.World.Objects.TryGetValue(entityId, out clickTarget))
                {
                    Type type = clickTarget.GetType();
                    MethodInfo methodInfo = type.GetMethod("OnClick");
                    var associate = clickTarget as VisibleObject;
                    if (associate.Map == user.Map)
                    {
                        // Certain NPCs can be "spoken to" even when dead
                        if (user.LastAssociate is Merchant && (!user.Condition.Alive && !user.LastAssociate.AllowDead))
                        {
                            user.SendSystemMessage("You cannot do that now.");
                            return;
                        }
                        methodInfo.Invoke(clickTarget, new[] { user });
                    }
                    else
                    {
                        GameLog.Warning($"User {user.Name}: Click packet for object not on current map: {entityId} {clickTarget.Id} {user.Map.Name}");
                        return;
                    }
                }
            }
            else
            {
                GameLog.DebugFormat("Unsupported clickType {0}", clickType);
                GameLog.DebugFormat("Packet follows:");
                packet.DumpPacket();
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x44_EquippedItemClick(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            // This packet is received when a client unequips an item from the detail (a) screen.

            var slot = packet.ReadByte();

            GameLog.DebugFormat("Removing equipment from slot {0}", slot);
            var item = user.Equipment[slot];
            if (item != null)
            {
                GameLog.DebugFormat("actually removing item");
                user.RemoveEquipment(slot);
                // Add our removed item to our first empty inventory slot
                GameLog.DebugFormat("Player weight is currently {0}", user.CurrentWeight);
                GameLog.DebugFormat("Adding item {0}, count {1} to inventory", item.Name, item.Count);
                user.AddItem(item);
                GameLog.DebugFormat("Player weight is now {0}", user.CurrentWeight);
            }
            else
            {
                GameLog.DebugFormat("Ignoring useless click on slot {0}", slot);
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
                GameLog.WarningFormat("{0}: byte heartbeat not valid, disconnecting", user.Name);
                user.SendRedirectAndLogoff(Game.World, Game.Login, user.Name);
            }
            else
            {
                GameLog.DebugFormat("{0}: byte heartbeat valid", user.Name);
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze, PlayerFlags.InDialog)]
        [Required(PlayerFlags.Alive)]
        private void PacketHandler_0x47_StatPoint(Object obj, ClientPacket packet)
        {
            var user = (User)obj;
            if (user.LevelPoints > 0)
            {
                switch (packet.ReadByte())
                {
                    case 0x01:
                        user.Stats.BaseStr++;
                        break;

                    case 0x04:
                        user.Stats.BaseInt++;
                        break;

                    case 0x08:
                        user.Stats.BaseWis++;
                        break;

                    case 0x10:
                        user.Stats.BaseCon++;
                        break;

                    case 0x02:
                        user.Stats.BaseDex++;
                        break;

                    default:
                        return;
                }

                user.LevelPoints--;
                user.UpdateAttributes(StatUpdateFlags.Primary);
            }
        }

        [Prohibited(Xml.CreatureCondition.Coma, Xml.CreatureCondition.Sleep, Xml.CreatureCondition.Freeze)]
        [Required(PlayerFlags.Alive)]
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
                    GameLog.Debug("Cancelling trade");
                    user.ActiveExchange.CancelExchange(user);
                    break;

                case 0x05:
                    // Confirm trade
                    GameLog.Debug("Confirming trade");
                    user.ActiveExchange.ConfirmExchange(user);
                    break;

                default:
                    return;
            }
        }

        [Prohibited(PlayerFlags.InDialog)]
        private void PacketHandler_0x4D_BeginCasting(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            user.Condition.Casting = true;
        }

        [Prohibited(PlayerFlags.InDialog)]
        private void PacketHandler_0x4E_CastLine(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            var text = packet.ReadString8();

            if(text.Contains("Mastery"))
            {
                text = text.Remove(text.IndexOf(" (Mastery"));
            }
            //var textLength = packet.ReadByte();
            //var text = packet.Read(textLength);
            if (!user.Condition.Casting) return;
            var x0D = new ServerPacketStructures.CastLine() { ChatType = 2, LineLength = (byte)text.Length, LineText = text, TargetId = user.Id };
            var enqueue = x0D.Packet();

            user.SendCastLine(enqueue);

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
                GameLog.InfoFormat("{0}: tick heartbeat not valid, disconnecting", user.Name);
                user.SendRedirectAndLogoff(Game.World, Game.Login, user.Name);
            }
            else
            {
                GameLog.DebugFormat("{0}: tick heartbeat valid", user.Name);
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
                x6F.WriteUInt16((ushort)WorldData.Count<CompiledMetafile>());
                foreach (var metafile in WorldData.Values<CompiledMetafile>())
                {
                    x6F.WriteString8(metafile.Name);
                    GameLog.Info($"Responding 6F: adding {metafile.Name}, checksum {metafile.Checksum}");
                    x6F.WriteUInt32(metafile.Checksum);
                }
                user.Enqueue(x6F);
            }
            else
            {
                var name = packet.ReadString8();
                if (WorldData.ContainsKey<CompiledMetafile>(name))
                {
                    var file = WorldData.Get<CompiledMetafile>(name);
                    GameLog.Info($"Responding 6f notall: sending {file.Name}, checksum {file.Checksum}");
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
            var quantity = Convert.ToUInt32(packet.ReadString8());
            user.ShowBuyItem(merchant, (int)quantity);
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

        private void MerchantMenuHandler_SellItemAccept(User user, Merchant merchant, ClientPacket packet) =>
            user.SellItemAccept(merchant);

        private void MerchantMenuHandler_LearnSkillMenu(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowLearnSkillMenu(merchant);

        private void MerchantMenuHandler_LearnSkill(User user, Merchant merchant, ClientPacket packet)
        {
            var skillName = packet.ReadString8(); //skill name
            var skill = WorldData.GetByIndex<Xml.Castable>(skillName);
            user.ShowLearnSkill(merchant, skill);
        }

        private void MerchantMenuHandler_LearnSkillAccept(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowLearnSkillAccept(merchant);

        private void MerchantMenuHandler_LearnSkillAgree(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowLearnSkillAgree(merchant);

        private void MerchantMenuHandler_LearnSkillDisagree(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowLearnSkillDisagree(merchant);

        private void MerchantMenuHandler_LearnSpellMenu(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowLearnSpellMenu(merchant);

        private void MerchantMenuHandler_LearnSpell(User user, Merchant merchant, ClientPacket packet)
        {
            var spellName = packet.ReadString8();
            var spell = WorldData.GetByIndex<Xml.Castable>(spellName);
            user.ShowLearnSpell(merchant, spell);
        }
        private void MerchantMenuHandler_LearnSpellAccept(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowLearnSpellAccept(merchant);

        private void MerchantMenuHandler_LearnSpellAgree(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowLearnSpellAgree(merchant);

        private void MerchantMenuHandler_LearnSpellDisagree(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowLearnSpellDisagree(merchant);


        private void MerchantMenuHandler_ForgetSkillMenu(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowForgetSkillMenu(merchant);

        private void MerchantMenuHandler_ForgetSkill(User user, Merchant merchant, ClientPacket packet) { }

        private void MerchantMenuHandler_ForgetSkillAccept(User user, Merchant merchant, ClientPacket packet)
        {
            var slot = packet.ReadByte();          
            user.ShowForgetSkillAccept(merchant, slot);
        }

        private void MerchantMenuHandler_ForgetSpellMenu(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowForgetSpellMenu(merchant);

        private void MerchantMenuHandler_ForgetSpell(User user, Merchant merchant, ClientPacket packet) { }

        private void MerchantMenuHandler_ForgetSpellAccept(User user, Merchant merchant, ClientPacket packet)
        {
            var slot = packet.ReadByte();
            user.ShowForgetSpellAccept(merchant, slot);
        }

        private void MerchantMenuHandler_SendParcelMenu(User user, Merchant merchant, ClientPacket packet) =>
            user.ShowMerchantSendParcel(merchant);

        private void MerchantMenuHandler_SendParcelRecipient(User user, Merchant merchant, ClientPacket packet)
        {
            var item = packet.ReadByte();
            var itemObj = user.Inventory[item];
            user.ShowMerchantSendParcelRecipient(merchant, itemObj);
        }

        private void MerchantMenuHandler_SendParcel(User user, Merchant merchant, ClientPacket packet) { }

        private void MerchantMenuHandler_SendParcelFailure(User user, Merchant merchant, ClientPacket packet) { }

        private void MerchantMenuHandler_SendParcelAccept(User user, Merchant merchant, ClientPacket packet)
        {
            var recipient = packet.ReadString8();
            user.ShowMerchantSendParcelAccept(merchant, recipient);
        }

        private void MerchantMenuHandler_WithdrawItemQuantity(User user, Merchant merchant, ClientPacket packet)
        {
            var item = packet.ReadString8();

            user.ShowWithdrawItemQuantity(merchant, item);
        }

        private void MerchantMenuHandler_WithdrawItemMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowWithdrawItemMenu(merchant);
        }

        private void MerchantMenuHandler_WithdrawItem(User user, Merchant merchant, ClientPacket packet)
        {
            var quantity = Convert.ToUInt32(packet.ReadString8());
            user.WithdrawItemConfirm(merchant, user.PendingWithdrawItem, quantity);
        }

        private void MerchantMenuHandler_WithdrawGoldMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowWithdrawGoldMenu(merchant);
        }

        private void MerchantMenuHandler_DepositGoldMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowDepositGoldMenu(merchant);
        }

        private void MerchantMenuHandler_DepositItemQuantity(User user, Merchant merchant, ClientPacket packet)
        {
            byte slot = packet.ReadByte();

            var quantity = packet.ReadByte();

            if (quantity < 1)
            {
                user.ShowDepositItemQuantity(merchant, slot);
                return;
            }

            var item = user.Inventory[slot];
            if (item == null || !item.Stackable) return;

            user.DepositItemConfirm(merchant, slot, quantity);
        }

        private void MerchantMenuHandler_DepositItemMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowDepositItemMenu(merchant);
        }

        private void MerchantMenuHandler_DepositItem(User user, Merchant merchant, ClientPacket packet)
        {
            
            var quantity = Convert.ToUInt32(packet.ReadString8());
            user.DepositItemConfirm(merchant, user.PendingDepositSlot, (byte)quantity);
        }

        private void MerchantMenuHandler_DepositGoldQuantity(User user, Merchant merchant, ClientPacket packet)
        {
            var amount = Convert.ToUInt32(packet.ReadString8());
            user.DepositGoldConfirm(merchant, amount);
        }

        private void MerchantMenuHandler_WithdrawGoldQuantity(User user, Merchant merchant, ClientPacket packet)
        {
            var amount = Convert.ToUInt32(packet.ReadString8());
            user.WithdrawGoldConfirm(merchant, amount);
        }

        private void MerchantMenuHandler_RepairItemMenu(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowRepairItemMenu(merchant);
        }
        private void MerchantMenuHandler_RepairItem(User user, Merchant merchant, ClientPacket packet)
        {
            var slot = packet.ReadByte();
            user.ShowRepairItem(merchant, slot);
        }

        private void MerchantMenuHandler_RepairItemAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowRepairItemAccept(merchant);
        }

        private void MerchantMenuHandler_RepairAllItems(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowRepairAllItems(merchant);
        }

        private void MerchantMenuHandler_RepairAllItemsAccept(User user, Merchant merchant, ClientPacket packet)
        {
            user.ShowRepairAllItemsAccept(merchant);
        }



        #endregion Merchant Menu ItemObject Handlers

        public void Insert(WorldObject obj)
        {
            if (obj is User)
            {
                AddUser((User)obj);
            }

            obj.Id = worldObjectID;
            obj.World = this;
            obj.SendId();

            if (obj is ItemObject)
            {
                Scripting.Script itemscript;
                if (Game.World.ScriptProcessor.TryGetScript(obj.Name, out itemscript))
                {
                    var clone = itemscript.Clone();
                    itemscript.AssociateScriptWithObject(obj);
                }
            }

            lock (_lock)
            {
                Objects.Add(worldObjectID, obj);
                ++worldObjectID;
            }
        }

        public void Remove(WorldObject obj)
        {
            if (obj is User)
            {
                DeleteUser(obj.Name);
            }
            lock (_lock)
            {
                Objects.Remove(obj.Id);
                if (obj is Creature creature)
                    ActiveStatuses.Remove(creature);
            }
            obj.World = null;
            obj.Id = 0;
        }

        public ItemObject CreateItem(string id, int quantity = 1)
        {
            var xmlitem = WorldData.FindItem(id);
            if (xmlitem.Count == 0) return null;
            var item = new ItemObject(xmlitem.First().Id, this);
            if (quantity > item.MaximumStack)
                quantity = item.MaximumStack;
            item.Count = Math.Max(quantity, 1);
            return item;
        }

        public bool TryGetItemTemplate(string name, Xml.Gender itemGender, out Xml.Item item)
        {
            var itemKey = new Tuple<Xml.Gender, string>(itemGender, name);
            return ItemCatalog.TryGetValue(itemKey, out item);
        }

        public bool TryGetItemTemplate(string name, out Xml.Item item)
        {
            // This is kinda gross
            var neutralKey = new Tuple<Xml.Gender, string>(Xml.Gender.Neutral, name);
            var femaleKey = new Tuple<Xml.Gender, string>(Xml.Gender.Female, name);
            var maleKey = new Tuple<Xml.Gender, string>(Xml.Gender.Male, name);

            return ItemCatalog.TryGetValue(neutralKey, out item) || ItemCatalog.TryGetValue(femaleKey, out item) || ItemCatalog.TryGetValue(maleKey, out item);
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
                catch (InvalidOperationException e)
                {
                    Game.ReportException(e);
                    if (!MessageQueue.IsCompleted)
                        GameLog.Error($"QUEUE CONSUMER: EXCEPTION RAISED: {e}", e);
                    continue;
                }

                if (message != null)
                {                   
                    var clientMessage = (HybrasylClientMessage)message;
                    var handler = PacketHandlers[clientMessage.Packet.Opcode];
                    try
                    {
                        if (ActiveUsers.TryGetValue(clientMessage.ConnectionId, out user))
                        {
                            // Check if the action is prohibited due to statuses or flags
                            MethodBase method = handler.GetMethodInfo();
                            // TODO: improve
                            bool sendRefresh = false;
                            bool ignore = false;
                            string systemMessage = string.Empty;

                            foreach (var prohibited in method.GetCustomAttributes(typeof(Prohibited), true))
                            {
                                var prohibitedCondition = prohibited as Prohibited;
                                if (prohibitedCondition == null) continue;
                                if (prohibitedCondition.Check(user.Condition)) continue;
                                // TODO: fix this to be per-flag/status
                                if (clientMessage.Packet.Opcode == 0x06 && user.Condition.Flags.HasFlag(PlayerFlags.InDialog))
                                    sendRefresh = true;
                                else
                                    systemMessage = "It cannot be done in your current state.";
                                ignore = true;
                            }

                            foreach (var required in method.GetCustomAttributes(typeof(Required), true))
                            {
                                var requiredCondition = required as Required;
                                if (requiredCondition == null) continue;
                                if (requiredCondition.Check(user.Condition)) continue;
                                systemMessage = "You cannot do that now.";
                                ignore = true;
                            }

                            if (systemMessage != string.Empty)
                                user.SendSystemMessage(systemMessage);

                            if (sendRefresh)
                                user.Refresh();

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
                            // Handle board usage
                            if (user.Condition.Flags.HasFlag(PlayerFlags.InDialog) && clientMessage.Packet.Opcode != 0x3b &&
                                clientMessage.Packet.Opcode != 0x45 && clientMessage.Packet.Opcode != 0x75)
                                user.Condition.Flags = user.Condition.Flags & ~PlayerFlags.InDialog;
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
                            GameLog.WarningFormat(
                                "Connection ID {0}: received packet, but seems to be dead connection?",
                                clientMessage.ConnectionId);
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        Game.ReportException(e);
                        GameLog.Error(e, "{Opcode}: Unhandled exception encountered in packet handler!", clientMessage.Packet.Opcode);
                    }
                }
                
            }
        }


        public void ControlQueueConsumer()
        {
            while (!ControlMessageQueue.IsCompleted)
            {
                if (StopToken.IsCancellationRequested)
                    return;
                // Process messages.
                HybrasylMessage message;
                try
                {
                    message = ControlMessageQueue.Take();
                }
                catch (InvalidOperationException e)
                {
                    Game.ReportException(e);
                    GameLog.Error("QUEUE CONSUMER: EXCEPTION RAISED: {exception}", e);
                    continue;
                }

                if (message is HybrasylControlMessage)
                {
                    try
                    {
                        var controlMessage = (HybrasylControlMessage)message;
                        ControlMessageHandlers[controlMessage.Opcode].Invoke(controlMessage);                       
                    }
                    catch (Exception e)
                    {
                        Game.ReportException(e);
                        GameLog.Error("Exception encountered in control message handler: {exception}", e);
                    }
                }
            }
        }

        public void StartQueueConsumer()
        {
            // Start our consumer
            ConsumerThread = new Thread(QueueConsumer);
            if (ConsumerThread.IsAlive) return;
            ConsumerThread.Start();
            GameLog.InfoFormat("Consumer thread: started");

        }

        public void StartControlConsumers()
        {
            ControlConsumerThread = new Thread(ControlQueueConsumer);
            if (ControlConsumerThread.IsAlive) return;
            ControlConsumerThread.Start();
            GameLog.Info("Control consumer thread: started");
        }

        // Mark the message queue as not accepting additions, which will result in thread termination
        public void StopQueueConsumer()
        {
            MessageQueue.CompleteAdding();
            // Remove remaining items
            while (MessageQueue.TryTake(out _)) { }
        }
        public void StopControlConsumers()
        {
            ControlMessageQueue.CompleteAdding();
            // Remove and discard all remaining items
            while (ControlMessageQueue.TryTake(out _)) { }
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
                        GameLog.ErrorFormat("Job class {0} has no Interval defined! Job will not be scheduled.");
                        continue;
                    }

                    aTimer.Interval = ((int)interval) * 1000; // Interval is in ms; interval in Job classes is s

                    GameLog.InfoFormat("Hybrasyl: timer loaded for job {0}: interval {1}", jobClass.Name, aTimer.Interval);
                    aTimer.Enabled = true;
                    aTimer.Start();
                }
                else
                {
                    GameLog.ErrorFormat("Job class {0} has no Execute method! Job will not be scheduled.", jobClass.Name);
                }
            }
        }
    }
}