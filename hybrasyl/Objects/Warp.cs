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

using Hybrasyl.Enums;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Objects;

public class Warp
{
    public Warp(MapObject sourceMap)
    {
        SourceMap = sourceMap;
        _initializeWarp();
    }

    public Warp(MapObject sourceMap, string destinationMap, byte sourceX, byte sourceY)
    {
        SourceMap = sourceMap;
        DestinationMapName = destinationMap;
        X = sourceX;
        Y = sourceY;
        _initializeWarp();
    }

    public MapObject SourceMap { get; set; }
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
                MapObject map;
                if (SourceMap.World.WorldState.TryGetValueByIndex(DestinationMapName, out map))
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
                    SourceMap.World.WorldState.Get<MapObject>(Game.ActiveConfiguration.Constants.LagMap)
                        .Insert(target, 5, 5, false);
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