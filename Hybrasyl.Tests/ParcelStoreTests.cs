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

using Hybrasyl.Subsystems.Players;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class ParcelStoreTests(HybrasylFixture fixture)
{
    public HybrasylFixture Fixture { get; set; } = fixture;

    [Fact]
    public void RemoveGoldConsumesTheMoneygramNotAParcel()
    {
        var user = Fixture.CreateUser("MoneygramUser");
        var store = new ParcelStore(user.Guid);
        store.AddItem("Sender", "Test Item");
        store.AddGold("Sender", 5000);
        var goldBefore = user.Stats.Gold;

        store.RemoveGold(user);

        Assert.Equal(goldBefore + 5000u, user.Stats.Gold);
        Assert.Empty(store.Gold);
        Assert.Single(store.Items);
    }

    [Fact]
    public void RemoveGoldWithNoMoneygramsIsANoOp()
    {
        var user = Fixture.CreateUser("NoMoneygramUser");
        var store = new ParcelStore(user.Guid);
        var goldBefore = user.Stats.Gold;

        store.RemoveGold(user); // must not throw on an empty gold list

        Assert.Equal(goldBefore, user.Stats.Gold);
    }
}
