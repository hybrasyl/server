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

namespace Hybrasyl.Objects;

/// <summary>
///     A single panel tile of a retail door. Each physical door on a map is represented by one
///     <see cref="DoorGroup" /> owning a shared Closed state plus one <see cref="Door" /> per panel tile
///     (1 for single-tile doors, up to 4 for four-tile doors).
///     <br /><br />
///     In Phase 2, per-tile <see cref="Closed" /> is preserved as a settable field so the existing
///     <c>ToggleDoor</c>/<c>ToggleDoors</c> logic continues to work unchanged. Phase 3 unifies state on
///     <see cref="DoorGroup.Closed" />.
/// </summary>
public class Door : VisibleObject
{
    public Door(byte x, byte y, DoorGroup group, int panelIndex)
    {
        X = x;
        Y = y;
        Group = group;
        PanelIndex = panelIndex;
        Closed = group.Closed;
    }

    public DoorGroup Group { get; }
    public int PanelIndex { get; }

    /// <summary>
    ///     Panel-local Closed state. Phase 2 keeps this settable for backward-compatibility with the
    ///     <c>ToggleDoor</c> per-tile mutation; Phase 3 makes this a thin delegate over
    ///     <see cref="DoorGroup.Closed" />.
    /// </summary>
    public bool Closed { get; set; }

    public bool IsLeftRight => Group.Definition.IsLeftRight;
    public bool UpdateCollision => Group.UpdateCollision;

    /// <summary>True when this panel is the center of a 3-tile "only center changes" door.</summary>
    public bool IsCenter => PanelIndex == Group.Definition.CenterPanelIndex;

    /// <summary>True when this panel actually toggles sprite on open/close — false for side panels of center-only doors.</summary>
    public bool Toggles => Group.Definition.IsPanelToggling(PanelIndex);

    public override void OnClick(User invoker)
    {
        invoker.Map.ToggleDoors(X, Y);
    }

    public override void AoiEntry(VisibleObject obj)
    {
        ShowTo(obj);
    }

    public override void ShowTo(IVisible obj)
    {
        if (obj is not User user) return;
        user.SendDoorUpdate(X, Y, Closed,
            IsLeftRight);
    }
}
