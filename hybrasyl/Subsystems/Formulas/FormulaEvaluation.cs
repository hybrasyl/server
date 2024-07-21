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

using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using Creature = Hybrasyl.Objects.Creature;

namespace Hybrasyl.Subsystems.Formulas;

public class FormulaEvaluation
{
    public Creature Source { get; set; } = null;
    public Castable Castable { get; set; } = null;
    public Creature Target { get; set; } = null;
    public MapObject Map { get; set; } = null;
    public Monster Spawn { get; set; } = null;
    public User User { get; set; } = null;
    public double? Damage { get; set; } = null;
    public Spawn XmlSpawn { get; set; } = null;
    public SpawnGroup SpawnGroup { get; set; } = null;
    public ItemObject ItemObject { get; set; } = null;
}