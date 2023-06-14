using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using C3;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Objects;

public class MapObject : IStateStorable
{
    private readonly object _lock = new();

    /// <summary>
    ///     Create a new Hybrasyl map from an XMLMap object.
    /// </summary>
    /// <param name="newMap">An XSD.Map object representing the XML map file.</param>
    /// <param name="theWorld">A world object where the map will be placed</param>
    public MapObject(Xml.Objects.Map newMap, World theWorld)
    {
        Init();
        World = theWorld;
        SpawnDebug = false;
        SpawnDirectives = newMap.SpawnGroup ?? new SpawnGroup { Spawns = new List<Spawn>() };

        // TODO: refactor Map class to not do this, but be a partial which overlays
        // TODO: XSD.Map
        Id = newMap.Id;
        X = newMap.X;
        Y = newMap.Y;
        Name = newMap.Name;
        AllowCasting = newMap.AllowCasting;
        EntityTree = new QuadTree<VisibleObject>(0, 0, X, Y);
        Music = newMap.Music;

        LoadMapFile();
        LoadXml(newMap);
    }

    public ushort Id { get; set; }

    [FormulaVariable] public byte X { get; set; }

    [FormulaVariable] public byte Y { get; set; }

    [FormulaVariable] public int Tiles => X * Y;

    [FormulaVariable] public byte BaseLevel => byte.TryParse(SpawnDirectives.BaseLevel, out var b) ? b : (byte) 1;

    public string Name { get; set; }
    public byte Flags { get; set; }
    public byte Music { get; set; }
    public World World { get; set; }
    public byte[] RawData { get; set; }
    public ushort Checksum { get; set; }

    private HashSet<(byte x, byte y)> Collisions { get; set; } = new();

    public bool IsWall(int x, int y) => IsWall((byte) x, (byte) y);
    public bool IsWall(byte x, byte y) => Collisions.Contains((x, y));

    public void ToggleCollisions(byte x, byte y)
    {
        if (Collisions.Contains((x, y)))
            Collisions.Remove((x, y));
        else
            Collisions.Add((x, y));
    }
    
    public bool AllowCasting { get; set; }
    public bool AllowSpeaking { get; set; }

    public Dictionary<Tuple<byte, byte>, Warp> Warps { get; set; }
    public string Message { get; set; }

    public QuadTree<VisibleObject> EntityTree { get; set; }

    private HashSet<VisibleObject> _objects { get; set; } = new();

    public HashSet<VisibleObject> Objects
    {
        get
        {
            lock (_lock)
            {
                return _objects;
            }
        }
        set
        {
            lock (_lock)
            {
                _objects = value;
            }
        }
    }

    public List<Monster> Monsters
    {
        get
        {
            lock (_lock)
            {
                return Objects.OfType<Monster>().ToList();
            }
        }
    }

    public Dictionary<string, User> Users { get; private set; }

    public Dictionary<(byte X, byte Y), Door> Doors { get; set; }
    public Dictionary<(byte X, byte Y), Signpost> Signposts { get; set; }
    public Dictionary<(byte X, byte Y), Dictionary<Guid, Reactor>> Reactors { get; set; }

    public SpawnGroup SpawnDirectives { get; set; }

    public bool SpawnDebug { get; set; }
    public bool SpawningDisabled { get; set; }

    public void MapMute()
    {
        AllowSpeaking = false;
    }

    public void MapUnmute()
    {
        AllowSpeaking = true;
    }

    public void Init()
    {
        RawData = new byte[0];
        Objects = new HashSet<VisibleObject>();
        Users = new Dictionary<string, User>();
        Warps = new Dictionary<Tuple<byte, byte>, Warp>();
        EntityTree = new QuadTree<VisibleObject>(1, 1, X, Y);
        Doors = new Dictionary<(byte X, byte Y), Door>();
        Signposts = new Dictionary<(byte X, byte Y), Signpost>();
        Reactors = new Dictionary<(byte X, byte Y), Dictionary<Guid, Reactor>>();
        AllowSpeaking = true;
    }

    public void LoadXml(Xml.Objects.Map newMap)
    {
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

            if (warpElement.Restrictions != null)
            {
                warp.MinimumLevel = warpElement.Restrictions.MinLev;
                warp.MaximumLevel = warpElement.Restrictions.MaxLev;
                warp.MinimumAbility = warpElement.Restrictions.MinAb;
                warp.MinimumAbility = warpElement.Restrictions.MaxAb;
                warp.MobUse = warpElement.Restrictions.MobUse;
            }

            Warps[new Tuple<byte, byte>(warp.X, warp.Y)] = warp;
        }

