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
using Hybrasyl.Networking;
using Hybrasyl.Networking.ClientPackets;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("hybrasyl")]
public class UserTests(HybrasylFixture fixture) : IClassFixture<HybrasylFixture>
{
    public HybrasylFixture Fixture { get; set; } = fixture;

    public static TestClient Client => new(new TestSocket())
    { EncryptionSeed = 0, EncryptionKey = "UrkcnItnI"u8.ToArray() };

    private IClient LoginToWorld(string username)
    {
        var client = Client;
        var handler = Game.Login.PacketHandlers[0x03];
        var loginPacket = new Login(username, "leethax6");
        handler(client, (ClientPacket)loginPacket);
        Assert.Equal("Welcome to Hybrasyl!", client.LastMessage);
        var worldLoginHandler = Game.World.WorldPacketHandlers[0x10];
        GlobalConnectionManifest.RegisterClient(client);
        worldLoginHandler(client.ConnectionId, (ClientPacket)
            new JoinWorld(client.EncryptionSeed, Encoding.UTF8.GetString(client.EncryptionKey), username,
                client.LastRedirect.Id));
        Assert.True(Game.World.WorldState.TryGetUser(username, out var u1));
        Assert.True(Game.World.TryGetActiveUser(username, out var u2));
        Assert.NotNull(u2);
        Assert.NotNull(u2.Map);
        return client;
    }

    private User? GetTestUser(string username) =>
        Game.World.TryGetActiveUser(username, out var user) ? user : null;

    private void LogoutOfWorld(string username)
    {
        var handler = Game.World.WorldPacketHandlers[0x0b];
        var user = GetTestUser(username);
        Assert.NotNull(user);
        Assert.NotNull(user.Map);
        var leavePacket = new LeaveWorld(1);
        handler(user, (ClientPacket)leavePacket);
        Assert.NotNull(user.Map);
        leavePacket = new LeaveWorld(0);
        handler(user, (ClientPacket)leavePacket);
        Assert.False(Game.World.TryGetActiveUser(username, out _));
    }

    private IClient CreateTestUser(string username)
    {
        var client = Client;
        var handler = Game.Login.PacketHandlers[0x02];
        Assert.NotNull(handler);
        var createAPacket = new CreateALogin(username, "leethax6", "k@erd.en");
        handler(client, (ClientPacket)createAPacket);
        var createBPacket = new CreateBLogin(0, (byte)Gender.Male, 0);
        handler = Game.Login.PacketHandlers[0x04];
        handler(client, (ClientPacket)createBPacket);
        Assert.Equal("\0", client.LastMessage);
        return client;
    }

    private void DeleteTestUser(string username, bool assert = false)
    {
        var result = Game.World.DeleteUser(username);
        if (assert) Assert.True(result);
    }

    [Fact]
    public void CreationOfUserShouldSucceed()
    {
        CreateTestUser("COUSS");
        DeleteTestUser("COUSS", true);
    }

    [Fact]
    public void DeletionOfUserShouldSucceed()
    {
        CreateTestUser("DOUSS");
        DeleteTestUser("DOUSS");
        Assert.False(Game.World.TryGetActiveUser("DOUSS", out _));
        Assert.False(Game.World.WorldState.TryGetUser("DOUSS", out _));
    }

    [Fact]
    public void AuthOfUserShouldSucceed()
    {
        var client = CreateTestUser("AOUSS");
        var handler = Game.Login.PacketHandlers[0x03];
        var loginPacket = new Login("AOUSS", "leethax6");
        handler(client, (ClientPacket)loginPacket);
        Assert.Equal("Welcome to Hybrasyl!", client.LastMessage);
        DeleteTestUser("AOUSS", true);
    }

    [Fact]
    public void CreationOfUserThatAlreadyExistsShouldFail()
    {
        var client = CreateTestUser("COUTAESF");
        var handler = Game.Login.PacketHandlers[0x02];
        Assert.NotNull(handler);
        var createAPacket = new CreateALogin("COUTAESF", "leethax6", "k@erd.en");
        handler(client, (ClientPacket)createAPacket);
        Assert.Equal("That name is unavailable.", client.LastMessage);
        DeleteTestUser("COUTAESF");
    }

    [Fact]
    public void CreationOfUserWithInvalidUsernameShouldFail()
    {
        using var client = Client;
        var handler = Game.Login.PacketHandlers[0x02];
        Assert.NotNull(handler);
        var createAPacket = new CreateALogin("Kerrrrrrrrrrrrrrden", "leethax6", "k@erd.en");
        handler(client, (ClientPacket)createAPacket);
        Assert.Equal("Names must be between 4 to 12 characters long.", client.LastMessage);
    }

    [Fact]
    public void CreationOfUserWithNumbersInNameShouldFail()
    {
        using var client = Client;
        var handler = Game.Login.PacketHandlers[0x02];
        Assert.NotNull(handler);
        var createAPacket = new CreateALogin("Kerden1337", "leethax6", "k@erd.en");
        handler(client, (ClientPacket)createAPacket);
        Assert.Equal("Names may only contain letters.", client.LastMessage);
    }

    [Fact]
    public void LoginOfUserToWorldShouldSucceed()
    {
        CreateTestUser("LOUTWSS");
        LoginToWorld("LOUTWSS");
        DeleteTestUser("LOUTWSS", true);
    }

    [Fact]
    public void LogoffOfUserShouldSucceedBecauseThisIsNotSwordArtOnline()
    {
        CreateTestUser("LOUSSBTINSAO");
        LoginToWorld("LOUSSBTINSAO");
        LogoutOfWorld("LOUSSBTINSAO");
        DeleteTestUser("LOUSSBTINSAO", true);
    }

    [Fact]
    public void SerializedStatusShouldReturnOnRelog()
    {
        CreateTestUser("SSSROR");
        LoginToWorld("SSSROR");
        var user = GetTestUser("SSSROR");
        Assert.NotNull(user);
        user.Stats.BaseAc = 50;
        var beforeAc = user.Stats.BaseAc;
        var castable = Game.World.WorldData.Find<Castable>(condition: x => x.Name == "TestPlusAc").FirstOrDefault();
        Assert.NotNull(castable);
        Assert.NotNull(castable.AddStatuses);
        Assert.NotEmpty(castable.AddStatuses);
        var expectedStatus = Game.World.WorldData.Get<Xml.Objects.Status>(castable.AddStatuses.First().Value);
        Assert.NotNull(expectedStatus.Effects.OnApply.StatModifiers);
        var expectedAcDelta = Convert.ToSByte(expectedStatus.Effects.OnApply.StatModifiers.BonusAc);
        var intensity = castable.AddStatuses.First().Intensity;
        user.SpellBook.Add(castable);
        Assert.True(user.UseCastable(castable, user));
        Assert.NotEmpty(user.CurrentStatuses);
        Assert.True(user.Stats.Ac == beforeAc + expectedAcDelta * intensity,
            $"ac was {beforeAc}, delta {expectedAcDelta}, should be {beforeAc + expectedAcDelta} but is {user.Stats.Ac}");
        LogoutOfWorld("SSSROR");
        LoginToWorld("SSSROR");
        var userAfterRelog = GetTestUser("SSSROR");
        Assert.NotEmpty(user.CurrentStatuses);
        LogoutOfWorld("SSSROR");
        DeleteTestUser("SSSROR");
    }
}