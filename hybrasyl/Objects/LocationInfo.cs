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

using Hybrasyl.Internals.Attributes;
using Hybrasyl.Xml.Objects;
using System;

namespace Hybrasyl.Objects;

[Persistable]
public class LocationInfo : IEquatable<LocationInfo>
{
    private MapObject _map { get; set; }
    private MapObject _deathmap { get; set; }

    public MapObject Map
    {
        get => _map;
        set
        {
            _map = value;
            if (value != null)
                _mapId = Map.Id;
        }
    }

    public MapObject DeathMap
    {
        get => _deathmap;
        set
        {
            _deathmap = value;
            if (value != null)
                _deathmapId = _deathmap.Id;
        }
    }

    private ushort _mapId { get; set; }

    [Persist]
    public ushort MapId
    {
        get => Map?.Id ?? _mapId;
        set
        {
            if (Game.World.WorldState.TryGetValue(value, out MapObject map))
                Map = map;
            _mapId = value;
        }
    }

    private ushort _deathmapId { get; set; }

    [Persist]
    public ushort DeathMapId
    {
        get => DeathMap?.Id ?? _deathmapId;
        set
        {
            if (Game.World.WorldState.TryGetValue(value, out MapObject map))
                DeathMap = map;
            _deathmapId = value;
        }
    }

    [Persist] public Direction Direction { get; set; }

    [Persist] public byte X { get; set; }

    [Persist] public byte Y { get; set; }

    [Persist] public bool WorldMap { get; set; }

    [Persist] public byte DeathMapX { get; set; }

    [Persist] public byte DeathMapY { get; set; }

    public override bool Equals(object obj) => Equals(obj as LocationInfo);
    public override int GetHashCode() => (X, Y, MapId).GetHashCode();

    public bool Equals(LocationInfo locationInfo)
    {
        if (locationInfo == null) return false;
        if (ReferenceEquals(locationInfo, this)) return true;
        if (GetType() != locationInfo.GetType()) return false;
        return X == locationInfo.X && Y == locationInfo.Y && MapId.Equals(locationInfo.MapId);
    }

    public static bool operator ==(LocationInfo left, LocationInfo right)
    {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(LocationInfo left, LocationInfo right) => !(left == right);
}