        foreach (var npcElement in newMap.Npcs)
        {
            if (!Game.World.WorldData.TryGetValue(npcElement.Name, out Npc npcTemplate))
            {
                GameLog.Error($"map {Name}: NPC {npcElement.Name} is missing, will not be loaded");
                continue;
            }

            var merchant = new Merchant(npcTemplate)
            {
                X = npcElement.X,
                Y = npcElement.Y,
                Name = npcElement.Name,
                Direction = npcElement.Direction
            };
            InsertNpc(merchant);
            // Keep the actual spawned object around in the index for later use
            World.WorldState.Set(merchant.Name, merchant);
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
            post = sign.Type == BoardType.Sign
                ? new Signpost(sign.X, sign.Y, sign.Message)
                : new Signpost(sign.X, sign.Y, sign.Message, true, sign.BoardKey);
            post.AoiEntryEffect = sign.Effect?.OnEntry ?? 0;
            post.AoiEntryEffectSpeed = sign.Effect?.OnEntrySpeed ?? 0;

            InsertSignpost(post);
        }
    }

    public List<VisibleObject> GetTileContents(int x1, int y1)
    {
        lock (_lock)
        {
            return EntityTree.GetObjects(new Rectangle(x1, y1, 1, 1));
        }
    }

    public List<Creature> GetCreatures(int x1, int y1) => GetTileContents(x1, y1).OfType<Creature>().ToList();

    public bool IsCreatureAt(int x1, int y1)
    {
        return GetTileContents(x1, y1).Any(predicate: x => x is Creature);
    }

    // TODO: remove World.Insert here
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
        if (!Reactors.ContainsKey((toInsert.X, toInsert.Y)))
            Reactors[(toInsert.X, toInsert.Y)] = new Dictionary<Guid, Reactor>();
        Reactors[(toInsert.X, toInsert.Y)].Add(toInsert.Guid, toInsert);
    }

    public void InsertSignpost(Signpost post)
    {
        World.Insert(post);
        Insert(post, post.X, post.Y);
        Signposts[(post.X, post.Y)] = post;
        GameLog.DataLogDebug($"Inserted signpost {post.Map.Name}@{post.X},{post.Y}");
    }

    private void InsertDoor(byte x, byte y, bool open, bool isLeftRight, bool triggerCollision = true)
    {
        var door = new Door(x, y, open, isLeftRight, triggerCollision);
        World.Insert(door);
        Insert(door, door.X, door.Y);
        Doors[(door.X, door.Y)] = door;
    }

    public bool LoadMapFile()
    {
        Collisions = new HashSet<(byte x, byte y)>();
        var filename = Path.Combine(World.MapFileDirectory, $"lod{Id}.map");

        if (!File.Exists(filename))
            return false;

        RawData = File.ReadAllBytes(filename);
        Checksum = Crc16.Calculate(RawData);

        var index = 0;
        for (byte y = 0; y < Y; ++y)
        for (byte x = 0; x < X; ++x)
        {
            var bg = RawData[index++] | (RawData[index++] << 8);
            var lfg = RawData[index++] | (RawData[index++] << 8);
            var rfg = RawData[index++] | (RawData[index++] << 8);

            if (lfg != 0 && (Game.Collisions[lfg - 1] & 0x0F) == 0x0F) Collisions.Add((x, y));

            if (rfg != 0 && (Game.Collisions[rfg - 1] & 0x0F) == 0x0F) Collisions.Add((x, y));

            var lfgu = (ushort) lfg;
            var rfgu = (ushort) rfg;

            if (Game.DoorSprites.ContainsKey(lfgu))
            {
                // This is a left-right door
                GameLog.DebugFormat("Inserting LR door at {0}@{1},{2}: Collision: {3}",
                    Name, x, y, Collisions.Contains((x, y)));

                InsertDoor((byte) x, (byte) y, Collisions.Contains((x, y)), true,
                    Game.IsDoorCollision(lfgu));
            }
            else if (Game.DoorSprites.ContainsKey(rfgu))
            {
                GameLog.DebugFormat("Inserting UD door at {0}@{1},{2}: Collision: {3}",
                    Name, x, y, Collisions.Contains((x, y)));
                // THis is an up-down door 
                InsertDoor((byte) x, (byte) y, Collisions.Contains((x, y)), false,
                    Game.IsDoorCollision(rfgu));
            }
        }

        return true;
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

                if (obj is User u) Users.Add(u.Name, u);
            }

            if (obj is User user)
                if (updateClient)
                {
                    // HS-1317: slight delay here to handle client weirdness
                    obj.SendMapInfo(250);
                    obj.SendLocation(275);
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
    ///     Toggle a given door's state (open/closed) and send updates to users nearby.
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

            ToggleCollisions(x,y);
        }

        GameLog.DebugFormat("Toggling door at {0},{1}", x, y);
        GameLog.DebugFormat("Door is now in state: Open: {0} Collision: {1}", Doors[coords].Closed, IsWall(x,y));

        var updateViewport = GetViewport(x, y);

        foreach (var obj in EntityTree.GetObjects(updateViewport))
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

    /// <summary>
    ///     Toggles a door panel at x,y and depending on whether there are doors
    ///     next to it, will open those as well. This code is pretty ugly, to boot.
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
            Door nextdoor;
            var door1Coords = ((byte) (x - 1), y);
            var door2Coords = ((byte) (x + 1), y);
            if (Doors.TryGetValue(door1Coords, out nextdoor)) ToggleDoor((byte) (x - 1), y);
            if (Doors.TryGetValue(door2Coords, out nextdoor)) ToggleDoor((byte) (x + 1), y);
        }
        else
        {
            // Look for a door at y-1, y+1 and open if they're present
            Door nextdoor;
            var door1Coords = (x, (byte) (y - 1));
            var door2Coords = (x, (byte) (y + 1));
            if (Doors.TryGetValue(door1Coords, out nextdoor)) ToggleDoor(x, (byte) (y - 1));
            if (Doors.TryGetValue(door2Coords, out nextdoor)) ToggleDoor(x, (byte) (y + 1));
        }
    }


    public Rectangle GetViewport(byte x, byte y) =>
        new(x - Constants.VIEWPORT_SIZE / 2,
            y - Constants.VIEWPORT_SIZE / 2, Constants.VIEWPORT_SIZE,
            Constants.VIEWPORT_SIZE);

    public Rectangle GetShoutViewport(byte x, byte y) =>
        new(x - Constants.VIEWPORT_SIZE,
            y - Constants.VIEWPORT_SIZE, Constants.VIEWPORT_SIZE * 2,
            Constants.VIEWPORT_SIZE * 2);

    public void Remove(VisibleObject obj)
    {
        var user = obj as User;
        var affectedObjects = new List<VisibleObject>();
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
            {
                GameLog.Fatal("Failed to remove gameobject id: {0} name: {1}", obj.Id, obj.Name);
            }
        }

        user?.ActiveExchange?.CancelExchange(user);

        foreach (var target in affectedObjects)
        {
            // If the target of a Remove is a player, we insert a 250ms delay to allow the animation
            // frame to complete, or a slight delay to allow a kill animation to finish animating.
            // Yes, this is a thing we do.
            if (target is User && obj is User)
                ((User) target).AoiDeparture(obj, 250);
            else if (target is User && obj is Creature)
                ((User) target).AoiDeparture(obj, 100);
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
        gold.X = (byte) x;
        gold.Y = (byte) y;
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
        itemObject.X = (byte) x;
        itemObject.Y = (byte) y;
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
            if (obj is User)
            {
                GameLog.DebugFormat("Notifying {0} of object {1} at {2},{3} with sprite {4}", obj.Name,
                    objectToAdd.Name,
                    objectToAdd.X, objectToAdd.Y, objectToAdd.Sprite);
                var user = obj as User;
                user.AoiEntry(objectToAdd);
            }
    }

    public void NotifyNearbyAoiDeparture(VisibleObject objectToRemove)
    {
        foreach (var obj in EntityTree.GetObjects(objectToRemove.GetViewport()))
            if (obj is User)
            {
                var user = obj as User;
                user.AoiDeparture(objectToRemove);
            }
    }

    public bool IsValidPoint(short x, short y) => x >= 0 && x < X && y >= 0 && y < Y;

    /// <summary>
    ///     Find the nearest empty tile (e.g. non-wall, not containing an NPC or a player) next to the specified x, y
    ///     coordinates.
    /// </summary>
    /// <param name="xStart">X location to start search</param>
    /// <param name="yStart">Y location to start search</param>
    /// <returns>x,y tuple of nearest empty tile</returns>
    public (byte x, byte y) FindEmptyTile(byte xStart, byte yStart)
    {
        byte retx = 0;
        byte rety = 0;
        var radius = 1;
        // TODO: update to check for map being full / other edge cases
        do
        {
            for (var x = -1 * radius; x <= radius; x++)
            for (var y = -1 * radius; y <= radius; y++)
                if (IsWall(xStart + x, yStart + y) ||
                    GetTileContents(xStart + x, yStart + y).Where(predicate: x => x is Creature).Count() > 0) { }
                else
                {
                    retx = (byte) (xStart + x);
                    rety = (byte) (yStart + y);
                    break;
                }

            radius++;
            // Don't go on forever here
            if (radius > 3) break;
        } while (true);

        return (retx, rety);
    }
}