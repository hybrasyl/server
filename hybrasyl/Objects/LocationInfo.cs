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

using Hybrasyl.Xml;
using Newtonsoft.Json;

namespace Hybrasyl.Objects;

[JsonObject(MemberSerialization.OptIn)]
public class LocationInfo
{
    private Map _map { get; set; }
    private Map _deathmap { get; set; }

    public Map Map
    {
        get => _map;
        set
        {
            _map = value;
            if (value != null)
                _mapId = Map.Id;
        }
    }

    public Map DeathMap
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

    [JsonProperty]
    public ushort MapId
    {
        get
        {
            if (Map != null) return Map.Id;
            return _mapId;
        }
        set
        {
            if (Game.World.WorldData.TryGetValue(value, out Map map))
                Map = map;
            _mapId = value;
        }
    }

    private ushort _deathmapId { get; set; }

    [JsonProperty]
    public ushort DeathMapId
    {
        get
        {
            if (DeathMap != null) return DeathMap.Id;
            return _deathmapId;
        }
        set
        {
            if (Game.World.WorldData.TryGetValue(value, out Map map))
                DeathMap = map;
            _deathmapId = value;
        }
    }

    [JsonProperty] public Direction Direction { get; set; }

    [JsonProperty] public byte X { get; set; }

    [JsonProperty] public byte Y { get; set; }

    [JsonProperty] public bool WorldMap { get; set; }

    [JsonProperty] public byte DeathMapX { get; set; }

    [JsonProperty] public byte DeathMapY { get; set; }
}