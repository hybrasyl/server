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
using MoonSharp.Interpreter;
using System;

namespace Hybrasyl.Objects;

[MoonSharpUserData]
public record CreatureSnapshot : IStateStorable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required StatInfo Stats { get; init; }
    public required string Name { get; init; }
    public Guid Parent { get; init; }
    public DateTime CreationDate { get; } = DateTime.Now;

    public bool IsPlayer => GetUserObject() != null;

    public User GetUserObject() => Game.World.WorldState.TryGetWorldObject(Parent, out User user) ? user : null;
}