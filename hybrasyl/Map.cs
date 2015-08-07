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
using Hybrasyl.Objects;
using Hybrasyl.Properties;
using log4net;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using log4net.Appender;

namespace Hybrasyl.Properties
{
    public partial class Door
    {
        public bool Open { get; set; }
    }
}

namespace Hybrasyl
{


    public class MapPoint
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Int64 Id { get; set; }
        public string Pointname { get; set; }
        public WorldMap Parent { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Name { get; set; }
        public ushort DestinationMap { get; set; }
        public byte DestinationX { get; set; }
        public byte DestinationY { get; set; }

        public int XOffset { get; set; }
        public int YOffset { get; set; }
        public int XQuadrant { get; set; }
        public int YQuadrant { get; set; }

        public MapPoint()
        {
            return;
        }

        public byte[] GetBytes()
        {
            var buffer = Encoding.GetEncoding(949).GetBytes(Name);
            Logger.DebugFormat("buffer is {0} and Name is {1}", BitConverter.ToString(buffer), Name);

            // X quadrant, offset, Y quadrant, offset, length of the name, the name, plus a 64-bit(?!) ID
            List<Byte> bytes = new List<Byte>();

            Logger.DebugFormat("{0}, {1}, {2}, {3}, {4}, mappoint ID is {5}", XQuadrant, XOffset, YQuadrant,
                YOffset, Name.Length, Id);

            bytes.Add((byte)XQuadrant);
            bytes.Add((byte)XOffset);
            bytes.Add((byte)YQuadrant);
            bytes.Add((byte)YOffset);
            bytes.Add((byte)Name.Length);
            bytes.AddRange(buffer);
            bytes.AddRange(BitConverter.GetBytes(Id));

            return bytes.ToArray();

        }


    }

    public class WorldMap
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public int Id { get; set; }
        public string Name { get; set; }
        public string ClientMap { get; set; }
        public List<MapPoint> Points { get; set; }
        public World World { get; set; }

        public WorldMap()
        {
            Points = new List<MapPoint>();
        }

