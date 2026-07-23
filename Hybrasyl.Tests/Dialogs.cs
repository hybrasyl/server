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

using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Subsystems.Scripting;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Dialogs
{
    private static HybrasylFixture Fixture;

    public Dialogs(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void DialogSequence_ScriptLookup_IsCachedAfterFirstAccess()
    {
        var script = new Script("dialogcachetest.lua", Game.World.ScriptProcessor);
        Game.World.ScriptProcessor.RegisterScript(script, run: false);

        var sequence = new DialogSequence("DialogCacheTest") { ScriptName = "dialogcachetest" };

        var first = sequence.Script;
        Assert.NotNull(first);

        // Once resolved, the sequence must hold the script instance; a subsequent access
        // must not re-run the name lookup (regression: out var shadowed the cache field).
        sequence.ScriptName = "does-not-exist-anywhere";
        var second = sequence.Script;

        Assert.NotNull(second);
        Assert.Same(first, second);
    }
}
