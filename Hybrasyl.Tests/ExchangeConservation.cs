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
using Hybrasyl.Subsystems.Players;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests;

/// <summary>
///     Gold staged in an exchange survives a recipient who cannot receive it.
/// </summary>
/// <remarks>
///     Staging debits the giver immediately, so the offer lives in the exchange rather than in
///     either purse. If the credit at settlement is refused — the recipient is at
///     <c>PlayerMaxGold</c> — and its result is discarded, the gold exists nowhere afterwards.
///     Totals are therefore taken before staging, because a sum of both purses taken afterwards
///     cannot see what escrow is holding.
/// </remarks>
[Collection("Hybrasyl")]
public class ExchangeConservation(HybrasylFixture fixture)
{
    public HybrasylFixture Fixture { get; set; } = fixture;

    private ulong TotalGold => (ulong) Fixture.TestUser.Stats.Gold + Fixture.SecondTestUser.Stats.Gold;

    [Fact]
    public void PerformExchange_ConservesGold_WhenTheRecipientIsAtMaxGold()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        Fixture.TestUser.Stats.Gold = Game.ActiveConfiguration.Constants.PlayerMaxGold;
        Fixture.SecondTestUser.Stats.Gold = 100;

        var before = TotalGold;

        var exchange = new Exchange(Fixture.TestUser, Fixture.SecondTestUser);
        Assert.True(exchange.AddGold(Fixture.SecondTestUser, 100), "precondition: the offer is staged");
        Assert.Equal(0u, Fixture.SecondTestUser.Stats.Gold);

        exchange.PerformExchange();

        Assert.Equal(before, TotalGold);
        Assert.Equal(100u, Fixture.SecondTestUser.Stats.Gold);
    }

    /// <summary>
    ///     A cancelled exchange hands staged gold back.
    /// </summary>
    [Fact]
    public void CancelExchange_ReturnsStagedGoldToItsOwner()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        Fixture.TestUser.Stats.Gold = 100;
        Fixture.SecondTestUser.Stats.Gold = 100;

        var before = TotalGold;

        var exchange = new Exchange(Fixture.TestUser, Fixture.SecondTestUser);
        Assert.True(exchange.AddGold(Fixture.TestUser, 100), "precondition: the offer is staged");

        exchange.CancelExchange(Fixture.TestUser);

        Assert.Equal(before, TotalGold);
        Assert.Equal(100u, Fixture.TestUser.Stats.Gold);
    }

    /// <summary>
    ///     The owner's own credit can be refused too: nothing stops a player filling their purse
    ///     while the exchange window is open, so "give it back to whoever staged it" is not a
    ///     terminal case. The gold lands at their feet rather than ceasing to exist.
    /// </summary>
    [Fact]
    public void CancelExchange_ConservesGold_WhenTheOwnersPurseRefilledDuringTheExchange()
    {
        Fixture.ResetTestUserStats();
        Fixture.ResetSecondTestUserStats();
        Fixture.TestUser.Stats.Gold = 100;
        Fixture.SecondTestUser.Stats.Gold = 0;

        var exchange = new Exchange(Fixture.TestUser, Fixture.SecondTestUser);
        Assert.True(exchange.AddGold(Fixture.TestUser, 100), "precondition: the offer is staged");

        Fixture.TestUser.Stats.Gold = Game.ActiveConfiguration.Constants.PlayerMaxGold;
        var goldOnMapBefore = Fixture.TestUser.Location.Map!.Objects.OfType<Gold>()
            .Sum(selector: x => (long) x.Amount);

        exchange.CancelExchange(Fixture.TestUser);

        var goldOnMapAfter = Fixture.TestUser.Location.Map!.Objects.OfType<Gold>()
            .Sum(selector: x => (long) x.Amount);

        Assert.Equal(Game.ActiveConfiguration.Constants.PlayerMaxGold, Fixture.TestUser.Stats.Gold);
        Assert.Equal(goldOnMapBefore + 100, goldOnMapAfter);
    }
}
