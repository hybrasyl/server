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

using Hybrasyl.Subsystems.Scripting;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Cookies
{
    private static HybrasylFixture Fixture;

    public Cookies(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private string CookieInDefaultNamespace(string? suffix = null)
    {
        var name = suffix == null ? "test-cidn" : $"test-cidn-{suffix}";
        Fixture.TestUser.SetCookie(name, "1234");
        Assert.Equal("1234", Fixture.TestUser.GetCookie(name));
        Assert.Equal("1234", Fixture.TestUser.GetCookie("default", name));
        Assert.True(Fixture.TestUser.HasCookie(name));
        Assert.True(Fixture.TestUser.HasCookie("default", name));
        return name;
    }

    private string CookieWithNamespace(string? suffix = null)
    {
        var name = suffix == null ? "test-cwn" : $"test-cwn-{suffix}";
        Fixture.TestUser.SetCookie("test", name, "1234");
        Assert.Equal(null, Fixture.TestUser.GetCookie(name));
        Assert.Equal("1234", Fixture.TestUser.GetCookie("test", name));
        return name;
    }

    [Fact]
    public void SetCookieWithNamespace() => CookieWithNamespace("set");

    [Fact]
    public void SetCookieInDefaultNamespace() => CookieInDefaultNamespace("set");

    [Fact]
    public void DeleteCookieWithNamespace()
    {
        var cookieName = CookieWithNamespace("delete");
        Assert.False(Fixture.TestUser.DeleteCookie(cookieName));
        Assert.True(Fixture.TestUser.DeleteCookie("test", cookieName));
        Assert.False(Fixture.TestUser.HasCookie("test-cwn"));
        Assert.False(Fixture.TestUser.HasCookie("test", cookieName));
    }

    [Fact]
    public void DeleteCookieInDefaultNamespace()
    {
        var cookieName = CookieInDefaultNamespace("delete-default");
        Assert.False(Fixture.TestUser.DeleteCookie("test", cookieName));
        Assert.True(Fixture.TestUser.DeleteCookie(cookieName));
        Assert.False(Fixture.TestUser.HasCookie(cookieName));
        Assert.False(Fixture.TestUser.HasCookie("default", cookieName));
    }

    public string SessionCookieInDefaultNamespace(string? suffix = null)
    {
        var name = suffix == null ? "test-scidn" : $"test-scidn-{suffix}";

        Fixture.TestUser.SetSessionCookie(name, "1234");
        Assert.Equal("1234", Fixture.TestUser.GetSessionCookie(name));
        Assert.Equal("1234", Fixture.TestUser.GetSessionCookie("default", name));
        Assert.True(Fixture.TestUser.HasSessionCookie(name));
        Assert.True(Fixture.TestUser.HasSessionCookie("default", name));
        return name;
    }

    public string SessionCookieWithNamespace(string? suffix = null)
    {
        var name = suffix == null ? "test-scwn" : $"test-scwn-{suffix}";

        Fixture.TestUser.SetSessionCookie("test", name, "1234");
        Assert.Equal("1234", Fixture.TestUser.GetSessionCookie("test", name));
        Assert.Equal(null, Fixture.TestUser.GetSessionCookie($"test:{name}"));
        Assert.Equal(null, Fixture.TestUser.GetSessionCookie(name));
        return name;
    }

    [Fact]
    public void SetSessionCookieInDefaultNamespace() => SessionCookieInDefaultNamespace("set");

    [Fact]
    public void SetSessionCookieWithNamespace() => SessionCookieWithNamespace("set");

    [Fact]
    public void DeleteSessionCookieWithNamespace()
    {
        var cookieName = SessionCookieWithNamespace("delete");
        Assert.False(Fixture.TestUser.DeleteSessionCookie(cookieName));
        Assert.True(Fixture.TestUser.DeleteSessionCookie("test", cookieName));
        Assert.False(Fixture.TestUser.HasSessionCookie(cookieName));
        Assert.False(Fixture.TestUser.HasSessionCookie("test", cookieName));
    }

    [Fact]
    public void DeleteSessionCookieInDefaultNamespace()
    {
        var cookieName = SessionCookieInDefaultNamespace("delete-ns");
        Assert.False(Fixture.TestUser.DeleteSessionCookie("shouldnotwork", cookieName));
        Assert.True(Fixture.TestUser.DeleteSessionCookie(cookieName));
        Assert.False(Fixture.TestUser.HasSessionCookie(cookieName));
        Assert.False(Fixture.TestUser.HasSessionCookie("default", cookieName));
    }

    [Fact]
    public void ScriptCookieInDefaultNamespace()
    {
        var scriptObj = new HybrasylUser(Fixture.TestUser);
        scriptObj.SetCookie("scidn", 13.37);
        Assert.True(scriptObj.HasCookie("scidn"));
        Assert.Equal(scriptObj.GetCookie("scidn"), "13.37");
        Assert.True(scriptObj.DeleteCookie("scidn"));
        Assert.False(scriptObj.HasCookie("scidn"));
    }

    [Fact]
    public void ScriptCookieWithNamespace()
    {
        var scriptObj = new HybrasylUser(Fixture.TestUser);
        scriptObj.SetCookie("scwn", "scwn", 13.37);
        Assert.True(scriptObj.HasCookie("scwn", "scwn"));
        Assert.Equal(scriptObj.GetCookie("scwn", "scwn"), "13.37");
        Assert.True(scriptObj.DeleteCookie("scwn", "scwn"));
        Assert.False(scriptObj.HasCookie("scwn", "scwn"));
    }

    [Fact]
    public void ScriptSessionCookieInDefaultNamespace()
    {
        var scriptObj = new HybrasylUser(Fixture.TestUser);
        scriptObj.SetSessionCookie("sscidn", 13.37);
        Assert.True(scriptObj.HasSessionCookie("sscidn"));
        Assert.Equal(scriptObj.GetSessionCookie("sscidn"), "13.37");
        Assert.True(scriptObj.DeleteSessionCookie("sscidn"));
        Assert.False(scriptObj.HasSessionCookie("sscidn"));
    }

    [Fact]
    public void ScriptSessionCookieWithNamespace()
    {
        var scriptObj = new HybrasylUser(Fixture.TestUser);
        scriptObj.SetSessionCookie("sscwn", "sscwn", 13.37);
        Assert.True(scriptObj.HasSessionCookie("sscwn", "sscwn"));
        Assert.Equal(scriptObj.GetSessionCookie("sscwn", "sscwn"), "13.37");
        Assert.True(scriptObj.DeleteSessionCookie("sscwn", "sscwn"));
        Assert.False(scriptObj.HasSessionCookie("sscwn", "sscwn"));
    }

}