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

using C3;
using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Properties;
using log4net;
using log4net.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

namespace Hybrasyl
{
    public class World : Server
    {
        private static uint worldObjectID = 0;

        public new static ILog Logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Dictionary<uint, WorldObject> Objects { get; set; }

        public Dictionary<ushort, Map> Maps { get; set; }
        public Dictionary<int, WorldMap> WorldMaps { get; set; }
        public Dictionary<int, item> Items { get; set; }
        public Dictionary<int, SkillTemplate> Skills { get; set; }
        public Dictionary<int, SpellTemplate> Spells { get; set; }
        public Dictionary<int, MonsterTemplate> Monsters { get; set; }
        public Dictionary<int, MerchantTemplate> Merchants { get; set; }
        public Dictionary<int, ReactorTemplate> Reactors { get; set; }
        public Dictionary<string, string> Portraits { get; set; } 
        public Dictionary<string, MethodInfo> Methods { get; set; }
        public Dictionary<string, User> Users { get; set; }
        public Dictionary<Int64, MapPoint> MapPoints { get; set; }
        public Dictionary<string, CompiledMetafile> Metafiles { get; set; }
        public Dictionary<string, nation> Nations { get; set; }

        public List<DialogSequence> GlobalSequences { get; set; }
        public Dictionary<String, DialogSequence> GlobalSequencesCatalog { get; set; }
        private Dictionary<MerchantMenuItem, MerchantMenuHandler> merchantMenuHandlers;

        public Dictionary<Tuple<Sex, String>, item> ItemCatalog { get; set; }
        public Dictionary<String, Map> MapCatalog { get; set; }

        public HybrasylScriptProcessor ScriptProcessor { get; set; }

        public static BlockingCollection<HybrasylMessage> MessageQueue;
        public static ConcurrentDictionary<Int64, User> ActiveUsers { get; private set; }
        public ConcurrentDictionary<string, long> ActiveUsersByName { get; set; }

        private Thread ConsumerThread { get; set; }

        // Timers
        //public static ConcurrentBag<Timer> Timers;

        public Login Login { get; private set; }

        public World(int port)
            : base(port)
        {
            Maps = new Dictionary<ushort, Map>();
            WorldMaps = new Dictionary<int, WorldMap>();
            Items = new Dictionary<int, item>();
            Skills = new Dictionary<int, SkillTemplate>();
            Spells = new Dictionary<int, SpellTemplate>();
            Monsters = new Dictionary<int, MonsterTemplate>();
            Merchants = new Dictionary<int, MerchantTemplate>();
            Methods = new Dictionary<string, MethodInfo>();
            Objects = new Dictionary<uint, WorldObject>();
            Users = new Dictionary<string, User>(StringComparer.CurrentCultureIgnoreCase);
            MapPoints = new Dictionary<Int64, MapPoint>();
            Metafiles = new Dictionary<string, CompiledMetafile>();
            Nations = new Dictionary<string, nation>();
            Portraits = new Dictionary<string, string>();
            GlobalSequences = new List<DialogSequence>();

            GlobalSequencesCatalog = new Dictionary<String, DialogSequence>();
            ItemCatalog = new Dictionary<Tuple<Sex, String>, item>();
            MapCatalog = new Dictionary<String, Map>();

            ScriptProcessor = new HybrasylScriptProcessor(this);
            MessageQueue = new BlockingCollection<HybrasylMessage>(new ConcurrentQueue<HybrasylMessage>());
            ActiveUsers = new ConcurrentDictionary<long, User>();
            ActiveUsersByName = new ConcurrentDictionary<String, long>();

            // Timers = new ConcurrentBag<Timer>();
        }

        public void InitWorld()
        {
            LoadData();
            CompileScripts();
            LoadMetafiles();
            LoadReactors();
            SetPacketHandlers();
            SetControlMessageHandlers();
            SetMerchantMenuHandlers();
            Logger.InfoFormat("Hybrasyl server ready");
        }

        private void LoadReactors()
        {
            using (var ctx = new hybrasylEntities(Constants.ConnectionString))
            {
                foreach (var reactor in ctx.reactors)
                {
                    Map map;
                    if (Maps.TryGetValue((ushort)reactor.map_id, out map))
                    {
                        map.InsertReactor(reactor);
                    }
                }

            }
        }

        internal void RegisterGlobalSequence(DialogSequence sequence)
        {
            sequence.Id = (uint) GlobalSequences.Count();
            GlobalSequences.Add(sequence);
            GlobalSequencesCatalog.Add(sequence.Name, sequence);
        }

        private void AddSingleWarp(int sourceId, int sourceX, int sourceY, int targetId, int targetX, int targetY,
            int maxLev = 99, int minLev = 0, int minAb = 0, bool mobUse = false)
        {
            var warp = new Warp();
            warp.X = (byte) sourceX;
            warp.Y = (byte) sourceY;
            warp.DestinationMap = (ushort) targetId;
            warp.DestinationX = (byte) targetX;
            warp.DestinationY = (byte) targetY;
            warp.MinimumLevel = (byte) (minLev);
            warp.MaximumLevel = (byte) (maxLev);
            warp.MinimumAbility = (byte) (minAb);
            warp.MobsCanUse = (bool) (mobUse);
            Maps[(ushort) sourceId].Warps.Add(warp);

            Logger.DebugFormat("Added warp: {0} {1},{2}", Maps[(ushort) sourceId].Name, warp.X, warp.Y);
        }

