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

using System.Linq;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Subsystems.Scripting;

[MoonSharpUserData]
public class HybrasylMap
{
    public HybrasylMap(MapObject map)
    {
        Map = map;
    }

    public ushort Id => Map.Id;

    internal MapObject Map { get; set; }

    public string Name => Map.Name;

    public bool CreateItem(string name, int x = -1, int y = -1) => false;

    public void DropItem(HybrasylWorldObject obj, int x, int y)
    {
        if (obj.Obj is ItemObject item)
            Map.Insert(item, (byte) x, (byte) y);
    }

    /// <summary>
    ///     Check to see if a creature (player or monster) is present at the given coordinates.
    /// </summary>
    /// <param name="x">X coordinate on map</param>
    /// <param name="y">Y coordinate on map</param>
    /// <returns></returns>
    public bool IsCreatureAt(byte x, byte y) => Map.IsCreatureAt(x, y);

    /// <summary>
    ///     Get a player at the given coordinates. Returns null if no player exists.
    /// </summary>
    /// <param name="x">X coordinate on map</param>
    /// <param name="y">Y coordinate on map</param>
    /// <returns></returns>
    public HybrasylUser? GetPlayerAt(byte x, byte y)
    {
        return Map.GetCreatures(x, y).FirstOrDefault(predicate: u => u is User) is User user
            ? new HybrasylUser(user)
            : null;
    }

    /// <summary>
    ///     Get a monster at the given coordinates. Return null if no monster exists.
    /// </summary>
    /// <param name="x">X coordinate on map</param>
    /// <param name="y">Y coordinate on map</param>
    /// <returns></returns>
    public HybrasylMonster? GetMonsterAt(byte x, byte y)
    {
        return Map.GetCreatures(x, y).FirstOrDefault(predicate: m => m is Monster) is Monster monster
            ? new HybrasylMonster(monster)
            : null;
    }
}