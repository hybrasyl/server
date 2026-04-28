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

using System.Collections.Generic;
using Hybrasyl.Internals;

namespace Hybrasyl.Objects;

/// <summary>
///     One physical door on a map — the authoritative owner of Closed state for a connected set of 1–4 panel
///     tiles that make up a single retail door. Instantiated at map load once <see cref="Hybrasyl.Objects.MapObject.LoadMapFile" />
///     finds a <see cref="DoorDefinition" />'s full panel set placed adjacently on the map.
///     <br /><br />
///     Phase 2 introduces the type and builds groups at load time. Per-tile <see cref="Door.Closed" /> state is
///     preserved for backward-compatibility with the existing <c>ToggleDoor</c> logic, which continues to mutate
///     each panel's <c>Closed</c> field independently via the adjacent-panel scan in <c>ToggleDoors</c>. Phase 3
///     unifies the state on <see cref="Closed" /> here and removes the adjacency scan in favour of a single
///     <c>Toggle</c> method that emits <c>SendDoorUpdate</c> for every toggle-able panel of the group atomically.
/// </summary>
public sealed class DoorGroup
{
    public DoorDefinition Definition { get; }
    public IReadOnlyList<DoorPanel> Panels { get; }
    public bool Closed { get; set; }

    /// <summary>
    ///     Mirrors <see cref="Hybrasyl.Game.IsDoorCollision" /> on the group's canonical closed sprite — true when
    ///     toggling this group should add/remove entries from the map's collision set.
    /// </summary>
    public bool UpdateCollision { get; }

    public DoorGroup(DoorDefinition definition, IReadOnlyList<DoorPanel> panels, bool closed, bool updateCollision)
    {
        Definition = definition;
        Panels = panels;
        Closed = closed;
        UpdateCollision = updateCollision;
    }

    public DoorPanel CenterPanel => Panels[Definition.CenterPanelIndex];

    public bool ContainsCoord(byte x, byte y)
    {
        foreach (var panel in Panels)
            if (panel.X == x && panel.Y == y)
                return true;

        return false;
    }
}

/// <summary>
///     One panel tile of a <see cref="DoorGroup" />: its map coordinates and its index within the group's
///     <see cref="DoorDefinition.ClosedSprites" />/<see cref="DoorDefinition.OpenSprites" /> arrays.
/// </summary>
public readonly record struct DoorPanel(byte X, byte Y, int PanelIndex)
{
    public (byte X, byte Y) Coords => (X, Y);
}

/// <summary>
///     Outcome of a foreground-grid sweep for door groups: the validated <see cref="DoorGroup" />
///     instances ready to insert into a map plus any per-anchor warnings for malformed/partial doors.
/// </summary>
public sealed record DoorScanResult(IReadOnlyList<DoorGroup> Groups, IReadOnlyList<string> Warnings);