        public void AddWarp(warp newWarp)
        {
            int[] sourceRangeX;
            int[] sourceRangeY;
            int[] targetRangeX;
            int[] targetRangeY;

            try
            {
                sourceRangeX = newWarp.source_x.Split('-').Select(s => Convert.ToInt32(s)).ToArray();
                sourceRangeY = newWarp.source_y.Split('-').Select(s => Convert.ToInt32(s)).ToArray();

                targetRangeX = newWarp.target_x.Split('-').Select(s => Convert.ToInt32(s)).ToArray();
                targetRangeY = newWarp.target_y.Split('-').Select(s => Convert.ToInt32(s)).ToArray();
            }
            catch
            {
                Logger.ErrorFormat("Unable to parse warp id {0}", newWarp.id);
                return;
            }

            var sourceCoords = sourceRangeX.Zip(sourceRangeY, (x, y) => new {X = x, Y = y});
            var targetCoords = targetRangeX.Zip(targetRangeY, (x, y) => new {X = x, Y = y});

            foreach (var source in sourceCoords)
            {
                foreach (var target in targetCoords)
                {
                    AddSingleWarp(newWarp.source_id, source.X, source.Y, newWarp.target_id, target.X, target.Y,
                        newWarp.max_lev, newWarp.min_lev,
                        newWarp.min_ab, newWarp.mob_use);
                }
            }

        }

        private void AddSingleWorldWarp(int sourceMapId, int sourceX, int sourceY, int targetWorldmapId, int maxLev = 99,
            int minLev = 0, int minAb = 0)
        {
            var worldwarp = new WorldWarp();
            var worldwarpKey = new Tuple<byte, byte>((byte) sourceX, (byte) sourceY);
            worldwarp.X = (byte) sourceX;
            worldwarp.Y = (byte) sourceY;
            worldwarp.DestinationWorldMap = WorldMaps[targetWorldmapId];
            worldwarp.MinimumLevel = (byte) minLev;
            worldwarp.MaximumLevel = (byte) maxLev;
            worldwarp.MinimumAbility = (byte) minAb;

            if (Maps[(ushort) sourceMapId].WorldWarps.ContainsKey(worldwarpKey))
            {
                Logger.WarnFormat("Duplicate warp detected (ignored): {0} {1},{2} to world map {3}",
                    Maps[(ushort) sourceMapId].Name, worldwarp.X, worldwarp.Y, worldwarp.DestinationWorldMap.Name);
                return;
            }

            Maps[(ushort) sourceMapId].WorldWarps.Add(worldwarpKey, worldwarp);

            Logger.DebugFormat("Added world warp from {0} {1},{2} to world map {3} for level {4}-{5}, min AB: {6}",
                Maps[(ushort) sourceMapId].Name, worldwarp.X, worldwarp.Y, worldwarp.DestinationWorldMap.Name,
                worldwarp.MinimumLevel, worldwarp.MaximumLevel,
                worldwarp.MinimumAbility);
        }

        public void AddWorldWarp(worldwarp target)
        {
            int[] sourceRangeX;
            int[] sourceRangeY;

            try
            {
                sourceRangeX = target.source_x.Split('-').Select(s => Convert.ToInt32(s)).ToArray();
                sourceRangeY = target.source_y.Split('-').Select(s => Convert.ToInt32(s)).ToArray();
            }
            catch
            {
                Logger.ErrorFormat("Unable to parse world warp id {0}", target.id);
                return;
            }

            var sourceCoords = sourceRangeX.Zip(sourceRangeY, (x, y) => new {X = x, Y = y});

            foreach (var sourceCoord in sourceCoords)
            {
                AddSingleWorldWarp(target.source_map_id, sourceCoord.X, sourceCoord.Y, target.target_worldmap_id,
                    target.max_lev, target.min_lev, target.min_ab);
            }
        }

        public bool PlayerExists(string name)
        {
            using (var ctx = new hybrasylEntities(Constants.ConnectionString))
            {
                var count = ctx.players.Where(player => player.name == name).Count();
                Logger.DebugFormat("count is {0}", count);
                return count != 0;

            }
        }

