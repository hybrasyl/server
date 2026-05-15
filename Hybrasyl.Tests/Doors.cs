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

using Hybrasyl.Internals;
using Hybrasyl.Objects;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Doors
{
    private static HybrasylFixture Fixture;

    public Doors(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    // --- DoorDefinition tests ---

    [Fact]
    public void SingleTileDoor_PanelCount_IsOne()
    {
        var def = new DoorDefinition(
            ClosedSprites: [12484],
            OpenSprites: [12485],
            Axis: DoorAxis.NorthSouth,
            OnlyCenterChanges: false,
            HasClosedVersion: true);

        Assert.Equal(1, def.PanelCount);
        Assert.Equal(0, def.CenterPanelIndex);
        Assert.True(def.IsPanelToggling(0));
    }

    [Fact]
    public void TwoTileDoor_BothPanelsToggle()
    {
        var def = new DoorDefinition(
            ClosedSprites: [1993, 1994],
            OpenSprites: [1996, 1997],
            Axis: DoorAxis.EastWest,
            OnlyCenterChanges: false,
            HasClosedVersion: true);

        Assert.Equal(2, def.PanelCount);
        Assert.True(def.IsLeftRight);
        Assert.True(def.IsPanelToggling(0));
        Assert.True(def.IsPanelToggling(1));
    }

    [Fact]
    public void ThreeTileCenterOnly_OnlyCenterToggles()
    {
        var def = new DoorDefinition(
            ClosedSprites: [100, 101, 102],
            OpenSprites: [200, 201, 202],
            Axis: DoorAxis.NorthSouth,
            OnlyCenterChanges: true,
            HasClosedVersion: true);

        Assert.Equal(3, def.PanelCount);
        Assert.Equal(1, def.CenterPanelIndex);
        Assert.False(def.IsPanelToggling(0));
        Assert.True(def.IsPanelToggling(1));
        Assert.False(def.IsPanelToggling(2));
    }

    [Fact]
    public void ThreeTileAllChange_AllPanelsToggle()
    {
        var def = new DoorDefinition(
            ClosedSprites: [2163, 2164, 2165],
            OpenSprites: [2167, 2168, 2169],
            Axis: DoorAxis.NorthSouth,
            OnlyCenterChanges: false,
            HasClosedVersion: true);

        Assert.True(def.IsPanelToggling(0));
        Assert.True(def.IsPanelToggling(1));
        Assert.True(def.IsPanelToggling(2));
    }

    [Fact]
    public void Archway_NoPanelsToggle()
    {
        var def = new DoorDefinition(
            ClosedSprites: [],
            OpenSprites: [500, 501],
            Axis: DoorAxis.EastWest,
            OnlyCenterChanges: false,
            HasClosedVersion: false);

        Assert.Equal(2, def.PanelCount);
        Assert.False(def.IsPanelToggling(0));
        Assert.False(def.IsPanelToggling(1));
    }

    // --- DoorGroup tests ---

    [Fact]
    public void DoorGroup_ContainsCoord_FindsPanels()
    {
        var def = new DoorDefinition([10, 11], [20, 21], DoorAxis.EastWest, false, true);
        var panels = new DoorPanel[] { new(5, 10, 0), new(6, 10, 1) };
        var group = new DoorGroup(def, panels, closed: true, updateCollision: false);

        Assert.True(group.ContainsCoord(5, 10));
        Assert.True(group.ContainsCoord(6, 10));
        Assert.False(group.ContainsCoord(7, 10));
        Assert.False(group.ContainsCoord(5, 11));
    }

    [Fact]
    public void DoorGroup_CenterPanel_ReturnsCorrectPanel()
    {
        var def = new DoorDefinition([10, 11, 12], [20, 21, 22], DoorAxis.NorthSouth, true, true);
        var panels = new DoorPanel[] { new(5, 10, 0), new(5, 11, 1), new(5, 12, 2) };
        var group = new DoorGroup(def, panels, closed: true, updateCollision: false);

        Assert.Equal(panels[1], group.CenterPanel);
    }

    // --- Door tests ---

    [Fact]
    public void Door_Closed_DelegatesToGroup()
    {
        var def = new DoorDefinition([10], [20], DoorAxis.NorthSouth, false, true);
        var panels = new DoorPanel[] { new(5, 10, 0) };
        var group = new DoorGroup(def, panels, closed: true, updateCollision: false);
        var door = new Door(5, 10, group, 0);

        Assert.True(door.Closed);
        group.Closed = false;
        Assert.False(door.Closed);
    }

    [Fact]
    public void Door_Toggles_MatchesDefinition()
    {
        var def = new DoorDefinition([10, 11, 12], [20, 21, 22], DoorAxis.NorthSouth, true, true);
        var panels = new DoorPanel[] { new(5, 10, 0), new(5, 11, 1), new(5, 12, 2) };
        var group = new DoorGroup(def, panels, closed: true, updateCollision: false);

        var sideDoor = new Door(5, 10, group, 0);
        var centerDoor = new Door(5, 11, group, 1);

        Assert.False(sideDoor.Toggles);
        Assert.True(centerDoor.Toggles);
        Assert.False(sideDoor.IsCenter);
        Assert.True(centerDoor.IsCenter);
    }

    // --- Sprites catalog tests ---

    [Fact]
    public void SpriteInfo_AllDefinitions_HaveReverseLookup()
    {
        foreach (var def in Sprites.Definitions)
        {
            if (def.HasClosedVersion)
                foreach (var sprite in def.ClosedSprites)
                    Assert.True(Sprites.SpriteInfo.ContainsKey(sprite),
                        $"Closed sprite {sprite} missing from SpriteInfo");

            foreach (var sprite in def.OpenSprites)
                Assert.True(Sprites.SpriteInfo.ContainsKey(sprite),
                    $"Open sprite {sprite} missing from SpriteInfo");
        }
    }

    [Fact]
    public void SpriteInfo_ClosedSprite_MapsBackToDefinition()
    {
        foreach (var def in Sprites.Definitions.Where(d => d.HasClosedVersion))
        {
            for (var i = 0; i < def.ClosedSprites.Length; i++)
            {
                var info = Sprites.SpriteInfo[def.ClosedSprites[i]];
                Assert.Same(def, info.Definition);
                Assert.Equal(i, info.PanelIndex);
                Assert.False(info.IsOpenState);
            }
        }
    }

    [Fact]
    public void SpriteInfo_OpenSprite_MapsBackToDefinition()
    {
        foreach (var def in Sprites.Definitions)
        {
            for (var i = 0; i < def.OpenSprites.Length; i++)
            {
                var info = Sprites.SpriteInfo[def.OpenSprites[i]];
                Assert.Same(def, info.Definition);
                Assert.Equal(i, info.PanelIndex);
                Assert.True(info.IsOpenState);
            }
        }
    }

    [Fact]
    public void SpriteInfo_NoSpriteId_AppearsInMultipleDefinitions()
    {
        var seen = new System.Collections.Generic.Dictionary<ushort, DoorDefinition>();
        foreach (var def in Sprites.Definitions)
        {
            var allSprites = def.HasClosedVersion
                ? def.ClosedSprites.Concat(def.OpenSprites)
                : def.OpenSprites;

            foreach (var sprite in allSprites)
            {
                if (seen.TryGetValue(sprite, out var existing))
                    Assert.Fail(
                        $"Sprite {sprite} appears in multiple definitions: " +
                        $"[{string.Join(",", existing.ClosedSprites)}] and [{string.Join(",", def.ClosedSprites)}]");
                seen[sprite] = def;
            }
        }
    }

    // --- Map-level toggle tests ---

    [Fact]
    public void ToggleDoors_FlipsGroupState()
    {
        var def = new DoorDefinition([10, 11], [20, 21], DoorAxis.EastWest, false, true);
        var panels = new DoorPanel[] { new(5, 10, 0), new(6, 10, 1) };
        var group = new DoorGroup(def, panels, closed: true, updateCollision: false);

        Assert.True(group.Closed);
        group.Closed = !group.Closed;
        Assert.False(group.Closed);

        // All doors in the group reflect the new state
        var door0 = new Door(5, 10, group, 0);
        var door1 = new Door(6, 10, group, 1);
        Assert.False(door0.Closed);
        Assert.False(door1.Closed);
    }

    [Fact]
    public void ToggleDoors_NonDoorTile_IsNoOp()
    {
        // ToggleDoors on a tile without a door should not throw
        Fixture.Map.ToggleDoors(0, 0);
    }

    // --- ScanForDoorGroups: map-load scanner tests ---
    //
    // Synthetic grids exercise the scanner in isolation. The retail door 18610 (N/S 2-tile,
    // closed [18610,18611] / open [18612,18613]) is the canonical "reversed-order" case that
    // produced the Abel sprite-not-found warnings before the orientation-agnostic rewrite.

    private const int W = 20;
    private const int H = 20;

    private static (ushort[,] lfgs, ushort[,] rfgs) Grids() =>
        (new ushort[W, H], new ushort[W, H]);

    [Fact]
    public void Scan_ForwardOrderedNSTwoTile_BuildsGroup()
    {
        var (lfgs, rfgs) = Grids();
        // Panel 0 sprite at NORTH (smaller y), panel 1 at SOUTH (larger y).
        rfgs[5, 10] = 18610;
        rfgs[5, 11] = 18611;

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Warnings);
        var g = Assert.Single(result.Groups);
        Assert.Equal(2, g.Panels.Count);
        Assert.Equal(((byte)5, (byte)10), (g.Panels[0].X, g.Panels[0].Y));
        Assert.Equal(0, g.Panels[0].PanelIndex);
        Assert.Equal(((byte)5, (byte)11), (g.Panels[1].X, g.Panels[1].Y));
        Assert.Equal(1, g.Panels[1].PanelIndex);
        Assert.True(g.Closed);
    }

    [Fact]
    public void Scan_ReversedOrderedNSTwoTile_BuildsGroup()
    {
        var (lfgs, rfgs) = Grids();
        // The Abel @57,18-19 case: panel 1 sprite at NORTH, panel 0 at SOUTH.
        rfgs[5, 10] = 18611;
        rfgs[5, 11] = 18610;

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Warnings);
        var g = Assert.Single(result.Groups);
        Assert.Equal(2, g.Panels.Count);
        Assert.Equal(((byte)5, (byte)11), (g.Panels[0].X, g.Panels[0].Y));
        Assert.Equal(0, g.Panels[0].PanelIndex);
        Assert.Equal(((byte)5, (byte)10), (g.Panels[1].X, g.Panels[1].Y));
        Assert.Equal(1, g.Panels[1].PanelIndex);
        Assert.True(g.Closed);
    }

    [Fact]
    public void Scan_ForwardOrderedEWTwoTile_BuildsGroup()
    {
        var (lfgs, rfgs) = Grids();
        // 1993,1994 / 1996,1997 — E/W 2-tile.
        lfgs[10, 5] = 1993;
        lfgs[11, 5] = 1994;

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Warnings);
        var g = Assert.Single(result.Groups);
        Assert.Equal(((byte)10, (byte)5), (g.Panels[0].X, g.Panels[0].Y));
        Assert.Equal(((byte)11, (byte)5), (g.Panels[1].X, g.Panels[1].Y));
    }

    [Fact]
    public void Scan_ReversedOrderedEWTwoTile_BuildsGroup()
    {
        var (lfgs, rfgs) = Grids();
        lfgs[10, 5] = 1994;
        lfgs[11, 5] = 1993;

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Warnings);
        var g = Assert.Single(result.Groups);
        Assert.Equal(((byte)11, (byte)5), (g.Panels[0].X, g.Panels[0].Y));
        Assert.Equal(((byte)10, (byte)5), (g.Panels[1].X, g.Panels[1].Y));
    }

    [Fact]
    public void Scan_OpenStateGroup_HasClosedFalse()
    {
        var (lfgs, rfgs) = Grids();
        rfgs[5, 10] = 18612; // open panel 0
        rfgs[5, 11] = 18613; // open panel 1

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Warnings);
        var g = Assert.Single(result.Groups);
        Assert.False(g.Closed);
    }

    [Fact]
    public void Scan_ThreeTileCenterOnlyForward_BuildsGroupWithArbitrarySidePanels()
    {
        // 3018,3019,3020 / 3024,3025,3026 — N/S center-only. Anchor must be the center
        // panel; side sprites are jamb art and aren't validated.
        var (lfgs, rfgs) = Grids();
        rfgs[5, 10] = 9999;  // arbitrary jamb sprite
        rfgs[5, 11] = 3019;  // center, closed
        rfgs[5, 12] = 9999;

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Warnings);
        var g = Assert.Single(result.Groups);
        Assert.Equal(3, g.Panels.Count);
        Assert.Equal(((byte)5, (byte)11), (g.CenterPanel.X, g.CenterPanel.Y));
        // Side panels are placed at the immediately-adjacent tiles regardless of sprite.
        Assert.Contains(g.Panels, p => p.X == 5 && p.Y == 10);
        Assert.Contains(g.Panels, p => p.X == 5 && p.Y == 12);
        Assert.True(g.Closed);
    }

    [Fact]
    public void Scan_ThreeTileAllChangeReversed_CollectsByPanelIndex()
    {
        // 2163,2164,2165 / 2167,2168,2169 — E/W 3-tile all-change. Place sprites in
        // reverse order along the axis to confirm the scanner keys off sprite identity.
        var (lfgs, rfgs) = Grids();
        lfgs[12, 5] = 2163; // panel 0 at EAST
        lfgs[11, 5] = 2164; // panel 1
        lfgs[10, 5] = 2165; // panel 2 at WEST

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Warnings);
        var g = Assert.Single(result.Groups);
        Assert.Equal(((byte)12, (byte)5), (g.Panels[0].X, g.Panels[0].Y));
        Assert.Equal(((byte)11, (byte)5), (g.Panels[1].X, g.Panels[1].Y));
        Assert.Equal(((byte)10, (byte)5), (g.Panels[2].X, g.Panels[2].Y));
    }

    [Fact]
    public void Scan_MixedOpenAndClosedSprites_WarnsNoGroup()
    {
        var (lfgs, rfgs) = Grids();
        rfgs[5, 10] = 18610; // closed panel 0
        rfgs[5, 11] = 18613; // OPEN panel 1 — inconsistent

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Groups);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("mixed open/closed"));
    }

    [Fact]
    public void Scan_PartialDoor_WarnsNoGroup()
    {
        var (lfgs, rfgs) = Grids();
        rfgs[5, 10] = 18610; // panel 0 — second panel missing entirely

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Groups);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("missing panel"));
    }

    [Fact]
    public void Scan_LoneTogglingSpriteAtMapEdge_DoesNotCrash()
    {
        var (lfgs, rfgs) = Grids();
        rfgs[0, 0] = 18610; // anchor on a corner with no neighbors

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Groups);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Scan_TwoSeparateDoorsOfSameDef_ProducesTwoGroups()
    {
        var (lfgs, rfgs) = Grids();
        // Two distinct 18610 doors on the same column, with a gap.
        rfgs[5, 10] = 18610;
        rfgs[5, 11] = 18611;
        rfgs[5, 14] = 18610;
        rfgs[5, 15] = 18611;

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Groups.Count);
    }

    [Fact]
    public void Scan_DoorVisitedFromBothPanels_DedupesViaConsumedTiles()
    {
        // Anchor is whichever toggling tile the row-major scan hits first; consumed-tile
        // dedup must prevent the sibling tile from re-triggering a second group attempt.
        var (lfgs, rfgs) = Grids();
        rfgs[5, 10] = 18611;
        rfgs[5, 11] = 18610;

        var result = MapObject.ScanForDoorGroups(lfgs, rfgs, W, H);

        Assert.Empty(result.Warnings);
        Assert.Single(result.Groups);
    }
}
