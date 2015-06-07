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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using C3;
using Hybrasyl.Objects;
using log4net;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Linq;
using hybrasyl.Util;
using Hybrasyl.Enums;

namespace Hybrasyl.Properties
{
    public partial class Door
    {
        public bool Open { get; set; }
    }
}

namespace Hybrasyl
{
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

        public List<Warp> Warps { get; set; }
        public string Message { get; set; }

        public QuadTree<VisibleObject> EntityTree { get; set; }

        public HashSet<VisibleObject> Objects { get; private set; }
        public Dictionary<string, User> Users { get; private set; }

        public Dictionary<Tuple<byte, byte>, WorldWarp> WorldWarps { get; set; }
        public Dictionary<Tuple<byte, byte>, Objects.Door> Doors { get; set; }
        public Dictionary<Tuple<byte, byte>, Signpost> Signposts { get; set; }
        public List<spawns> Spawns { get; set; }
        public Dictionary<int, List<Monster>> Monsters { get; set; }

        public Map()
        {
            RawData = new byte[0];
            Objects = new HashSet<VisibleObject>();
            Users = new Dictionary<string, User>();
            Warps = new List<Warp>();
            WorldWarps = new Dictionary<Tuple<byte, byte>, WorldWarp>();
            EntityTree = new QuadTree<VisibleObject>(1, 1, X, Y);
            Doors = new Dictionary<Tuple<byte, byte>, Objects.Door>();
            Signposts = new Dictionary<Tuple<byte, byte>, Signpost>();
            Spawns = new List<spawns>();
            Monsters = new Dictionary<int, List<Monster>>();
        }

        public WorldObject GetInfront(User player, int tileCount = 1)
        {
            for (var i = 1; i <= tileCount; i++)
            {
                switch (player.Direction)
                {
                    case Enums.Direction.North:
                        return GetWorldObjectAt(player.X, player.Y - i);
                    case Enums.Direction.East:
                        return GetWorldObjectAt(player.X + i, player.Y);
                    case Enums.Direction.South:
                        return GetWorldObjectAt(player.X, player.Y + i);
                    case Enums.Direction.West:
                        return GetWorldObjectAt(player.X - i, player.Y);
                }
            }

            return null;
        }

        public WorldObject GetWorldObjectAt(int x, int y)
        {
            WorldObject obj = null;

            foreach (var mapobj in EntityTree)
            {
                if (mapobj != null
                    && mapobj.X == x && mapobj.Y == y)
                {
                    obj = mapobj;
                    goto returnResult;
                }
            }
            returnResult:
            return obj;
        }


        public List<VisibleObject> GetTileContents(int x, int y)
        {
            return EntityTree.GetObjects(new Rectangle(x, y, 1, 1));
        }

        public void InsertNpc(npcs toinsert)
        {
            var merchant = new Merchant(toinsert);
            World.Insert(merchant);
            Insert(merchant, merchant.X, merchant.Y);
            merchant.OnSpawn();
        }

        public void InsertSignpost(signposts toinsert)
        {
            var post = new Signpost(toinsert);
            World.Insert(post);
            Insert(post, post.X, post.Y);
            Signposts[new Tuple<byte, byte>(post.X, post.Y)] = post;
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

                var index = 0;
                for (var y = 0; y < Y; ++y)
                {
                    for (var x = 0; x < X; ++x)
                    {
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

                        var lfgu = (ushort)lfg;
                        var rfgu = (ushort)rfg;

                        if (Game.DoorSprites.ContainsKey(lfgu))
                        {
                            Logger.DebugFormat("Inserting LR door at {0}@{1},{2}: Collision: {3}",
                                Name, x, y, IsWall[x, y]);

                            InsertDoor((byte)x, (byte)y, IsWall[x, y], true,
                            Game.IsDoorCollision(lfgu));
                        }
                        else
                        {
                            if (Game.DoorSprites.ContainsKey(rfgu))
                            {
                                Logger.DebugFormat("Inserting UD door at {0}@{1},{2}: Collision: {3}",
                                Name, x, y, IsWall[x, y]);
                                InsertDoor((byte)x, (byte)y, IsWall[x, y], false,
                                Game.IsDoorCollision(rfgu));
                            }
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

            ToggleDoor(x, y);

            if (door.IsLeftRight)
            {
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
                    {
                        user.ActiveExchange.CancelExchange(user);
                    }
                }

                var affectedObjects = EntityTree.GetObjects(obj.GetViewport());

                foreach (var target in affectedObjects)
                {
                    if (target is User)
                    {
                        ((User)target).AoiDeparture(obj, 250);
                    }
                    else
                    {
                        target.AoiDeparture(obj);
                    }
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
            item.X = (byte)x;
            item.Y = (byte)y;
            item.Map = this;
            EntityTree.Add(item);
            Objects.Add(item);
            NotifyNearbyAoiEntry(item);
        }

        public void RemoveGold(Gold gold)
        {
            Logger.DebugFormat("Removing {0} qty {1} id {2}", gold.Name, gold.Amount, gold.Id);
            NotifyNearbyAoiDeparture(gold);
            EntityTree.Remove(gold);
            Objects.Remove(gold);
        }

        public void RemoveItem(Item item)
        {
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

        internal void Update(TimeSpan delta)
        {
            foreach (var spawn in Spawns)
            {
                if (Monsters[spawn.Id].Count < spawn.Quantity)
                {
                    var template = World.Monsters[spawn.Id];
                    var monster = new Monster();
                    monster.BashTimer = new GameServerTimer(TimeSpan.FromSeconds(template.Speed));
                    monster.CastTimer = new GameServerTimer(TimeSpan.FromSeconds(template.Speed));
                    monster.WalkTimer = new GameServerTimer(TimeSpan.FromSeconds(template.Speed));
                    monster.Template = template;
                    monster.Id = (uint)Generator.GenerateNumber();
                    monster.Sprite = template.Sprite;
                    monster.OffensiveElement = template.OffensiveElement;
                    monster.DefensiveElement = template.DefensiveElement;

                    do
                    {
#warning Add MapXY to mobs Template

                        monster.X = (byte)Generator.Random.Next(1, (int)100);
                        monster.Y = (byte)Generator.Random.Next(1, (int)100);
                    }
                    while (IsWall[monster.X, monster.Y]);

                    monster.Direction = (Direction)Generator.Random.Next(0, 4);

                    foreach (var mapobj in EntityTree.GetObjects(monster.GetViewport()))
                    {
                        if (mapobj is User)
                        {
                            if (mapobj == null)
                            {
                                continue;
                            }

                            if ((mapobj as User).WithinRangeOf(monster))
                                Insert(monster, monster.X, monster.Y, true);
                        }
                    }

                }
            }
        }
    }

    public struct Warp
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public ushort DestinationMap { get; set; }
        public byte DestinationX { get; set; }
        public byte DestinationY { get; set; }
        public byte MinimumLevel { get; set; }
        public byte MaximumLevel { get; set; }
        public byte MinimumAbility { get; set; }
        public bool MobsCanUse { get; set; }
    }

    public struct WorldWarp
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public byte WorldmapId { get; set; }
        public byte MinimumLevel { get; set; }
        public byte MaximumLevel { get; set; }
        public byte MinimumAbility { get; set; }
        public WorldMap DestinationWorldMap { get; set; }
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
