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

namespace Hybrasyl.Subsystems.Statuses;

/// <summary>
///     Serializable representation of statuses. Used to save a player's active statuses on logout.
/// </summary>
public class StatusSnapshot
{
    public string Name { get; set; }
    public ushort Icon { get; set; }
    public SimpleStatusEffect OnStartEffect { get; set; }
    public SimpleStatusEffect OnTickEffect { get; set; }
    public SimpleStatusEffect OnRemoveEffect { get; set; }
    public SimpleStatusEffect OnExpireEffect { get; set; }
    public double Remaining { get; set; }
    public double Tick { get; set; }
    public double Intensity { get; set; }
}