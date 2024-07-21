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

using System.Collections.Generic;

namespace Hybrasyl.Subsystems.Formulas;

public class CastCost
{
    public uint Hp { get; set; }
    public uint Mp { get; set; }
    public uint Gold { get; set; }
    public List<(byte Quantity, string Item)> Items { get; set; } = new();
    public bool IsNoCost => Hp == 0 && Mp == 0 && Gold == 0 && Items.Count == 0;
}