        public byte[] GetBytes()
        {
            // Returns the representation of the worldmap as an array of bytes, 
            // suitable to passing to a map packet.

            var buffer = Encoding.GetEncoding(949).GetBytes(ClientMap);
            List<Byte> bytes = new List<Byte>();

            bytes.Add((byte)ClientMap.Length);
            bytes.AddRange(buffer);
            bytes.Add((byte)Points.Count);
            bytes.Add(0x00);

            foreach (var mappoint in Points)
            {
                bytes.AddRange(mappoint.GetBytes());
            }

            Logger.DebugFormat("I am sending the following map packet:");
            Logger.DebugFormat("{0}", BitConverter.ToString(bytes.ToArray()));

            return bytes.ToArray();
        }
    }

    public class Map
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        public ushort Id { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
        public string Name { get; set; }
        public byte Flags { get; set; }
        public byte Music { get; set; }
        public World World { get; set; }
        public byte[] RawData { get; set; }
        public ushort Checksum { get; set; }
        public bool[,] IsWall { get; set; }

        public Dictionary<Tuple<byte, byte>, Warp> Warps { get; set; }
        public string Message { get; set; }

        public QuadTree<VisibleObject> EntityTree { get; set; }

        public HashSet<VisibleObject> Objects { get; private set; }
        public Dictionary<string, User> Users { get; private set; }

        public Dictionary<Tuple<byte, byte>, Objects.Door> Doors { get; set; }
        public Dictionary<Tuple<byte, byte>, Signpost> Signposts { get; set; }
        public Dictionary<Tuple<byte, byte>, Reactor> Reactors { get; set; }

        /// <summary>
        /// Create a new Hybrasyl map from an XMLElement representing a map 
        /// element in an XML file.
        /// </summary>
        /// <param name="mapElement"></param>
        public Map(XmlElement mapElement, World theWorld)
        {
            Init();
            World = theWorld;

            Id = Convert.ToUInt16(mapElement.Attributes["id"].Value);
            X = Convert.ToByte(mapElement.Attributes["x"].Value);
            Y = Convert.ToByte(mapElement.Attributes["y"].Value);
            Name = mapElement["name"].InnerText;
            EntityTree = new QuadTree<VisibleObject>(0,0,X,Y);
            Music = Convert.ToByte(mapElement.Attributes["music"].Value);

            foreach (XmlElement warpElement in mapElement.GetElementsByTagName("warp"))
            {
                var warpX = Convert.ToByte(warpElement.Attributes["x"].Value);
                var warpY = Convert.ToByte(warpElement.Attributes["y"].Value);
                var targetElement = warpElement.GetElementsByTagName("maptarget").Item(0) ??
                                    warpElement.GetElementsByTagName("worldmaptarget").Item(0);
                var destinationMapName = targetElement.InnerText;

                var warp = new Warp(this, destinationMapName, warpX, warpY);

                if (targetElement.Name == "maptarget")
                {
                    warp.WarpType = WarpType.Map;
                    warp.DestinationX = Convert.ToByte(targetElement.Attributes["x"].Value);
                    warp.DestinationY = Convert.ToByte(targetElement.Attributes["y"].Value);
                }
                else if (targetElement.Name == "worldmaptarget")
                {
                    warp.WarpType = WarpType.WorldMap;
                }

                var restrictions = targetElement["restrictions"];
                if (restrictions != null)
                {
                    var level = restrictions["level"];
                    var ab = restrictions["ab"];
                    var noMobUse = restrictions["noMobUse"];
                    if (level != null)
                    {
                        warp.MinimumLevel = Convert.ToByte(level.Attributes["min"].Value);
                        warp.MaximumLevel = Convert.ToByte(level.Attributes["min"].Value);
                    }
                    if (ab != null)
                    {
                        warp.MinimumAbility = Convert.ToByte(ab.Attributes["min"].Value);
                        warp.MaximumAbility = Convert.ToByte(ab.Attributes["min"].Value);
                    }
                    if (noMobUse != null)
                        warp.MobUse = false;
                }
                Warps[new Tuple<byte, byte>(warpX, warpY)] = warp;
            }

            foreach (XmlElement npcElement in mapElement.GetElementsByTagName("npc"))
            {
                var merchant = new Merchant();
                merchant.X = Convert.ToByte(npcElement.Attributes["x"].Value);
                merchant.Y = Convert.ToByte(npcElement.Attributes["y"].Value);
                merchant.Name = npcElement["name"].InnerText;
                merchant.Sprite = Convert.ToUInt16(npcElement["appearance"].Attributes["sprite"].Value);
                merchant.Direction = (Hybrasyl.Enums.Direction)Convert.ToByte(npcElement["appearance"].Attributes["direction"].Value);
                if (npcElement.Attributes["portrait"] != null)
                    merchant.Portrait = npcElement.Attributes["portrait"].Value;
                if (npcElement["jobs"] != null)
                {
                    var jobList = String.Join(",", npcElement["jobs"].InnerText.Trim().Split(' '));
                    MerchantJob jobs;
                    if (Enum.TryParse(jobList, out jobs))
                        merchant.Jobs = jobs;
                }

                InsertNpc(merchant);
            }

            foreach (XmlElement reactorElement in mapElement.GetElementsByTagName("reactor"))
            {
                // TODO: implement me                
            }

            foreach (XmlElement signpostElement in mapElement.GetElementsByTagName("signpost"))
            {
                var postX = Convert.ToByte(signpostElement.Attributes["x"].Value);
                var postY = Convert.ToByte(signpostElement.Attributes["y"].Value);
                var message = signpostElement["message"].InnerText;
                var signPost = new Signpost(postX, postY, message);
                InsertSignpost(signPost);
            }

            foreach (XmlElement spawnElement in mapElement.GetElementsByTagName("spawns"))
            {
                // TODO: implement me
            }

            var npcs = mapElement.GetElementsByTagName("npcs");
            Logger.InfoFormat("Added {0}: {1}x{2} {3}", Id, X, Y, Name);
            Load();
        }

        public Map()
        {
            Init();
        }

        public void Init()
        {
            RawData = new byte[0];
            Objects = new HashSet<VisibleObject>();
            Users = new Dictionary<string, User>();
            Warps = new Dictionary<Tuple<byte, byte>, Warp>();
            EntityTree = new QuadTree<VisibleObject>(1, 1, X, Y);
            Doors = new Dictionary<Tuple<byte, byte>, Objects.Door>();
            Signposts = new Dictionary<Tuple<byte, byte>, Signpost>();
            Reactors = new Dictionary<Tuple<byte, byte>, Reactor>();
        }

        public List<VisibleObject> GetTileContents(int x, int y)
        {
            return EntityTree.GetObjects(new Rectangle(x, y, 1, 1));
        }

        public void InsertNpc(Merchant toInsert)
        {
            World.Insert(toInsert);
            Insert(toInsert, toInsert.X, toInsert.Y);
            toInsert.OnSpawn();
        }
        
        public void InsertReactor(/*reactor toinsert*/)
        {
            /*
            var reactor = new Reactor(toinsert);
            World.Insert(reactor);
            Insert(reactor, reactor.X, reactor.Y);
            reactor.OnSpawn();
             */
        }

        public void InsertSignpost(Signpost post)
        {
            World.Insert(post);
            Insert(post, post.X, post.Y);
            Signposts[new Tuple<byte, byte>(post.X, post.Y)] = post;
            Logger.InfoFormat("Inserted signpost {0}@{1},{2}", post.Map.Name, post.X, post.Y);
        }

        private void InsertDoor(byte x, byte y, bool open, bool isLeftRight, bool triggerCollision = true)
        {
            var door = new Objects.Door(x, y, open, isLeftRight, triggerCollision);
            World.Insert(door);
            Insert(door, door.X, door.Y);
            Doors[new Tuple<byte, byte>(door.X, door.Y)] = door;
        }

        public bool Load()
        {
            IsWall = new bool[X, Y];
            var filename = Path.Combine(Constants.DataDirectory, string.Format("maps\\lod{0}.map", Id));

            if (File.Exists(filename))
            {
                RawData = File.ReadAllBytes(filename);
                Checksum = Crc16.Calculate(RawData);

                int index = 0;
                for (int y = 0; y < Y; ++y)
                {
                    for (int x = 0; x < X; ++x)
                    {
                        var bg = RawData[index++] | RawData[index++] << 8;
                        var lfg = RawData[index++] | RawData[index++] << 8;
                        var rfg = RawData[index++] | RawData[index++] << 8;

                        if (lfg != 0 && (Game.Collisions[lfg - 1] & 0x0F) == 0x0F)
                        {
                            IsWall[x, y] = true;
                        }

                        if (rfg != 0 && (Game.Collisions[rfg - 1] & 0x0F) == 0x0F)
                        {
                            IsWall[x, y] = true;
                        }

                        ushort lfgu = (ushort)lfg;
                        ushort rfgu = (ushort)rfg;

                        if (Game.DoorSprites.ContainsKey(lfgu))
                        {
                            // This is a left-right door
                            Logger.DebugFormat("Inserting LR door at {0}@{1},{2}: Collision: {3}",
                                Name, x, y, IsWall[x, y]);

                            InsertDoor((byte)x, (byte)y, IsWall[x, y], true,
                            Game.IsDoorCollision(lfgu));
                        }
                        else if (Game.DoorSprites.ContainsKey(rfgu))
                        {
                            Logger.DebugFormat("Inserting UD door at {0}@{1},{2}: Collision: {3}",
                                Name, x, y, IsWall[x, y]);
                            // THis is an up-down door 
                            InsertDoor((byte)x, (byte)y, IsWall[x, y], false,
                                Game.IsDoorCollision(rfgu));
                        }

                    }
                }

                return true;
            }

            return false;
        }


        public void Insert(VisibleObject obj, byte x, byte y, bool updateClient = true)
        {
            if (Objects.Add(obj))
            {
                obj.Map = this;
                obj.X = x;
                obj.Y = y;

                EntityTree.Add(obj);

                var user = obj as User;
                if (user != null)
                {
                    if (updateClient)
                    {
                        obj.SendMapInfo();
                        obj.SendLocation();
                    }
                    Users.Add(user.Name, user);
                }

                var value = obj as Reactor;
                if (value != null)
                {
                    Reactors.Add(new Tuple<byte, byte>((byte)x,(byte)y), value);
                }

                var affectedObjects = EntityTree.GetObjects(obj.GetViewport());

                foreach (var target in affectedObjects)
                {
                    target.AoiEntry(obj);
                    obj.AoiEntry(target);
                }

            }
        }


        /// <summary>
        /// Toggle a given door's state (open/closed) and send updates to users nearby.
        /// </summary>
        /// <param name="x">The X coordinate of the door.</param>
        /// <param name="y">The Y coordinate of the door.</param>
        /// <returns></returns>
        public void ToggleDoor(byte x, byte y)
        {
            var coords = new Tuple<byte, byte>(x, y);
            Logger.DebugFormat("Door {0}@{1},{2}: Open: {3}, changing to {4}",
                Name, x, y, Doors[coords].Closed,
                !Doors[coords].Closed);

            Doors[coords].Closed = !Doors[coords].Closed;

            // There are several examples of doors in Temuair that trigger graphic
            // changes but do not trigger collision updates (e.g. 3-panel doors in
            // Piet & Undine).
            if (Doors[coords].UpdateCollision)
            {
                Logger.DebugFormat("Door {0}@{1},{2}: updateCollision is set, collisions are now {3}",
                    Name, x, y, !Doors[coords].Closed);
                IsWall[x, y] = !IsWall[x, y];
            }

            Logger.DebugFormat("Toggling door at {0},{1}", x, y);
            Logger.DebugFormat("Door is now in state: Open: {0} Collision: {1}", Doors[coords].Closed, IsWall[x, y]);

            var updateViewport = GetViewport(x, y);

            foreach (var obj in EntityTree.GetObjects(updateViewport))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    Logger.DebugFormat("Sending door packet to {0}: X {1}, Y {2}, Open {3}, LR {4}",
                        user.Name, x, y, Doors[coords].Closed,
                        Doors[coords].IsLeftRight);

                    user.SendDoorUpdate(x, y, Doors[coords].Closed,
                        Doors[coords].IsLeftRight);
                }
            }
        }

        /// <summary>
        /// Toggles a door panel at x,y and depending on whether there are doors
        /// next to it, will open those as well. This code is pretty ugly, to boot.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void ToggleDoors(byte x, byte y)
        {
            var coords = new Tuple<byte, byte>(x, y);
            var door = Doors[coords];

            // First, toggle the actual door itself

            ToggleDoor(x, y);

            // Now, toggle any potentially adjacent "doors"

            if (door.IsLeftRight)
            {
                // Look for a door at x-1, x+1, and open if they're present
                Objects.Door nextdoor;
                var door1Coords = new Tuple<byte, byte>((byte)(x - 1), (byte)(y));
                var door2Coords = new Tuple<byte, byte>((byte)(x + 1), (byte)(y));
                if (Doors.TryGetValue(door1Coords, out nextdoor))
                {
                    ToggleDoor((byte)(x - 1), (byte)(y));
                }
                if (Doors.TryGetValue(door2Coords, out nextdoor))
                {
                    ToggleDoor((byte)(x + 1), (byte)(y));
                }

            }
            else
            {
                // Look for a door at y-1, y+1 and open if they're present
                Objects.Door nextdoor;
                var door1Coords = new Tuple<byte, byte>((byte)(x), (byte)(y - 1));
                var door2Coords = new Tuple<byte, byte>((byte)(x), (byte)(y + 1));
                if (Doors.TryGetValue(door1Coords, out nextdoor))
                {
                    ToggleDoor((byte)(x), (byte)(y - 1));
                }
                if (Doors.TryGetValue(door2Coords, out nextdoor))
                {
                    ToggleDoor((byte)(x), (byte)(y + 1));
                }
            }
        }


        public Rectangle GetViewport(byte x, byte y)
        {
            return new Rectangle((x - Constants.VIEWPORT_SIZE / 2),
                (y - Constants.VIEWPORT_SIZE / 2), Constants.VIEWPORT_SIZE,
                Constants.VIEWPORT_SIZE);
        }

        public Rectangle GetShoutViewport(byte x, byte y)
        {
            return new Rectangle((x - Constants.VIEWPORT_SIZE),
                (y - Constants.VIEWPORT_SIZE), Constants.VIEWPORT_SIZE * 2,
                Constants.VIEWPORT_SIZE * 2);
        }

        public void Remove(VisibleObject obj)
        {
            if (Objects.Remove(obj))
            {
                EntityTree.Remove(obj);

                if (obj is User)
                {
                    var user = obj as User;
                    Users.Remove(user.Name);
                    if (user.ActiveExchange != null)
                        user.ActiveExchange.CancelExchange(user);
                }

                var affectedObjects = EntityTree.GetObjects(obj.GetViewport());

                foreach (var target in affectedObjects)
                {
                    // If the target of a Remove is a player, we insert a 250ms delay to allow the animation
                    // frame to complete.
                    if (target is User)
                        ((User)target).AoiDeparture(obj, 250);
                    else
                        target.AoiDeparture(obj);

                    obj.AoiDeparture(target);
                }

                obj.Map = null;
                obj.X = 0;
                obj.Y = 0;
            }
        }

        public void AddGold(int x, int y, Gold gold)
        {
            Logger.DebugFormat("{0}, {1}, {2} qty {3} id {4}",
                x, y, gold.Name, gold.Amount, gold.Id);
            if (gold == null)
            {
                Logger.DebugFormat("Item is null, aborting");
                return;
            }
            // Add the gold to the world at the given location.
            gold.X = (byte)x;
            gold.Y = (byte)y;
            gold.Map = this;
            EntityTree.Add(gold);
            Objects.Add(gold);
            NotifyNearbyAoiEntry(gold);
        }

        public void AddItem(int x, int y, Item item)
        {
            Logger.DebugFormat("{0}, {1}, {2} qty {3} id {4}",
                x, y, item.Name, item.Count, item.Id);
            if (item == null)
            {
                Logger.DebugFormat("Item is null, aborting");
                return;
            }            
            // Add the item to the world at the given location.
            item.X = (byte)x;
            item.Y = (byte)y;
            item.Map = this;
            EntityTree.Add(item);
            Objects.Add(item);
            NotifyNearbyAoiEntry(item);
        }

        public void RemoveGold(Gold gold)
        {
            // Remove the gold from the world at the specified location.
            Logger.DebugFormat("Removing {0} qty {1} id {2}", gold.Name, gold.Amount, gold.Id);
            NotifyNearbyAoiDeparture(gold);
            EntityTree.Remove(gold);
            Objects.Remove(gold);
        }

        public void RemoveItem(Item item)
        {
            // Remove the item from the world at the specified location.
            Logger.DebugFormat("Removing {0} qty {1} id {2}", item.Name, item.Count, item.Id);
            NotifyNearbyAoiDeparture(item);
            EntityTree.Remove(item);
            Objects.Remove(item);
        }


        public void NotifyNearbyAoiEntry(VisibleObject objectToAdd)
        {
            foreach (var obj in EntityTree.GetObjects(objectToAdd.GetViewport()))
            {
                if (obj is User)
                {
                    Logger.DebugFormat("Notifying {0} of item {1} at {2},{3} with sprite {4}", obj.Name, objectToAdd.Name,
                        objectToAdd.X, objectToAdd.Y, objectToAdd.Sprite);
                    var user = obj as User;
                    user.AoiEntry(objectToAdd);
                }
            }
        }

        public void NotifyNearbyAoiDeparture(VisibleObject objectToRemove)
        {
            foreach (var obj in EntityTree.GetObjects(objectToRemove.GetViewport()))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    user.AoiDeparture(objectToRemove);
                }
            }

        }

        public bool IsValidPoint(short x, short y)
        {
            return x >= 0 && x < X && y >= 0 && y < Y;
        }
 
    }


    public enum WarpType
    {
        Map,
        WorldMap
    }

    public class Warp
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Map SourceMap { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
        public string DestinationMapName { get; set; }
        public WarpType WarpType { get; set; }
        public byte DestinationX { get; set; }
        public byte DestinationY { get; set; }
        public byte MinimumLevel { get; set; }
        public byte MaximumLevel { get; set; }
        public byte MinimumAbility { get; set; }
        public byte MaximumAbility { get; set; }
        public bool MobUse { get; set; }

        public Warp(Map sourceMap, string destinationMap, byte sourceX, byte sourceY)
        {
            SourceMap = sourceMap;
            DestinationMapName = destinationMap;
            X = sourceX;
            Y = sourceY;
            MinimumLevel = 0;
            MaximumLevel = 255;
            MinimumAbility = 0;
            MaximumAbility = 255;
            MobUse = true;
        }

        public bool Use(User target)
        {
            Logger.DebugFormat("warp: {0} from {1} ({2},{3}) to {4} ({5}, {6}", target.Name, SourceMap.Name, X, Y,
                DestinationMapName, DestinationX, DestinationY);
            switch (WarpType)
            {
                case WarpType.Map:
                    Map map;
                    if (SourceMap.World.MapCatalog.TryGetValue(DestinationMapName, out map))
                    {
                        Thread.Sleep(250);
                        target.Teleport(map.Id, DestinationX, DestinationY);
                    }
                    Logger.ErrorFormat("User {0} tried to warp to nonexistent map {1} from {2}: {3},{4}", target.Name,
                        DestinationMapName, SourceMap.Name, X, Y);
                    break;
                case WarpType.WorldMap:
                    WorldMap wmap;
                    if (SourceMap.World.WorldMaps.TryGetValue(DestinationMapName, out wmap))
                    {
                        SourceMap.Remove(target);
                        target.SendWorldMap(wmap);
                        SourceMap.World.Maps[Hybrasyl.Constants.LAG_MAP].Insert(target, 5, 5, false);
                    }
                    Logger.ErrorFormat("User {0} tried to warp to nonexistent worldmap {1} from {2}: {3},{4}",
                        target.Name,
                        DestinationMapName, SourceMap.Name, X, Y);
                    break;
            }
            return false;
        }
    }

    public struct Point
    {
        public static int Distance(int x1, int y1, int x2, int y2)
        {
            return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
        }
        public static int Distance(VisibleObject obj1, VisibleObject obj2)
        {
            return Distance(obj1.X, obj1.Y, obj2.X, obj2.Y);
        }
        public static int Distance(VisibleObject obj, int x, int y)
        {
            return Distance(obj.X, obj.Y, x, y);
        }
    }
}
