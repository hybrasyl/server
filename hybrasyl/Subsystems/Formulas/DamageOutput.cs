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

using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Formulas;

public class DamageOutput
{
    public double Amount { get; set; }
    public DamageType Type { get; set; }
    public DamageFlags Flags { get; set; }
    public ElementType Element { get; set; }

    public override string ToString() => $"{Element}, {Amount}: {Type} {Flags}";
}