        private void LoadData()
        {
            var random = new Random();
            var elements = Enum.GetValues(typeof(Element)); 
            try
            {
                using (var ctx = new hybrasylEntities(Constants.ConnectionString))
                {
                    var maps = ctx.maps.Include("warps").Include("worldwarps").Include("npcs").Include("spawns").ToList();

                    var worldmaps = ctx.worldmaps.Include("worldmap_points").ToList();
                    var items = ctx.items.ToList();
                    //var doors = ctx.doors.ToList();
                    var signposts = ctx.signposts.ToList();

                    Logger.InfoFormat("Adding {0} worldmaps", ctx.worldmaps.Count());

                    foreach (var wmap in worldmaps)
                    {
                        var worldmap = new WorldMap();
                        worldmap.World = this;
                        worldmap.Id = wmap.id;
                        worldmap.Name = wmap.name;
                        worldmap.ClientMap = wmap.client_map;
                        WorldMaps.Add(worldmap.Id, worldmap);

                        foreach (var mappoint in wmap.worldmap_points)
                        {
                            var point = new MapPoint();
                            point.Parent = worldmap;
                            point.Id = (Int64) mappoint.id;
                            point.Name = (string) mappoint.name;
                            point.DestinationMap = (ushort) mappoint.target_map_id;
                            point.DestinationX = (byte) mappoint.target_x;
                            point.DestinationY = (byte) mappoint.target_y;
                            point.X = (int) mappoint.map_x;
                            point.Y = (int) mappoint.map_y;
                            point.XOffset = point.X%255;
                            point.YOffset = point.Y%255;
                            point.XQuadrant = (point.X - point.XOffset)/255;
                            point.YQuadrant = (point.Y - point.YOffset)/255;
                            worldmap.Points.Add(point);
                            MapPoints.Add(point.Id, point);
                        }
                    }
                    Logger.InfoFormat("Adding {0} maps", ctx.maps.Count());

                    foreach (var map in maps)
                    {
                        var newmap = new Map();
                        newmap.World = this;
                        newmap.Id = (ushort) map.id;
                        newmap.X = (byte) map.size_x;
                        newmap.Y = (byte) map.size_y;
                        newmap.Name = (string) map.name;
                        newmap.Flags = (byte) map.flags;
                        newmap.Music = (byte) (map.music ?? 0);
                        newmap.EntityTree = new QuadTree<VisibleObject>(0, 0, map.size_x, map.size_y);

                        if (newmap.Load())
                        {
                            Maps.Add(newmap.Id, newmap);
                            try
                            {
                                MapCatalog.Add(newmap.Name, newmap);
                            }
                            catch
                            {
                                Logger.WarnFormat("map name {0}, id {1} ignored for map catalog",
                                    newmap.Name, newmap.Id);
                            }

                            foreach (var warp in map.warps)
                            {
                                AddWarp(warp);
                            }

                            foreach (var worldwarp in map.worldwarps)
                            {
                                AddWorldWarp(worldwarp);
                            }

                            foreach (var npc in map.npcs)
                            {
                                newmap.InsertNpc(npc);
                                if (npc.portrait != null)
                                {
                                    Portraits[npc.name] = npc.portrait;
                                }
                            }

                            foreach (var spawn in map.spawns)
                            {
                                AddSpawn(spawn);
                            }
                        }
                    }

                    // We have to handle signposts separately due to a bug in MySQL connector
                    foreach (var signpost in signposts)
                    {
                        Map postmap;
                        if (Maps.TryGetValue((ushort) signpost.map_id, out postmap))
                            postmap.InsertSignpost(signpost);
                        else
                            Logger.ErrorFormat("Signpost {0}: {1},{2}: Map {0} is missing ", signpost.id,
                                signpost.map_x, signpost.map_y, signpost.map_id);
                    }

                    int variantId = Hybrasyl.Constants.VARIANT_ID_START;

                    Logger.InfoFormat("Adding {0} items", items.Count);
                    var variants = ctx.item_variant.ToList();

                    foreach (var item in items)
                    {
                        Logger.DebugFormat("Adding item {0}", item.name);
                        item.Variants = new Dictionary<int, item>();

                        // Handle the case of item having random element, which is an optional bit of support
                        // and not really intended for serious use
                        if (item.element == Element.Random)
                        {
                            item.element = (Element)elements.GetValue(random.Next(elements.Length - 1 ));
                        }

                        if (item.has_consecratable_variants)
                        {
                            foreach (var variant in variants.Where(variant => variant.consecratable_variant == true))
                            {
                                var newitem = variant.ResolveVariant(item);
                                newitem.id = variantId;
                                Items.Add(variantId, newitem);
                                item.Variants[variant.id] = newitem;
                                variantId++;
                            }
                        }

                        if (item.has_elemental_variants)
                        {
                            foreach (var variant in variants.Where(variant => variant.elemental_variant == true))
                            {
                                var newitem = variant.ResolveVariant(item);
                                newitem.id = variantId;
                                Items.Add(variantId, newitem);
                                item.Variants[variant.id] = newitem;
                                variantId++;
                            }
                        }

                        if (item.has_enchantable_variants)
                        {
                            foreach (var variant in variants.Where(variant => variant.enchantable_variant == true))
                            {
                                var newitem = variant.ResolveVariant(item);
                                newitem.id = variantId;
                                Items.Add(variantId, newitem);
                                item.Variants[variant.id] = newitem;
                                variantId++;
                            }
                        }

                        if (item.has_smithable_variants)
                        {
                            foreach (var variant in variants.Where(variant => variant.smithable_variant == true))
                            {
                                var newitem = variant.ResolveVariant(item);
                                newitem.id = variantId;
                                Items.Add(variantId, newitem);
                                item.Variants[variant.id] = newitem;
                                variantId++;
                            }
                        }

                        if (item.has_tailorable_variants)
                        {
                            foreach (var variant in variants.Where(variant => variant.tailorable_variant == true))
                            {
                                var newitem = variant.ResolveVariant(item);
                                newitem.id = variantId;
                                Items.Add(variantId, newitem);
                                item.Variants[variant.id] = newitem;
                                variantId++;
                            }
                        }
                        Items.Add(item.id, item);
                        try
                        {
                            ItemCatalog.Add(new Tuple<Sex, String>(item.sex, item.name), item);
                        }
                        catch
                        {
                            Logger.WarnFormat("probable duplicate item {0} not added to item catalog", item.name);
                        }
                    }
                    Logger.InfoFormat("Added {0} item variants", variantId - Hybrasyl.Constants.VARIANT_ID_START);
                    Logger.InfoFormat("{0} items (including variants) active", Items.Count);

                    // Load national data
                    Nations = ctx.nations.Include("spawn_points").ToDictionary(n => n.name, n => n);
                }
            }
            catch (Exception e)
            {
                Logger.ErrorFormat("Error initializing Hybrasyl data: {0} {1}", e, e.InnerException);
                Logger.ErrorFormat("Beware, server is now likely inconsistent and should be shut down");
            }
        }

