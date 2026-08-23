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
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class MapLoad(HybrasylFixture fixture)
{
    public HybrasylFixture Fixture { get; set; } = fixture;

    [Fact]
    public void MessageboardSignpostWithoutBoardKey_IsSkippedNotFatal()
    {
        // Regression: a messageboard sign missing its board key made the Signpost ctor
        // throw out of World.LoadData, killing server startup. Data errors must degrade
        // to a logged skip.
        var map = new Map
        {
            Id = 65533, // no lod65533.map on disk; LoadMapFile no-ops
            X = 5,
            Y = 5,
            Name = "BadSignMap"
        };
        map.Signs.Add(new MapSign { X = 1, Y = 1, Message = "keyless board", Type = BoardType.Messageboard });
        map.Signs.Add(new MapSign { X = 2, Y = 2, Message = "plain sign", Type = BoardType.Sign });

        var mapObj = new MapObject(map, Game.World);

        // The bad messageboard is skipped; the loop continues and loads the valid sign.
        var post = Assert.Single(mapObj.Objects.OfType<Signpost>());
        Assert.False(post.IsMessageboard);
        Assert.Equal("plain sign", post.Message);
    }
}
