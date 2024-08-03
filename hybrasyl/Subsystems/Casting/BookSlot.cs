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
using System;

namespace Hybrasyl.Casting;

public class BookSlot
{
    public Castable Castable { get; set; }
    public uint UseCount { get; set; }
    public uint MasteryLevel { get; set; }
    public DateTime LastCast { get; set; }
    public int ClientSlot { get; set; }

    public bool OnCooldown => Castable.Cooldown > 0 &&
                              (DateTime.Now - LastCast).TotalSeconds < Castable.Cooldown;

    public bool HasBeenUsed => LastCast != default;
    public double SecondsSinceLastUse => (DateTime.Now - LastCast).TotalSeconds;

    public void TriggerCooldown()
    {
        LastCast = DateTime.Now;
    }

    public void ClearCooldown()
    {
        LastCast = DateTime.MinValue;
    }
}