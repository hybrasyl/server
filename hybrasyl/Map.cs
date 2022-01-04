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

using C3;
using Hybrasyl.Objects;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;

namespace Hybrasyl
{


    public class MapPoint
    {
        public long Id
        {
            get
            {
                unchecked
                {
                    return Name.GetHashCode() + X + Y + Parent.GetHashCode();
                }
            }
        }

        public string Parent { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Name { get; set; }
        public string DestinationMap { get; set; }
        public byte DestinationX { get; set; }
        public byte DestinationY { get; set; }

        public int XOffset => X%255;
        public int YOffset => Y%255;
        public int XQuadrant => (X - XOffset)/255;
        public int YQuadrant => (Y - YOffset) / 255;

        public MapPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public byte[] GetBytes()
        {
            var buffer = Encoding.ASCII.GetBytes(Name);
            GameLog.DebugFormat("buffer is {0} and Name is {1}", BitConverter.ToString(buffer), Name);

            // X quadrant, offset, Y quadrant, offset, length of the name, the name, plus a 64-bit(?!) ID
            var bytes = new List<Byte>();

            GameLog.DebugFormat("{0}, {1}, {2}, {3}, {4}, mappoint ID is {5}", XQuadrant, XOffset, YQuadrant,
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
        public string Name { get; set; }
        public string ClientMap { get; set; }
        public List<MapPoint> Points { get; set; }
        public World World { get; set; }

        public WorldMap(Xml.WorldMap newWorldMap)
        {
            Points = new List<MapPoint>();
            Name = newWorldMap.Name;
            ClientMap = newWorldMap.ClientMap;

            foreach (var point in newWorldMap.Points.Point)
            {
                var mapPoint = new MapPoint(point.X, point.Y)
                {
                    DestinationMap = point.Target.Value,
                    DestinationX = point.Target.X,
                    DestinationY = point.Target.Y,
                    Name = point.Name,
                    Parent = this.Name
                };
                // We don't implement world map point restrictions yet, so we're done here
                Points.Add(mapPoint);
            }

        }
        public byte[] GetBytes()
        {
            // Returns the representation of the worldmap as an array of bytes, 
            // suitable to passing to a map packet.

            var buffer = Encoding.ASCII.GetBytes(ClientMap);
            var bytes = new List<Byte> {(byte) ClientMap.Length};

            bytes.AddRange(buffer);
            bytes.Add((byte)Points.Count);
            bytes.Add(0x00);

            foreach (var mappoint in Points)
            {
                bytes.AddRange(mappoint.GetBytes());
            }

            GameLog.DebugFormat("I am sending the following map packet:");
            GameLog.DebugFormat("{0}", BitConverter.ToString(bytes.ToArray()));

            return bytes.ToArray();
        }
    }

    public class Map
    {
        private object _lock = new object();

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
        public bool AllowCasting { get; set; }
        public bool AllowSpeaking { get; set; }

        public Dictionary<Tuple<byte, byte>, Warp> Warps { get; set; }
        public string Message { get; set; }

        public QuadTree<VisibleObject> EntityTree { get; set; }

        public HashSet<VisibleObject> Objects { get; private set; }
        public Dictionary<string, User> Users { get; private set; }

        public Dictionary<(byte X, byte Y), Door> Doors { get; set; }
        public Dictionary<(byte X, byte Y), Signpost> Signposts { get; set; }
        public Dictionary<(byte X, byte Y), Reactor> Reactors { get; set; }

        public Xml.SpawnGroup SpawnDirectives { get; set; }

        public bool SpawnDebug { get; set; }

        public bool SpawningDisabled { get; set; }


        /// <summary>
        /// Create a new Hybrasyl map from an XMLMap object.
        /// </summary>
        /// <param name="newMap">An XSD.Map object representing the XML map file.</param>
        /// <param name="theWorld">A world object where the map will be placed</param>
        public Map(Xml.Map newMap, World theWorld)
        {
            Init();
            World = theWorld;
            SpawnDebug = false;
            SpawnDirectives = newMap.SpawnGroup ?? new Xml.SpawnGroup() { Spawns = new List<Xml.Spawn>() };

            // TODO: refactor Map class to not do this, but be a partial which overlays
            // TODO: XSD.Map
            Id = newMap.Id;
            X = newMap.X;
            Y = newMap.Y;
            Name = newMap.Name;
            AllowCasting = newMap.AllowCasting;
            EntityTree = new QuadTree<VisibleObject>(0, 0, X, Y);
            Music = newMap.Music;

            foreach (var warpElement in newMap.Warps)
            {
                var warp = new Warp(this)
                {
                    X = warpElement.X,
                    Y = warpElement.Y
                };

                if (warpElement.MapTarget != null)
                {
                    // map warp
                    warp.DestinationMapName = warpElement.MapTarget.Value;
                    warp.WarpType = WarpType.Map;
                    warp.DestinationX = warpElement.MapTarget.X;
                    warp.DestinationY = warpElement.MapTarget.Y;
                }
                else if (warpElement.WorldMapTarget != string.Empty)
                {
                    // worldmap warp
                    warp.DestinationMapName = warpElement.WorldMapTarget;
                    warp.WarpType = WarpType.WorldMap;
                }
                if (warpElement.Restrictions?.Level != null)
                {
                    warp.MinimumLevel = warpElement.Restrictions.Level.Min;
                    warp.MaximumLevel = warpElement.Restrictions.Level.Max;
                }
                if (warpElement.Restrictions?.Ab != null)
                {
                    warp.MinimumAbility = warpElement.Restrictions.Ab.Min;
                    warp.MaximumAbility = warpElement.Restrictions.Ab.Max;
                }
                warp.MobUse = warpElement.Restrictions?.NoMobUse ?? true;
                Warps[new Tuple<byte, byte>(warp.X, warp.Y)] = warp;
            }

            foreach (var npcElement in newMap.Npcs)
            {
                var npcTemplate = World.WorldData.Get<Xml.Npc>(npcElement.Name);
                if (npcTemplate == null)
                {
                    GameLog.Error("map ${Name}: NPC ${npcElement.Name} is missing, will not be loaded");
                    continue;
                }

                var merchant = new Merchant(npcTemplate)
                {
                    X = npcElement.X,
                    Y = npcElement.Y,
                    Name = npcElement.Name,
                    Direction = npcElement.Direction,
                };
                InsertNpc(merchant);
                // Keep the actual spawned object around in the index for later use
                World.WorldData.Set(merchant.Name, merchant);
            }

            foreach (var reactorElement in newMap.Reactors)
            {
                var reactor = new Reactor(reactorElement.X, reactorElement.Y, this, 
                    reactorElement.Script, 0, reactorElement.Description, reactorElement.Blocking);
                reactor.AllowDead = reactorElement.AllowDead;
                InsertReactor(reactor);
                GameLog.Debug($"{reactor.Id} placed in {reactor.Map.Name}, description was {reactor.Description}");
            }
            foreach (var sign in newMap.Signs)
            {
                Signpost post;
                post = sign.Type == Xml.BoardType.Sign ? new Signpost(sign.X, sign.Y, sign.Message) : new Signpost(sign.X, sign.Y, sign.Message, true, sign.BoardKey);
                InsertSignpost(post);
            }
            Load();
        }
    
        
        public Map()
        {
            Init();
        }

        public void MapMute() => AllowSpeaking = false;
        public void MapUnmute() => AllowSpeaking = true;

        public void Init()
        {
            RawData = new byte[0];
            Objects = new HashSet<VisibleObject>();
            Users = new Dictionary<string, User>();
            Warps = new Dictionary<Tuple<byte, byte>, Warp>();
            EntityTree = new QuadTree<VisibleObject>(1, 1, X, Y);
            Doors = new Dictionary<(byte X, byte Y), Door>();
            Signposts = new Dictionary<(byte X, byte Y), Signpost>();
            Reactors = new Dictionary<(byte X, byte Y), Reactor>();
            AllowSpeaking = true;
        }

        public List<VisibleObject> GetTileContents(int x1, int y1)
        {
            lock (_lock)
            {
                return EntityTree.GetObjects(new Rectangle(x1, y1, 1, 1));
            }
        }

        public List<Creature> GetCreatures(int x1, int y1) => GetTileContents(x1, y1).OfType<Creature>().ToList();

        public bool IsCreatureAt(int x1, int y1) => GetTileContents(x1,y1).Any(x => x is Creature);

        public void InsertNpc(Merchant toInsert)
        {
            World.Insert(toInsert);
            Insert(toInsert, toInsert.X, toInsert.Y);
            try
            {
                toInsert.OnSpawn();
            }
            catch (Exception e)
            {
                GameLog.Error("NPC {name}: exception occurred, aborting: {e}", toInsert.Name, e);
            }
        }
        public void InsertCreature(Creature toInsert)
        {
            World.Insert(toInsert);
            Insert(toInsert, toInsert.X, toInsert.Y);
            GameLog.DebugFormat("Monster {0} with id {1} spawned.", toInsert.Name, toInsert.Id);
        }

        public void RemoveCreature(Creature toRemove)
        {
            World.Remove(toRemove);
            Remove(toRemove);
            GameLog.DebugFormat("Removing creature {0} (id {1})", toRemove.Name, toRemove.Id);
        }

        public void InsertReactor(Reactor toInsert)
        {
            World.Insert(toInsert);
            Insert(toInsert, toInsert.X, toInsert.Y);
            Reactors[(toInsert.X, toInsert.Y)] = toInsert;
        }

        public void InsertSignpost(Objects.Signpost post)
        {
            World.Insert(post);
            Insert(post, post.X, post.Y);
            Signposts[(post.X, post.Y)] = post;
            GameLog.InfoFormat("Inserted signpost {0}@{1},{2}", post.Map.Name, post.X, post.Y);
        }

        private void InsertDoor(byte x, byte y, bool open, bool isLeftRight, bool triggerCollision = true)
        {
            var door = new Objects.Door(x, y, open, isLeftRight, triggerCollision);
            World.Insert(door);
            Insert(door, door.X, door.Y);
            Doors[(door.X, door.Y)] = door;
        }

        public bool Load()
        {
            IsWall = new bool[X, Y];
            var filename = Path.Combine(World.MapFileDirectory, $"lod{Id}.map");

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
                            GameLog.DebugFormat("Inserting LR door at {0}@{1},{2}: Collision: {3}",
                                Name, x, y, IsWall[x, y]);

                            InsertDoor((byte)x, (byte)y, IsWall[x, y], true,
                            Game.IsDoorCollision(lfgu));
                        }
                        else if (Game.DoorSprites.ContainsKey(rfgu))
                        {
                            GameLog.DebugFormat("Inserting UD door at {0}@{1},{2}: Collision: {3}",
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
            lock (_lock)
            {
                if (Objects.Add(obj))
                {
                    obj.Map = this;
                    obj.X = x;
                    obj.Y = y;

                    EntityTree.Add(obj);

                    if (obj is User u)
                    {
                        Users.Add(u.Name, u);
                    }
                }

                if (obj is User user)
                {
                    if (updateClient)
                    {
                        obj.SendMapInfo();
                        obj.SendLocation();
                    }

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
            var coords = (x, y);
            GameLog.DebugFormat("Door {0}@{1},{2}: Open: {3}, changing to {4}",
                Name, x, y, Doors[coords].Closed,
                !Doors[coords].Closed);

            Doors[coords].Closed = !Doors[coords].Closed;

            // There are several examples of doors in Temuair that trigger graphic
            // changes but do not trigger collision updates (e.g. 3-panel doors in
            // Piet & Undine).
            if (Doors[coords].UpdateCollision)
            {
                GameLog.DebugFormat("Door {0}@{1},{2}: updateCollision is set, collisions are now {3}",
                    Name, x, y, !Doors[coords].Closed);
                IsWall[x, y] = !IsWall[x, y];
            }

            GameLog.DebugFormat("Toggling door at {0},{1}", x, y);
            GameLog.DebugFormat("Door is now in state: Open: {0} Collision: {1}", Doors[coords].Closed, IsWall[x, y]);

            var updateViewport = GetViewport(x, y);

            foreach (var obj in EntityTree.GetObjects(updateViewport))
            {
                if (obj is User)
                {
                    var user = obj as User;
                    GameLog.DebugFormat("Sending door packet to {0}: X {1}, Y {2}, Open {3}, LR {4}",
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
            var coords = (x, y);
            var door = Doors[coords];

            // First, toggle the actual door itself

            ToggleDoor(x, y);

            // Now, toggle any potentially adjacent "doors"

            if (door.IsLeftRight)
            {
                // Look for a door at x-1, x+1, and open if they're present
                Objects.Door nextdoor;
                var door1Coords = ((byte)(x - 1), y);
                var door2Coords = ((byte)(x + 1), y);
                if (Doors.TryGetValue(door1Coords, out nextdoor))
                {
                    ToggleDoor((byte)(x - 1), y);
                }
                if (Doors.TryGetValue(door2Coords, out nextdoor))
                {
                    ToggleDoor((byte)(x + 1), y);
                }

            }
            else
            {
                // Look for a door at y-1, y+1 and open if they're present
                Objects.Door nextdoor;
                var door1Coords = (x, (byte)(y - 1));
                var door2Coords = (x, (byte)(y + 1));
                if (Doors.TryGetValue(door1Coords, out nextdoor))
                {
                    ToggleDoor(x, (byte)(y - 1));
                }
                if (Doors.TryGetValue(door2Coords, out nextdoor))
                {
                    ToggleDoor(x, (byte)(y + 1));
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
            var user = obj as User;
            List<VisibleObject> affectedObjects = new List<VisibleObject>();

            lock (_lock)
            {
                if (Objects.Remove(obj))
                {
                    EntityTree.Remove(obj);
                    affectedObjects = EntityTree.GetObjects(obj.GetViewport());

                    if (user != null)
                        Users.Remove(user.Name);
                }
                else
                    GameLog.Fatal("Failed to remove gameobject id: {0} name: {1}", obj.Id, obj.Name);
            }

            user?.ActiveExchange?.CancelExchange(user);

            foreach (var target in affectedObjects)
            {
                // If the target of a Remove is a player, we insert a 250ms delay to allow the animation
                // frame to complete, or a slight delay to allow a kill animation to finish animating.
                // Yes, this is a thing we do.
                if (target is User && obj is User)
                    ((User)target).AoiDeparture(obj, 250);
                else if (target is User && obj is Creature)
                    ((User)target).AoiDeparture(obj, 100);
                else
                    target.AoiDeparture(obj);

                obj.AoiDeparture(target);
            }

            obj.Map = null;
        }
        

        public void AddGold(int x, int y, Gold gold)
        {
            GameLog.DebugFormat("{0}, {1}, {2} qty {3} id {4}",
                x, y, gold.Name, gold.Amount, gold.Id);
            if (gold == null)
            {
                GameLog.DebugFormat("ItemObject is null, aborting");
                return;
            }
            // Add the gold to the world at the given location.
            gold.X = (byte)x;
            gold.Y = (byte)y;
            gold.Map = this;
            lock (_lock)
            {
                EntityTree.Add(gold);
                Objects.Add(gold);
            }
            NotifyNearbyAoiEntry(gold);
        }

        public void AddItem(int x, int y, ItemObject itemObject)
        {
            GameLog.DebugFormat("{0}, {1}, {2} qty {3} id {4}",
                x, y, itemObject.Name, itemObject.Count, itemObject.Id);
            if (itemObject.Id == 0)
                World.Insert(itemObject);

            if (itemObject == null)
            {
                GameLog.DebugFormat("ItemObject is null, aborting");
                return;
            }            
            // Add the ItemObject to the world at the given location.
            itemObject.X = (byte)x;
            itemObject.Y = (byte)y;
            itemObject.Map = this;
            lock (_lock)
            {
                EntityTree.Add(itemObject);
                Objects.Add(itemObject);
            }
            NotifyNearbyAoiEntry(itemObject);
        }

        public void RemoveGold(Gold gold)
        {
            // Remove the gold from the world at the specified location.
            GameLog.DebugFormat("Removing {0} qty {1} id {2}", gold.Name, gold.Amount, gold.Id);
            NotifyNearbyAoiDeparture(gold);
            lock (_lock)
            {
                EntityTree.Remove(gold);
                Objects.Remove(gold);
            }
            World.Remove(gold);
        }

        public void RemoveItem(ItemObject itemObject)
        {
            // Remove the ItemObject from the world at the specified location.
            GameLog.DebugFormat("Removing {0} qty {1} id {2}", itemObject.Name, itemObject.Count, itemObject.Id);
            NotifyNearbyAoiDeparture(itemObject);
            lock (_lock)
            {
                EntityTree.Remove(itemObject);
                Objects.Remove(itemObject);
            }
        }


        public void NotifyNearbyAoiEntry(VisibleObject objectToAdd)
        {
            foreach (var obj in EntityTree.GetObjects(objectToAdd.GetViewport()))
            {
                if (obj is User)
                {
                    GameLog.DebugFormat("Notifying {0} of object {1} at {2},{3} with sprite {4}", obj.Name, objectToAdd.Name,
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

        /// <summary>
        /// Find the nearest empty tile (e.g. non-wall, not containing an NPC or a player) next to the specified x, y coordinates.
        /// </summary>
        /// <param name="xStart">X location to start search</param>
        /// <param name="yStart">Y location to start search</param>
        /// <returns>x,y tuple of nearest empty tile</returns>
        public (byte x, byte y) FindEmptyTile(byte xStart, byte yStart)
        {
            byte retx = 0;
            byte rety = 0;
            int radius = 1;
            // TODO: update to check for map being full / other edge cases
            do
            {   
                for (int x = -1 * radius; x <= radius; x++)
                {
                    for (int y = -1 * radius; y <= radius; y++)
                    {
                        if (IsWall[xStart + x, yStart + y] || GetTileContents(xStart + x, yStart + y).Where(x => x is Creature).Count() > 0)
                            continue;
                        else
                        {
                            retx = (byte) (xStart + x);
                            rety = (byte) (yStart + y);
                            break;
                        }
                    }
                }
                radius++;
                // Don't go on forever here
                if (radius > 3) break;
            } while (true);

            return (retx, rety);
        }
 
    }


    public enum WarpType
    {
        Map,
        WorldMap
    }

    public class Warp
    {
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

        public Warp(Map sourceMap)
        {
            SourceMap = sourceMap;
            _initializeWarp();
        }

        public Warp(Map sourceMap, string destinationMap, byte sourceX, byte sourceY)
        {
            SourceMap = sourceMap;
            DestinationMapName = destinationMap;
            X = sourceX;
            Y = sourceY;
            _initializeWarp();         
        }

        private void _initializeWarp()
        {
            MinimumLevel = 0;
            MaximumLevel = 255;
            MinimumAbility = 0;
            MaximumAbility = 255;
            MobUse = true;
        }

        public bool Use(User target)
        {
            GameLog.DebugFormat("warp: {0} from {1} ({2},{3}) to {4} ({5}, {6}", target.Name, SourceMap.Name, X, Y,
                DestinationMapName, DestinationX, DestinationY);
            switch (WarpType)
            {
                case WarpType.Map:
                    Map map;
                    if (SourceMap.World.WorldData.TryGetValueByIndex(DestinationMapName, out map))
                    {
                        target.Teleport(map.Id, DestinationX, DestinationY);
                        return true;
                    }
                    GameLog.ErrorFormat("User {0} tried to warp to nonexistent map {1} from {2}: {3},{4}", target.Name,
                        DestinationMapName, SourceMap.Name, X, Y);
                    break;
                case WarpType.WorldMap:
                    WorldMap wmap;
                    if (SourceMap.World.WorldData.TryGetValue(DestinationMapName, out wmap))
                    {
                        SourceMap.Remove(target);
                        target.SendWorldMap(wmap);
                        SourceMap.World.WorldData.Get<Map>(Hybrasyl.Constants.LAG_MAP).Insert(target, 5, 5, false);
                        return true;
                    }
                    GameLog.ErrorFormat("User {0} tried to warp to nonexistent worldmap {1} from {2}: {3},{4}",
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
