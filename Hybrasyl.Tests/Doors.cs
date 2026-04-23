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
}
