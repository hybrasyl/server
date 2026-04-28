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

using Hybrasyl.Subsystems.Messaging.ChatCommands;
using Hybrasyl.Xml.Objects;
using System.Reflection;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class ChatCommands
{
    private static HybrasylFixture Fixture;

    public ChatCommands(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    //Internal command classes are dispatched via reflection by ChatCommandHandler at runtime; the
    //test bypasses the handler's arg-parsing/auth glue and exercises the command's static Run
    //method directly, which is where parsing and state mutation actually live.
    private static ChatCommandResult Invoke(string commandClass, params string[] args)
    {
        var type = typeof(ChatCommand).Assembly.GetType(
            $"Hybrasyl.Subsystems.Messaging.ChatCommands.{commandClass}");
        Assert.NotNull(type);
        var run = type!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(run);
        return (ChatCommandResult)run!.Invoke(null, new object[] { Fixture.TestUser, args })!;
    }

    [Fact]
    public void GenderCommand_M_SetsMale()
    {
        Fixture.TestUser.Gender = Gender.Female;

        var result = Invoke("GenderCommand", "m");

        Assert.True(result.Success);
        Assert.Equal(Gender.Male, Fixture.TestUser.Gender);
    }

    [Fact]
    public void GenderCommand_FemaleAlias_SetsFemale()
    {
        Fixture.TestUser.Gender = Gender.Male;

        var result = Invoke("GenderCommand", "female");

        Assert.True(result.Success);
        Assert.Equal(Gender.Female, Fixture.TestUser.Gender);
    }

    [Fact]
    public void GenderCommand_CaseInsensitive()
    {
        Fixture.TestUser.Gender = Gender.Female;

        var result = Invoke("GenderCommand", "MALE");

        Assert.True(result.Success);
        Assert.Equal(Gender.Male, Fixture.TestUser.Gender);
    }

    [Fact]
    public void GenderCommand_RejectsNeutral()
    {
        Fixture.TestUser.Gender = Gender.Male;

        var result = Invoke("GenderCommand", "neutral");

        Assert.False(result.Success);
        Assert.Equal(Gender.Male, Fixture.TestUser.Gender);
    }

    [Fact]
    public void GenderCommand_RejectsGarbage()
    {
        Fixture.TestUser.Gender = Gender.Male;

        var result = Invoke("GenderCommand", "potato");

        Assert.False(result.Success);
        Assert.Equal(Gender.Male, Fixture.TestUser.Gender);
    }
}