        private void AddSpawn(spawn spawn)
        {
            //throw new NotImplementedException();
        }

        private void LoadMetafiles()
        {
            // these might be better suited in LoadData as the database is being read, but only items are in database atm

            #region ItemInfo

            var iteminfo0 = new Metafile("ItemInfo0");
            // TODO: split items into multiple ItemInfo files (DA does ~700 each)
            foreach (var item in Items.Values)
            {
                iteminfo0.Nodes.Add(new MetafileNode(item.name, item.level, (int) item.class_type, item.weight,
                    string.Empty /* shop tab */, string.Empty /* shop description */));
            }
            Metafiles.Add(iteminfo0.Name, iteminfo0.Compile());

            #endregion

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

            #endregion

            #region NPCIllust

            var npcillust = new Metafile("NPCIllust");
            foreach (var kvp in Portraits) // change to merchants that have a portrait rather than all
            {
                npcillust.Nodes.Add(new MetafileNode(kvp.Key, kvp.Value /* portrait filename */));
            }
            Metafiles.Add(npcillust.Name, npcillust.Compile());

            #endregion

            #region NationDesc

            var nationdesc = new Metafile("NationDesc");
            foreach (var nation in Nations.Values)
            {
                Logger.DebugFormat("Adding flag {0} for nation {1}", nation.flag, nation.name);
                nationdesc.Nodes.Add(new MetafileNode("nation_" + nation.flag, nation.name));
            }
            Metafiles.Add(nationdesc.Name, nationdesc.Compile());

            #endregion
        }

        public void CompileScripts()
        {
            // Scan each directory for *.py files
            foreach (var dir in Constants.SCRIPT_DIRECTORIES)
            {
                Logger.InfoFormat("Scanning script directory: {0}", dir);
                var directory = Path.Combine(Constants.DataDirectory, "scripts", dir);
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
        }



        public void SetPacketHandlers()
        {
            PacketHandlers[0x05] = PacketHandler_0x05_RequestMap;
            PacketHandlers[0x06] = PacketHandler_0x06_Walk;
            PacketHandlers[0x07] = PacketHandler_0x07_PickupItem;
            PacketHandlers[0x08] = PacketHandler_0x08_DropItem;
            PacketHandlers[0x0B] = PacketHandler_0x0B_ClientExit;
            PacketHandlers[0x0E] = PacketHandler_0x0E_Talk;
            PacketHandlers[0x10] = PacketHandler_0x10_ClientJoin;
            PacketHandlers[0x11] = PacketHandler_0x11_Turn;
            PacketHandlers[0x13] = PacketHandler_0x13_Assail;
            PacketHandlers[0x18] = PacketHandler_0x18_ShowPlayerList;
            PacketHandlers[0x19] = PacketHandler_0x19_Whisper;
            PacketHandlers[0x1C] = PacketHandler_0x1C_UseItem;
            PacketHandlers[0x1D] = PacketHandler_0x1D_Emote;
            PacketHandlers[0x24] = PacketHandler_0x24_DropGold;
            PacketHandlers[0x29] = PacketHandler_0x29_DropItemOnCreature;
            PacketHandlers[0x2A] = PacketHandler_0x2A_DropGoldOnCreature;
            PacketHandlers[0x2D] = PacketHandler_0x2D_PlayerInfo;
            PacketHandlers[0x30] = PacketHandler_0x30_MoveUIElement;
            PacketHandlers[0x38] = PacketHandler_0x38_Refresh;
            PacketHandlers[0x39] = PacketHandler_0x39_NPCMainMenu;
            PacketHandlers[0x3A] = PacketHandler_0x3A_DialogUse;
            PacketHandlers[0x3B] = PacketHandler_0x3B_AccessMessages;
            PacketHandlers[0x3F] = PacketHandler_0x3F_MapPointClick;
            PacketHandlers[0x43] = PacketHandler_0x43_PointClick;
            PacketHandlers[0x44] = PacketHandler_0x44_EquippedItemClick;
            PacketHandlers[0x45] = PacketHandler_0x45_ByteHeartbeat;
            PacketHandlers[0x47] = PacketHandler_0x47_StatPoint;
            PacketHandlers[0x4a] = PacketHandler_0x4A_Trade;
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
                    new MerchantMenuHandler(MerchantJob.Vendor, MerchantMenuHandler_BuyItemMenu)
                },
                {MerchantMenuItem.BuyItem, new MerchantMenuHandler(MerchantJob.Vendor, MerchantMenuHandler_BuyItem)},
                {
                    MerchantMenuItem.BuyItemQuantity,
                    new MerchantMenuHandler(MerchantJob.Vendor, MerchantMenuHandler_BuyItemWithQuantity)
                },
                {
                    MerchantMenuItem.SellItemMenu,
                    new MerchantMenuHandler(MerchantJob.Vendor, MerchantMenuHandler_SellItemMenu)
                },
                {MerchantMenuItem.SellItem, new MerchantMenuHandler(MerchantJob.Vendor, MerchantMenuHandler_SellItem)},
                {
                    MerchantMenuItem.SellItemQuantity,
                    new MerchantMenuHandler(MerchantJob.Vendor, MerchantMenuHandler_SellItemWithQuantity)
                },
                {
                    MerchantMenuItem.SellItemAccept,
                    new MerchantMenuHandler(MerchantJob.Vendor, MerchantMenuHandler_SellItemConfirmation)
                }
            };
        }

