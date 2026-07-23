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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Networking;
using Hybrasyl.Subsystems.Scripting;
using Hybrasyl.Xml.Objects;
using System;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class ControlMessages
{
    private static HybrasylFixture Fixture = null!;

    public ControlMessages(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void MessageArgumentAccessors()
    {
        var msg = new HybrasylControlMessage(ControlOpcode.ProcessProc, "abc", null, Guid.Empty);

        Assert.Equal("abc", msg.GetArgument<string>(0));
        Assert.Null(msg.GetOptionalArgument<Castable>(1));
        Assert.Equal(Guid.Empty, msg.GetArgument<Guid>(2));

        var ex = Assert.Throws<InvalidOperationException>(() => msg.GetArgument<Castable>(1));
        Assert.Contains("argument 1", ex.Message);
        Assert.Contains("Castable", ex.Message);
    }

    [Fact]
    public void ItemProcWithNullCastableAndFailingScriptDoesNotThrow()
    {
        // Regression: item procs enqueue a null castable slot (ItemObject.Invoke). When the proc
        // script exists but returns non-Success, the error-log path used to deref castable.Name
        // and NRE. A Disabled script returns ScriptResult.Disabled, exercising exactly that path.
        var script = new Script("procfail_test.lua", Game.World.ScriptProcessor) { Disabled = true };
        Game.World.ScriptProcessor.RegisterScript(script, run: false);

        var source = Game.World.WorldState.GetWorldObject<Hybrasyl.Objects.Creature>(Fixture.TestUser.Guid);
        Assert.NotNull(source); // otherwise the handler returns before reaching the script path

        var proc = new Proc { Script = "procfail_test.lua" };
        var msg = new HybrasylControlMessage(ControlOpcode.ProcessProc, proc, null,
            Fixture.TestUser.Guid, Fixture.TestUser.Guid);

        var exception = Record.Exception(() =>
            Game.World.ControlMessageHandlers[ControlOpcode.ProcessProc].Invoke(msg));
        Assert.Null(exception);
    }
}
