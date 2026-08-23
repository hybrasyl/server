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

using Hybrasyl.Subsystems.Players;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class ExchangeTests(HybrasylFixture fixture)
{
    public HybrasylFixture Fixture { get; set; } = fixture;

    [Fact]
    public void AddGold_IsRefused_WhenTargetLacksTheGold()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        Fixture.TestUser.Stats.Gold = 10;
        Fixture.SecondTestUser.Stats.Gold = 5;

        var exchange = new Exchange(Fixture.TestUser, Fixture.SecondTestUser);
        var accepted = exchange.AddGold(Fixture.SecondTestUser, 100);

        Assert.False(accepted);
        Assert.Equal(5u, Fixture.SecondTestUser.Stats.Gold);
    }

    [Fact]
    public void AddGold_IsRefused_WhenSourceLacksTheGold()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        Fixture.TestUser.Stats.Gold = 10;
        Fixture.SecondTestUser.Stats.Gold = 5;

        var exchange = new Exchange(Fixture.TestUser, Fixture.SecondTestUser);
        var accepted = exchange.AddGold(Fixture.TestUser, 100);

        Assert.False(accepted);
        Assert.Equal(10u, Fixture.TestUser.Stats.Gold);
    }

    [Fact]
    public void AddGold_IsAccepted_AndDebitsTheGiver_WhenAffordable()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        Fixture.TestUser.Stats.Gold = 10;
        Fixture.SecondTestUser.Stats.Gold = 100;

        var exchange = new Exchange(Fixture.TestUser, Fixture.SecondTestUser);

        Assert.True(exchange.AddGold(Fixture.SecondTestUser, 60));
        Assert.Equal(40u, Fixture.SecondTestUser.Stats.Gold);
        Assert.True(exchange.AddGold(Fixture.TestUser, 10));
        Assert.Equal(0u, Fixture.TestUser.Stats.Gold);
    }
}
