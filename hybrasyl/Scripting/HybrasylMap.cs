/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program.If not, see<http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */


using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting;

[MoonSharpUserData]
public class HybrasylMap
{
    internal Map Map { get; set; }

    public HybrasylMap(Map map)
    {
        Map = map;
    }

    public string Name => Map.Name;

    public bool CreateItem(string name, int x = -1, int y = -1)
    {
        return false;
    }

    public void DropItem(HybrasylWorldObject obj, int x, int y)
    {
        if (obj.Obj is ItemObject)
            Map.Insert(obj.Obj as ItemObject, (byte) x, (byte) y);
    }

    public void SpawnMonster(string creatureName, HybrasylSpawn spawn, int x, int y)
    {
        //if (Game.World.WorldData.TryGetValue(creatureName, out Xml.Creature creature))
        //{
        //    var baseMob = new Monster(creature, spawn.Spawn, Map.Id);
        //    baseMob.X = (byte)x;
        //    baseMob.Y = (byte)y;
        //    World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MonolithSpawn, baseMob, Map));
        //}
    }
}