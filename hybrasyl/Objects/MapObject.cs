// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Crc;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Servers;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Hybrasyl.Internals;

namespace Hybrasyl.Objects;

public class MapObject : IStateStorable
{
    private readonly object _lock = new();

    /// <summary>
    ///     Create a new Hybrasyl map from an XMLMap object.
    /// </summary>
    /// <param name="newMap">An XSD.Map object representing the XML map file.</param>
    /// <param name="theWorld">A world object where the map will be placed</param>
    public MapObject(Map newMap, World theWorld)
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
        Flags = newMap.Flags;

        LoadMapFile();
        LoadXml(newMap);
        for (byte x = 0; x <= X; x++)
            for (byte y = 0; y <= Y; y++)
            {
                if (IsWall(x, y)) continue;
                UsableTiles.Add((x, y));
            }
    }

    public ushort Id { get; set; }

    [FormulaVariable] public byte X { get; set; }

    [FormulaVariable] public byte Y { get; set; }

    [FormulaVariable] public int Tiles => X * Y;

    [FormulaVariable] public byte BaseLevel => byte.TryParse(SpawnDirectives.BaseLevel, out var b) ? b : (byte)1;

    public string Name { get; set; }
    public MapFlags Flags { get; set;}
    public byte Music { get; set; }
    public World World { get; set; }
    public byte[] RawData { get; set; }
    public ushort Checksum { get; set; }

    private HashSet<(byte x, byte y)> Collisions { get; set; } = new();
    private HashSet<(byte x, byte y)> UsableTiles { get; } = new();

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

    public bool IsWall(int x, int y) => IsWall((byte)x, (byte)y);
    public bool IsWall(byte x, byte y) => Collisions.Contains((x, y));
    public bool IsWall((byte x, byte y) coordinate) => IsWall(coordinate.x, coordinate.y);

    public (int x, int y) FindEmptyTile()
    {
        var tiles = new HashSet<(byte x, byte y)>(UsableTiles);

        do
        {
            var rand = Random.Shared.Next(0, tiles.Count);
            var randTile = tiles.ElementAt(rand);
            if (IsCreatureAt(randTile.x, randTile.y)) continue;
            return (randTile.x, randTile.y);
        } while (tiles.Count > 0);

        return (-1, -1);
    }

    public void ToggleCollisions(byte x, byte y)
    {
        if (Collisions.Contains((x, y)))
            Collisions.Remove((x, y));
        else
            Collisions.Add((x, y));
    }

    public void MapMute()
    {
        AllowSpeaking = false;
    }

    public void MapUnmute()
    {
        AllowSpeaking = true;
    }

    /// <summary>
    ///     Remove all objects on a map except for NPCs. This function is only intended to be used by unit tests.
    ///     It is almost assuredly not what you want in a live environment.
    /// </summary>
    public void Clear()
    {
        foreach (var obj in EntityTree.GetAllObjects().Where(predicate: obj => obj is not Merchant))
        {
            EntityTree.Remove(obj);
            Objects.Remove(obj);
        }

        Users = new Dictionary<string, User>();
        Warps = new Dictionary<Tuple<byte, byte>, Warp>();
        Reactors = new Dictionary<(byte X, byte Y), Dictionary<Guid, Reactor>>();
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

    public void LoadXml(Map newMap)
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
                Direction = npcElement.Direction,
                DisplayName = string.IsNullOrWhiteSpace(npcElement.DisplayName) ? npcTemplate.Name : npcElement.DisplayName
            };
            InsertNpc(merchant);
            // Keep the actual spawned object around in the index for later use
            World.WorldState.Set(merchant.Name, merchant);
        }

        foreach (var reactorElement in newMap.Reactors)
        {
            var reactor = new Reactor(reactorElement, this);
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

    public bool IsCreatureAt(int x1, int y1) =>
        GetTileContents(x1, y1).Any(predicate: x => x is Creature);

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

    public void InsertMonster(Monster toInsert)
    {
        World.Insert(toInsert);
        Insert(toInsert, toInsert.X, toInsert.Y); 
        toInsert.SpawnPoint = new LocationInfo { Map = this, X = toInsert.X, Y = toInsert.Y };
        GameLog.DebugFormat("Monster {0} with id {1} spawned.", toInsert.Name, toInsert.Id);
    }

    public void InsertReactor(Reactor toInsert)
    {
        World.Insert(toInsert);
        Insert(toInsert, toInsert.X, toInsert.Y);
        if (!Reactors.ContainsKey((toInsert.X, toInsert.Y)))
            Reactors[(toInsert.X, toInsert.Y)] = new Dictionary<Guid, Reactor>();
        Reactors[(toInsert.X, toInsert.Y)].Add(toInsert.Guid, toInsert);
        toInsert.OnSpawn();
    }

    public void InsertSignpost(Signpost post)
    {
        World.Insert(post);
        Insert(post, post.X, post.Y);
        Signposts[(post.X, post.Y)] = post;
        GameLog.DataLogDebug($"Inserted signpost {post.Map.Name}@{post.X},{post.Y}");
    }

    private void InsertDoorGroup(DoorGroup group)
    {
        foreach (var panel in group.Panels)
        {
            var door = new Door(panel.X, panel.Y, group, panel.PanelIndex);
            World.Insert(door);
            Insert(door, door.X, door.Y);
            Doors[(door.X, door.Y)] = door;
        }
    }

    private static DoorSpriteInfo LookupDoorSprite(ushort lfg, ushort rfg)
    {
        if (Sprites.SpriteInfo.TryGetValue(lfg, out var linfo))
            return linfo;

        if (Sprites.SpriteInfo.TryGetValue(rfg, out var rinfo))
            return rinfo;

        return null;
    }

    /// <summary>
    ///     Scans the foreground sprite grids for door groups using sprite identity (not directional
    ///     offset) to place panels. Tolerates retail door definitions where doors.md's array order
    ///     runs opposite to map-coordinate increase along the axis: each panel's tile is determined
    ///     by the panel index encoded in its sprite, so a 2-tile N/S door with sprite[0] in the south
    ///     tile and sprite[1] in the north tile is collected just as cleanly as the reverse.
    ///     <br /><br />
    ///     For center-only doors, only the center panel toggles and triggers a scan. The side panels
    ///     are placed at the tiles immediately adjacent to the center along the definition's axis
    ///     and are NOT sprite-validated — per doors.md, side-panel sprites are static jamb art whose
    ///     contents are irrelevant.
    /// </summary>
    public static DoorScanResult ScanForDoorGroups(ushort[,] lfgs, ushort[,] rfgs, int width, int height)
    {
        var groups = new List<DoorGroup>();
        var warnings = new List<string>();
        var consumed = new HashSet<(byte, byte)>();

        for (byte y = 0; y < height; ++y)
            for (byte x = 0; x < width; ++x)
            {
                if (consumed.Contains((x, y))) continue;

                var info = LookupDoorSprite(lfgs[x, y], rfgs[x, y]);
                if (info is null) continue;
                if (!info.Definition.HasClosedVersion) continue;
                if (!info.IsToggling) continue;

                TryBuildDoorGroup(x, y, info, lfgs, rfgs, width, height, groups, warnings, consumed);
            }

        return new DoorScanResult(groups, warnings);
    }

    private static void TryBuildDoorGroup(
        byte anchorX,
        byte anchorY,
        DoorSpriteInfo anchorInfo,
        ushort[,] lfgs,
        ushort[,] rfgs,
        int width,
        int height,
        List<DoorGroup> groups,
        List<string> warnings,
        HashSet<(byte, byte)> consumed)
    {
        var def = anchorInfo.Definition;
        var dx = def.IsLeftRight ? 1 : 0;
        var dy = def.IsLeftRight ? 0 : 1;

        DoorPanel[] panels;
        bool closed;

        if (def.OnlyCenterChanges)
        {
            //Only the center panel anchors a scan (it's the sole toggling panel). Side panels go
            //at the immediately-adjacent tiles along the axis; their sprites are jamb art and aren't
            //validated. Which side ends up index 0 vs N-1 doesn't affect runtime behavior.
            if (anchorInfo.PanelIndex != def.CenterPanelIndex)
                return;

            panels = new DoorPanel[def.PanelCount];
            for (var i = 0; i < def.PanelCount; i++)
            {
                var offset = i - def.CenterPanelIndex;
                var px = anchorX + offset * dx;
                var py = anchorY + offset * dy;

                if (px < 0 || py < 0 || px >= width || py >= height)
                {
                    warnings.Add(
                        $"Door {def.ClosedSprites[def.CenterPanelIndex]} anchored at {anchorX},{anchorY}: " +
                        $"panel {i} at ({px},{py}) extends off-map, skipping group");
                    return;
                }

                panels[i] = new DoorPanel((byte)px, (byte)py, i);
            }

            closed = !anchorInfo.IsOpenState;
        }
        else
        {
            //Walk both directions from the anchor along the definition's axis, placing each
            //matching tile by the panel index encoded in its sprite. The arrays are sized
            //PanelCount; we drop tiles into the slot the sprite identifies, regardless of
            //walk direction.
            var coords = new (byte X, byte Y)?[def.PanelCount];
            var states = new bool?[def.PanelCount];
            coords[anchorInfo.PanelIndex] = (anchorX, anchorY);
            states[anchorInfo.PanelIndex] = anchorInfo.IsOpenState;

            WalkAxis(lfgs, rfgs, anchorX, anchorY, -dx, -dy, def, width, height, coords, states);
            WalkAxis(lfgs, rfgs, anchorX, anchorY, dx, dy, def, width, height, coords, states);

            for (var i = 0; i < def.PanelCount; i++)
            {
                if (!coords[i].HasValue)
                {
                    var found = coords.Count(c => c.HasValue);
                    warnings.Add(
                        $"Door {def.ClosedSprites[0]} anchored at {anchorX},{anchorY}: " +
                        $"missing panel {i} ({found}/{def.PanelCount} panels found), skipping group");
                    return;
                }
            }

            var firstState = states[0]!.Value;
            for (var i = 1; i < def.PanelCount; i++)
                if (states[i] != firstState)
                {
                    warnings.Add(
                        $"Door {def.ClosedSprites[0]} anchored at {anchorX},{anchorY}: " +
                        $"panels are in mixed open/closed states, skipping group");
                    return;
                }

            panels = new DoorPanel[def.PanelCount];
            for (var i = 0; i < def.PanelCount; i++)
            {
                var (cx, cy) = coords[i]!.Value;
                panels[i] = new DoorPanel(cx, cy, i);
            }

            closed = !firstState;
        }

        var updateCollision = Game.IsDoorCollision(def);
        var group = new DoorGroup(def, panels, closed, updateCollision);
        groups.Add(group);
        foreach (var p in panels) consumed.Add((p.X, p.Y));
    }

    private static void WalkAxis(
        ushort[,] lfgs,
        ushort[,] rfgs,
        int startX,
        int startY,
        int stepX,
        int stepY,
        DoorDefinition def,
        int width,
        int height,
        (byte X, byte Y)?[] coords,
        bool?[] states)
    {
        var x = startX + stepX;
        var y = startY + stepY;
        while (x >= 0 && y >= 0 && x < width && y < height)
        {
            var info = LookupDoorSprite(lfgs[x, y], rfgs[x, y]);
            if (info is null || info.Definition != def) return;
            if (coords[info.PanelIndex].HasValue) return; //duplicate panel — adjacent door, halt

            coords[info.PanelIndex] = ((byte)x, (byte)y);
            states[info.PanelIndex] = info.IsOpenState;
            x += stepX;
            y += stepY;
        }
    }

    public bool LoadMapFile()
    {
        Collisions = new HashSet<(byte x, byte y)>();
        var filename = Path.Combine(World.MapFileDirectory, $"lod{Id}.map");

        if (!File.Exists(filename))
            return false;

        RawData = File.ReadAllBytes(filename);
        Checksum = Crc16.Calculate(RawData);

        //First pass: read fg sprites into per-tile grids and compute SOTP collisions. The grids let the
        //door-group scanner probe tiles more than once without re-parsing RawData.
        var lfgs = new ushort[X, Y];
        var rfgs = new ushort[X, Y];
        var rawIndex = 0;
        for (byte y = 0; y < Y; ++y)
            for (byte x = 0; x < X; ++x)
            {
                _ = RawData[rawIndex++] | (RawData[rawIndex++] << 8);    // bg (unused post-load)
                var lfg = RawData[rawIndex++] | (RawData[rawIndex++] << 8);
                var rfg = RawData[rawIndex++] | (RawData[rawIndex++] << 8);

                if (lfg != 0 && (Game.Collisions[lfg - 1] & 0x0F) == 0x0F) Collisions.Add((x, y));

                if (rfg != 0 && (Game.Collisions[rfg - 1] & 0x0F) == 0x0F) Collisions.Add((x, y));

                lfgs[x, y] = (ushort)lfg;
                rfgs[x, y] = (ushort)rfg;
            }

        var scan = ScanForDoorGroups(lfgs, rfgs, X, Y);
        foreach (var w in scan.Warnings)
            GameLog.Warning($"{Name}: {w}");

        foreach (var group in scan.Groups)
        {
            InsertDoorGroup(group);
            var anchor = group.Panels[0];
            GameLog.DebugFormat(
                "Inserted {0}-tile {1} door group at {2}@{3},{4}: Closed={5}, UpdateCollision={6}, OnlyCenterChanges={7}",
                group.Definition.PanelCount, group.Definition.Axis, Name, anchor.X, anchor.Y,
                group.Closed, group.UpdateCollision, group.Definition.OnlyCenterChanges);
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
            else
            {
                throw new Exception("What in the fuck");
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
    ///     Toggles every panel of the <see cref="DoorGroup" /> atomically: flips the shared Closed flag, updates
    ///     collision for each toggle-able panel (if the group is collision-bearing), and emits a
    ///     <c>SendDoorUpdate</c> per panel to every user within viewport. Side panels of center-only doors are
    ///     skipped — they're static jamb art with no collision change and no visual flip, so emitting a packet
    ///     for them would cause a spurious sprite swap on the client.
    /// </summary>
    private void ToggleDoorGroup(DoorGroup group)
    {
        group.Closed = !group.Closed;

        GameLog.DebugFormat(
            "Toggling {0}-tile {1} door at {2}@{3},{4}: Closed={5}",
            group.Definition.PanelCount, group.Definition.Axis, Name,
            group.Panels[0].X, group.Panels[0].Y, group.Closed);

        foreach (var panel in group.Panels)
        {
            if (!group.Definition.IsPanelToggling(panel.PanelIndex))
                continue;

            if (group.UpdateCollision)
                ToggleCollisions(panel.X, panel.Y);

            var viewport = GetViewport(panel.X, panel.Y);
            foreach (var obj in EntityTree.GetObjects(viewport))
                if (obj is User user)
                    user.SendDoorUpdate(panel.X, panel.Y, group.Closed, group.Definition.IsLeftRight);
        }
    }

    /// <summary>
    ///     Toggle the door group at (x, y). Pre-Phase 3 this was a per-tile operation with an adjacency
    ///     scan in <c>ToggleDoors</c>; now both entry points resolve to a single group-level flip so a
    ///     click anywhere on a 2/3/4-tile door opens the whole thing.
    /// </summary>
    public void ToggleDoor(byte x, byte y) => ToggleDoors(x, y);

    /// <summary>
    ///     Toggle the door group at (x, y). Single authoritative entry point.
    /// </summary>
    public void ToggleDoors(byte x, byte y)
    {
        if (!Doors.TryGetValue((x, y), out var door))
        {
            GameLog.DebugFormat("ToggleDoors called on non-door tile at {0}@{1},{2}", Name, x, y);
            return;
        }

        ToggleDoorGroup(door.Group);
    }


    public Rectangle GetViewport(byte x, byte y) =>
        new(x - Game.ActiveConfiguration.Constants.ViewportSize / 2,
            y - Game.ActiveConfiguration.Constants.ViewportSize / 2, Game.ActiveConfiguration.Constants.ViewportSize,
            Game.ActiveConfiguration.Constants.ViewportSize);

    public Rectangle GetShoutViewport(byte x, byte y) =>
        new(x - Game.ActiveConfiguration.Constants.ViewportSize,
            y - Game.ActiveConfiguration.Constants.ViewportSize, Game.ActiveConfiguration.Constants.ViewportSize * 2,
            Game.ActiveConfiguration.Constants.ViewportSize * 2);

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
                        retx = (byte)(xStart + x);
                        rety = (byte)(yStart + y);
                        break;
                    }

            radius++;
            // Don't go on forever here
            if (radius > 3) break;
        } while (true);

        return (retx, rety);
    }
}