        #endregion

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
            TcpListener.Stop();
            Logger.Warn("Shutdown complete");
        }


        #region Control Message Handlers

        private void ControlMessage_CleanupUser(HybrasylControlMessage message)
        {
            // clean up after a broken connection
            var connectionId = (long) message.Arguments[0];
            User user;
            if (ActiveUsers.TryRemove(connectionId, out user))
            {
                Logger.InfoFormat("cid {0}: closed, player {1} removed", connectionId, user.Name);
                if (user.ActiveExchange != null)
                    user.ActiveExchange.CancelExchange(user);
                ((IDictionary) ActiveUsersByName).Remove(user.Name);
                user.Save();
                user.UpdateLogoffTime();
                user.Map.Remove(user);
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
            var connectionId = (long) message.Arguments[0];
            if (ActiveUsers.TryGetValue(connectionId, out user))
            {
                uint hpRegen = 0;
                uint mpRegen = 0;
                double fixedRegenBuff = Math.Min(user.Regen*0.0015, 0.15);
                if (user.Hp != user.MaximumHp)
                {
                    hpRegen = (uint) Math.Min(user.MaximumHp*(0.1*Math.Max(user.Con, (user.Con - user.Level))*0.01),
                        user.MaximumHp*0.20);
                    hpRegen = hpRegen + (uint) (fixedRegenBuff*user.MaximumHp);

                }
                if (user.Mp != user.MaximumMp)
                {
                    mpRegen = (uint) Math.Min(user.MaximumMp*(0.1*Math.Max(user.Int, (user.Int - user.Level))*0.01),
                        user.MaximumMp*0.20);
                    mpRegen = mpRegen + (uint) (fixedRegenBuff*user.MaximumMp);

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
            var connectionId = (long) message.Arguments[0];
            if (ActiveUsers.TryGetValue(connectionId, out user))
            {
                Logger.DebugFormat("Saving user {0}", user.Name);
                user.SaveDataToEntityFramework();
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
            var userName = (string) message.Arguments[0];
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
        #endregion


        private void PacketHandler_0x05_RequestMap(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            int index = 0;

            for (ushort row = 0; row < user.Map.Y; ++row)
            {
                var x3C = new ServerPacket(0x3C);
                x3C.WriteUInt16(row);
                for (int col = 0; col < user.Map.X*6; col += 2)
                {
                    x3C.WriteByte(user.Map.RawData[index + 1]);
                    x3C.WriteByte(user.Map.RawData[index]);
                    index += 2;
                }
                user.Enqueue(x3C);
            }
        }

        private void PacketHandler_0x06_Walk(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var direction = packet.ReadByte();
            if (direction > 3) return;
            user.Walk((Direction) direction);
        }

        private void PacketHandler_0x07_PickupItem(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
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
            var pickupObject = user.Map.EntityTree.GetObjects(tile).FindLast(i => i is Gold || i is Item);

            // If the add is successful, remove the item from the map quadtree
            if (pickupObject is Gold)
            {
                var gold = (Gold) pickupObject;
                if (user.AddGold(gold))
                {
                    Logger.DebugFormat("Removing {0}, qty {1} from {2}@{3},{4}",
                        gold.Name, gold.Amount, user.Map.Name, x, y);
                    user.Map.RemoveGold(gold);
                }
            }
            else if (pickupObject is Item)
            {
                var item = (Item) pickupObject;
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

        private void PacketHandler_0x08_DropItem(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
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

            Item toDrop = user.Inventory[slot];

            if (toDrop.Stackable && count < toDrop.Count)
            {
                toDrop.Count -= count;
                user.SendItemUpdate(toDrop, slot);

                toDrop = new Item(toDrop);
                toDrop.Count = count;
                Insert(toDrop);
            }
            else
            {
                user.RemoveItem(slot);
            }

            // Are we dropping an item onto a reactor?
            Reactor reactor;
            var coordinates = new Tuple<byte, byte>((byte) x, (byte) y);
            if (user.Map.Reactors.TryGetValue(coordinates, out reactor))
            {
                reactor.OnDrop(user, toDrop);
            }
            else
                user.Map.AddItem(x, y, toDrop);
        }

        private void PacketHandler_0x11_Turn(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var direction = packet.ReadByte();
            if (direction > 3) return;
            user.Turn((Direction) direction);
        }

        //Default Spacebar Assail Handler
        private void PacketHandler_0x13_Assail(object obj, ClientPacket packet)
        {
            var user = (User)obj;
            if (user == null)
                return;

            if ((DateTime.Now - user.LastAssail).TotalMilliseconds > 750)
            {
                //send to self
                user.SendMotion(user.Id, 0x01, 0x14);
                user.SendSound(0x0001);

                //send to nearby players
                foreach (var mapobj in user.Map.EntityTree.GetObjects(user.Map.GetViewport(user.X, user.Y)))
                {
                    if (mapobj is User)
                    {
                        if (mapobj == null)
                            continue;
                        if (user.Id == mapobj.Id)
                            continue;

                        var aisling = FindUser(mapobj.Name);

                        if (aisling != null)
                        {
                            aisling.SendSound(0x0001);
                            aisling.SendMotion(user.Id, 0x01, 0x14);
                        }
                    }
                }
                user.Attack();
                user.LastAssail = DateTime.Now;
            }
        }

        private void ProcessSlashCommands(Client client, ClientPacket packet)
        {

        }

        private void PacketHandler_0x0E_Talk(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
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
                            Logger.InfoFormat("Search term was {0}",searchString);
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
                            user.Class = (Hybrasyl.Enums.Class) Hybrasyl.Constants.CLASSES[className];
                            user.SendMessage(String.Format("Class set to {0}", className.ToLower()), 0x1);
                        }
                        else
                        {
                            user.SendMessage("I know nothing about that class. Try again.", 0x1);
                        }
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
                            user.Level = newLevel;
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
                        user.Guild = guild;
                        user.SendMessage(String.Format("Guild changed to {0}", guild), 0x1);
                    }
                        break;
                    case "/guildrank":
                    {
                        var guildrank = string.Join(" ", args, 1, args.Length - 1);
                        user.GuildRank = guildrank;
                        user.SendMessage(String.Format("Guild rank changed to {0}", guildrank), 0x1);
                    }
                        break;
                    case "/title":
                    {
                        var title = string.Join(" ", args, 1, args.Length - 1);
                        user.Title = title;
                        user.SendMessage(String.Format("Title changed to {0}", title), 0x1);
                    }
                        break;
                    case "/debug":
                    {
                        if (!user.IsPrivileged)
                            return;
                        user.SendMessage("Debugging enabled", 3);
                        ((log4net.Repository.Hierarchy.Hierarchy) LogManager.GetRepository()).Root.Level = Level.Debug;
                        ((log4net.Repository.Hierarchy.Hierarchy) LogManager.GetRepository()).RaiseConfigurationChanged(
                            EventArgs.Empty);
                        Logger.InfoFormat("Debugging enabled by admin command");

                    }
                        break;
                    case "/nodebug":
                    {
                        if (!user.IsPrivileged)
                            return;
                        user.SendMessage("Debugging disabled", 3);
                        ((log4net.Repository.Hierarchy.Hierarchy) LogManager.GetRepository()).Root.Level = Level.Info;
                        ((log4net.Repository.Hierarchy.Hierarchy) LogManager.GetRepository()).RaiseConfigurationChanged(
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
                                    ((IPEndPoint) pair.Value.Socket.RemoteEndPoint).Address.ToString(),
                                    ((IPEndPoint) pair.Value.Socket.RemoteEndPoint).Port, serverType);
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
                            if (template.Value.name.Equals(itemName, StringComparison.CurrentCultureIgnoreCase))
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
                #endregion world's biggest switch statement
                }
            }
            else
            {
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
        
        private void CheckCommandPrivileges()
        {
            throw new NotImplementedException();
        }

        private void PacketHandler_0x0B_ClientExit(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
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

                user.Save();
                user.UpdateLogoffTime();
                user.Map.Remove(user);
                Remove(user);
                DeleteUser(user.Name);
                user.SendRedirectAndLogoff(this, Game.Login, user.Name);

                if (ActiveUsersByName.TryRemove(user.Name, out connectionId))
                {
                    ((IDictionary) ActiveUsers).Remove(connectionId);
                }
                Logger.InfoFormat("cid {0}: {1} leaving world", connectionId, user.Name);
            }
        }

        private void PacketHandler_0x10_ClientJoin(Object obj, ClientPacket packet)
        {
            var connectionId = (long) obj;

            var seed = packet.ReadByte();
            var keyLength = packet.ReadByte();
            var key = packet.Read(keyLength);
            var name = packet.ReadString8();
            var id = packet.ReadUInt32();

            var redirect = ExpectedConnections[id];

            if (redirect.Matches(name, key, seed))
            {
                ((IDictionary) ExpectedConnections).Remove(id);

                if (PlayerExists(name))
                {
                    var user = new User(this, connectionId, name);
                    user.SetEncryptionParameters(key, seed, name);
                    user.LoadDataFromEntityFramework(true);
                    user.UpdateLoginTime();
                    user.UpdateAttributes(StatUpdateFlags.Full);
                    Logger.DebugFormat("Elapsed time since login: {0}", user.SinceLastLogin);
                    if (user.Citizenship.spawn_points.Count != 0 &&
                        user.SinceLastLogin > Hybrasyl.Constants.NATION_SPAWN_TIMEOUT)
                    {
                        Insert(user);
                        var spawnpoint = user.Citizenship.spawn_points.First();
                        user.Teleport((ushort) spawnpoint.map_id, (byte) spawnpoint.map_x, (byte) spawnpoint.map_y);

                    }
                    else if (user.MapId != null && Maps.ContainsKey(user.MapId))
                    {
                        Insert(user);
                        user.Teleport(user.MapId, (byte) user.MapX, (byte) user.MapY);
                    }
                    else
                    {
                        // Handle any weird cases where a map someone exited on was deleted, etc
                        // This "default" of Mileth should be set somewhere else
                        Insert(user);
                        user.Teleport((ushort) 500, (byte) 50, (byte) 50);
                    }
                    Logger.DebugFormat("Adding {0} to hash", user.Name);
                    AddUser(user);
                    ActiveUsers[connectionId] = user;
                    ActiveUsersByName[user.Name] = connectionId;
                    Logger.InfoFormat("cid {0}: {1} entering world", connectionId, user.Name);
                }
            }
        }

        private void PacketHandler_0x18_ShowPlayerList(Object obj, ClientPacket packet)
        {
            var me = (User) obj;

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
                
                if (!string.IsNullOrEmpty(me.Guild) && user.Guild == me.Guild) listPacket.WriteByte(84);
                else if (levelDifference <= 5) listPacket.WriteByte(151);
                else listPacket.WriteByte(255);

                listPacket.WriteByte((byte)user.GroupStatus);
                listPacket.WriteString8(user.Title);
                listPacket.WriteBoolean(user.IsMaster);
                listPacket.WriteString8(user.Name);
            }
            me.Enqueue(listPacket);
        }

        private void PacketHandler_0x19_Whisper(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var size = packet.ReadByte();
            var target = Encoding.GetEncoding(949).GetString(packet.Read(size));
            var msgsize = packet.ReadByte();
            var message = Encoding.GetEncoding(949).GetString(packet.Read(msgsize));

            user.SendWhisper(target, message);

        }

        private void PacketHandler_0x1C_UseItem(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var slot = packet.ReadByte();

            Logger.DebugFormat("Updating slot {0}", slot);

            if (slot == 0 || slot > Constants.MAXIMUM_INVENTORY) return;

            var item = user.Inventory[slot];
            
            if (item == null) return;

            switch (item.ItemType)
            {
                case ItemType.CanUse:
                    item.Invoke(user);
                    break;
                case ItemType.CannotUse:
                    user.SendMessage("You can't use that.", 3);
                    break;
                case ItemType.Equipment:
                {

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

        private void PacketHandler_0x1D_Emote(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var emote = packet.ReadByte();
            if (emote <= 35)
            {
                emote += 9;
                user.Motion(emote, 120);
            }
        }

        private void PacketHandler_0x24_DropGold(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
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
            var user = (User) obj;
            user.SendProfile();
        }

        private void PacketHandler_0x2A_DropGoldOnCreature(Object obj, ClientPacket packet)
        {
            var goldAmount = packet.ReadUInt32();
            var targetId = packet.ReadUInt32();

            var user = (User) obj;
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
                    var playerTarget = (User) target;

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
                    var creature = (Creature) target;
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

        private void PacketHandler_0x29_DropItemOnCreature(Object obj, ClientPacket packet)
        {
            var itemSlot = packet.ReadByte();
            var targetId = packet.ReadUInt32();
            var quantity = packet.ReadByte();
            var user = (User) obj;
            
            // If the object is a creature or an NPC, simply give them the item, otherwise,
            // initiate an exchange

            WorldObject target;
            if (!user.World.Objects.TryGetValue(targetId, out target))
                return;

            if (user.Map.Objects.Contains((VisibleObject)target))
            {
                if (target is User)
                {
                    
                    var playerTarget = (User) target;

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
                    var creature = (Creature) target;
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

        private void PacketHandler_0x30_MoveUIElement(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var window = packet.ReadByte();
            var oldSlot = packet.ReadByte();
            var newSlot = packet.ReadByte();

            var inventory = user.Inventory;

            // For right now we ignore the other cases (moving a skill or spell)
            if (window > 0)
                return;

            Logger.DebugFormat("Moving {0} to {1}", oldSlot, newSlot);

            // Is the slot invalid? Does at least one of the slots contain an item?
            if (oldSlot == 0 || oldSlot > Constants.MAXIMUM_INVENTORY ||
                newSlot == 0 || newSlot > Constants.MAXIMUM_INVENTORY ||
                (inventory[oldSlot] == null && inventory[newSlot] == null)) return;

            user.SwapItem(oldSlot, newSlot);
        }

        private void PacketHandler_0x3B_AccessMessages(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var messagePacket = new ServerPacket(0x31);
            messagePacket.WriteByte(0x01);
            messagePacket.WriteUInt16(0x00);
            messagePacket.WriteByte(0x00);
            user.Enqueue(messagePacket);
        }

        private void PacketHandler_0x3F_MapPointClick(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
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
            var user = (User) obj;
            if (user.CheckSquelch(0x38, null))
            {
                Logger.InfoFormat("{0}: squelched (refresh)", user.Name);
                return;
            }
            user.Refresh();
        }

        private void PacketHandler_0x39_NPCMainMenu(Object obj, ClientPacket packet)
        {
            var user = (User) obj;

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

                    var menuItem = (MerchantMenuItem) pursuitId;
                    var merchant = (Merchant) wobj;
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


        private void PacketHandler_0x3A_DialogUse(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
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

        private void PacketHandler_0x43_PointClick(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var clickType = packet.ReadByte();
            Rectangle commonViewport = user.GetViewport();

            // User has clicked an X,Y point
            if (clickType == 3)
            {
                var x = (byte) packet.ReadUInt16();
                var y = (byte) packet.ReadUInt16();
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
                        methodInfo.Invoke(clickTarget, new[] {user});
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

        private void PacketHandler_0x44_EquippedItemClick(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
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
            var user = (User) obj;
            // Client sends 0x45 response in the reverse order of what the server sends...
            var byteB = packet.ReadByte();
            var byteA = packet.ReadByte();

            if (!user.IsHeartbeatValid(byteA, byteB))
            {
                Logger.InfoFormat("{0}: byte heartbeat not valid, disconnecting", user.Name);
                user.Logoff();
            }
            else
            {
                Logger.DebugFormat("{0}: byte heartbeat valid", user.Name);
            }
        }

        //Added System Messages.
        private void PacketHandler_0x47_StatPoint(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            if (user.LevelPoints > 0)
            {
                switch (packet.ReadByte())
                {
                    case 0x01:
                        user.BaseStr++;
                        user.SendSystemMessage("Your muscles harden.");
                        break;
                    case 0x04:
                        user.BaseInt++;
                        user.SendSystemMessage("You understand more.");
                        break;
                    case 0x08:
                        user.BaseWis++;
                        user.SendSystemMessage("You feel more in touch.");
                        break;
                    case 0x10:
                        user.BaseCon++;
                        user.SendSystemMessage("Energy flows into you.");
                        break;
                    case 0x02:
                        user.BaseDex++;
                        user.SendSystemMessage("You feel more nimble.");
                        break;
                    default:
                        return;
                }

                user.LevelPoints--;
                user.UpdateAttributes(StatUpdateFlags.Primary);
            }
            else
            {
                user.SendSystemMessage("You can't do that.");
            }
            

        }

        private void PacketHandler_0x4A_Trade(object obj, ClientPacket packet)
        {
            var user = (User) obj;
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

        private void PacketHandler_0x4F_ProfileTextPortrait(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var totalLength = packet.ReadUInt16();
            var portraitLength = packet.ReadUInt16();
            var portraitData = packet.Read(portraitLength);
            var profileText = packet.ReadString16();

            user.PortraitData = portraitData;
            user.ProfileText = profileText;
        }

        private void PacketHandler_0x75_TickHeartbeat(object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var serverTick = packet.ReadInt32();
            var clientTick = packet.ReadInt32(); // Dunno what to do with this right now, so we just store it

            if (!user.IsHeartbeatValid(serverTick, clientTick))
            {
                Logger.InfoFormat("{0}: tick heartbeat not valid, disconnecting", user.Name);
                user.Logoff();
            }
            else
            {
                Logger.DebugFormat("{0}: tick heartbeat valid", user.Name);
            }
        }

        private void PacketHandler_0x79_Status(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var status = packet.ReadByte();
            if (status <= 7)
            {
                user.GroupStatus = (UserStatus) status;
            }
        }

        private void PacketHandler_0x7B_RequestMetafile(Object obj, ClientPacket packet)
        {
            var user = (User) obj;
            var all = packet.ReadBoolean();

            if (all)
            {
                var x6F = new ServerPacket(0x6F);
                x6F.WriteBoolean(all);
                x6F.WriteUInt16((ushort) Metafiles.Count);
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
                    x6F.WriteUInt16((ushort) file.Data.Length);
                    x6F.Write(file.Data);
                    user.Enqueue(x6F);
                }
            }
        }

        #region Merchant Menu Item Handlers

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

            if (user.Gold < template.value)
            {
                user.ShowMerchantGoBack(merchant, "You do not have enough gold.", MerchantMenuItem.BuyItemMenu);
                return;
            }

            if (user.CurrentWeight + template.weight > user.MaximumWeight)
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

            user.RemoveGold((uint) template.value);

            var item = CreateItem(template.id);
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

            uint cost = (uint) (template.value*quantity);

            if (user.Gold < cost)
            {
                user.ShowMerchantGoBack(merchant, "You do not have enough gold.", MerchantMenuItem.BuyItemMenu);
                return;
            }

            if (quantity > template.max_stack)
            {
                user.ShowMerchantGoBack(merchant, string.Format("You cannot hold that many {0}.", name),
                    MerchantMenuItem.BuyItemMenu);
                return;
            }

            if (user.Inventory.Contains(name))
            {
                byte slot = user.Inventory.SlotOf(name);
                if (user.Inventory[slot].Count + quantity > template.max_stack)
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

                var item = CreateItem(template.id, quantity);
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

            uint profit = (uint) (Math.Round(item.Value*0.50)*quantity);

            if (item.Stackable && quantity < item.Count)
                user.DecreaseItem(slot, quantity);
            else user.RemoveItem(slot);

            user.AddGold(profit);

            merchant.DisplayPursuits(user);
        }

        #endregion

        public void Insert(WorldObject obj)
        {
            if (obj is User)
            {
                AddUser((User) obj);
            }

            ++worldObjectID;
            obj.Id = worldObjectID;
            obj.World = this;
            obj.SendId();

            if (obj is Item)
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

        public Item CreateItem(int id, int quantity = 1)
        {
            if (Items.ContainsKey(id))
            {
                var item = new Item(id, this);
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

        public bool TryGetItemTemplate(string name, Sex itemSex, out item item)
        {
            var itemKey = new Tuple<Sex, String>(itemSex, name);
            return ItemCatalog.TryGetValue(itemKey, out item);
        }

        public bool TryGetItemTemplate(string name, out item item)
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
                        var clientMessage = (HybrasylClientMessage) message;
                        var handler = PacketHandlers[clientMessage.Packet.Opcode];
                        try
                        {                            
                            if (ActiveUsers.TryGetValue(clientMessage.ConnectionId, out user))
                            {
                                // If we are in an exchange, we should only receive exchange packets and the
                                // occasional heartbeat. If we receive anything else, just kill the exchange.
                                if (user.ActiveExchange != null && (clientMessage.Packet.Opcode != 0x4a &&
                                    clientMessage.Packet.Opcode != 0x45 && clientMessage.Packet.Opcode != 0x75)) 
                                    user.ActiveExchange.CancelExchange(user);

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

                        var controlMessage = (HybrasylControlMessage) message;
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
                        (ElapsedEventHandler) Delegate.CreateDelegate(typeof (ElapsedEventHandler), executeMethod);
                    // Interval is set to whatever is in the class
                    var interval = jobClass.GetField("Interval").GetValue(null);

                    if (interval == null)
                    {
                        Logger.ErrorFormat("Job class {0} has no Interval defined! Job will not be scheduled.");
                        continue;
                    }

                    aTimer.Interval = ((int) interval)*1000; // Interval is in ms; interval in Job classes is s

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
