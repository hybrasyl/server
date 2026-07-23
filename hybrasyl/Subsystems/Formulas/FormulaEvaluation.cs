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
    // All inputs are optional; a given evaluation supplies only the subset a formula needs.
    public Creature? Source { get; set; }
    public Creature? Target { get; set; }
    public StatInfo? OriginalCaster { get; set; }
    public Castable? Castable { get; set; }
    public MapObject? Map { get; set; }
    public Monster? Spawn { get; set; }
    public User? User { get; set; }
    public double? Damage { get; set; }
    public Spawn? XmlSpawn { get; set; }
    public SpawnGroup? SpawnGroup { get; set; }
    public ItemObject? ItemObject { get; set; }
}