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

using System;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Players;
using Hybrasyl.Xml.Objects;
using System.IO;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Merchants
{
    public Merchants(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    public HybrasylFixture Fixture { get; set; }

    private Merchant GetTestMerchant()
    {
        var merchant = Fixture.Map.Objects.OfType<Merchant>().FirstOrDefault(predicate: x => x.Name == "Maria");
        Assert.NotNull(merchant);
        return merchant;
    }

    // Regression tests: merchant slot handlers must ignore (not NRE on) crafted packets
    // referencing an empty inventory slot.

    // Continuation handlers are selected by menu id from the packet; a crafted or
    // out-of-order packet can invoke them without the preceding dialog step having
    // established the pending state they rely on. All must ignore, not throw.
    [Fact]
    public void ContinuationHandlersWithoutPendingStateAreIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        Assert.Null(Record.Exception(() => Fixture.TestUser.ShowLearnSkillAgree(merchant)));
        Assert.Null(Record.Exception(() => Fixture.TestUser.ShowLearnSkillAccept(merchant)));
        Assert.Null(Record.Exception(() => Fixture.TestUser.ShowLearnSpellAgree(merchant)));
        Assert.Null(Record.Exception(() => Fixture.TestUser.ShowLearnSpellAccept(merchant)));
        Assert.Null(Record.Exception(() => Fixture.TestUser.ShowBuyItem(merchant)));
        Assert.Null(Record.Exception(() => Fixture.TestUser.ShowMerchantSendParcelAccept(merchant, "nobody")));
    }

    [Fact]
    public void SellQuantityEmptySlotIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var pendingBefore = Fixture.TestUser.PendingSellableSlot;
        var ex = Record.Exception(() => Fixture.TestUser.ShowSellQuantity(merchant, 5));
        Assert.Null(ex);
        Assert.Equal(pendingBefore, Fixture.TestUser.PendingSellableSlot);
    }

    [Fact]
    public void SellConfirmEmptySlotIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var pendingBefore = Fixture.TestUser.PendingSellableSlot;
        var goldBefore = Fixture.TestUser.Stats.Gold;
        var ex = Record.Exception(() => Fixture.TestUser.ShowSellConfirm(merchant, 5));
        Assert.Null(ex);
        Assert.Equal(pendingBefore, Fixture.TestUser.PendingSellableSlot);
        Assert.Equal(goldBefore, Fixture.TestUser.Stats.Gold);
    }

    [Fact]
    public void DepositQuantityEmptySlotIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var pendingBefore = Fixture.TestUser.PendingDepositSlot;
        var ex = Record.Exception(() => Fixture.TestUser.ShowDepositItemQuantity(merchant, 5));
        Assert.Null(ex);
        Assert.Equal(pendingBefore, Fixture.TestUser.PendingDepositSlot);
    }

    [Fact]
    public void DepositConfirmEmptySlotIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var goldBefore = Fixture.TestUser.Stats.Gold;
        var ex = Record.Exception(() => Fixture.TestUser.DepositItemConfirm(merchant, 5));
        Assert.Null(ex);
        Assert.Equal(goldBefore, Fixture.TestUser.Stats.Gold);
        Assert.Empty(Fixture.TestUser.Vault.Items);
    }

    [Fact]
    public void RepairItemEmptySlotIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var pendingBefore = Fixture.TestUser.PendingRepairSlot;
        var ex = Record.Exception(() => Fixture.TestUser.ShowRepairItem(merchant, 5));
        Assert.Null(ex);
        Assert.Equal(pendingBefore, Fixture.TestUser.PendingRepairSlot);
    }

    [Fact]
    public void RepairItemAcceptEmptiedSlotIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item epee),
            "Couldn't find epee in test items");
        var item = new ItemObject(epee, Fixture.TestUser.World.Guid);
        Assert.True(Fixture.TestUser.AddItem(item), "Couldn't add item to inventory");
        item.Durability /= 2;
        Fixture.TestUser.ShowRepairItem(merchant, 1);
        Assert.Equal(1, Fixture.TestUser.PendingRepairSlot);
        // Item vanishes between the repair offer and the accept (drop, trade, crafted packet)
        Assert.True(Fixture.TestUser.RemoveItem(1), "Couldn't remove item from inventory");
        Fixture.TestUser.Stats.Gold = 10000;
        var goldBefore = Fixture.TestUser.Stats.Gold;
        var ex = Record.Exception(() => Fixture.TestUser.ShowRepairItemAccept(merchant));
        Assert.Null(ex);
        Assert.Equal(goldBefore, Fixture.TestUser.Stats.Gold);
    }

    [Fact]
    public void RepairItemAcceptReplayIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item epee),
            "Couldn't find epee in test items");
        var item = new ItemObject(epee, Fixture.TestUser.World.Guid);
        Assert.True(Fixture.TestUser.AddItem(item), "Couldn't add item to inventory");
        item.Durability /= 2;
        Fixture.TestUser.Stats.Gold = 10000;
        Fixture.TestUser.ShowRepairItem(merchant, 1);
        Fixture.TestUser.ShowRepairItemAccept(merchant);
        Assert.Equal(item.MaximumDurability, item.Durability);
        Assert.Equal(0, Fixture.TestUser.PendingRepairSlot);
        // Replaying the accept packet with no pending repair (PendingRepairSlot == 0) must be ignored
        var goldBefore = Fixture.TestUser.Stats.Gold;
        var ex = Record.Exception(() => Fixture.TestUser.ShowRepairItemAccept(merchant));
        Assert.Null(ex);
        Assert.Equal(goldBefore, Fixture.TestUser.Stats.Gold);
    }

    [Fact]
    public void CheckOnDeposit()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Fixture.TestUser.Say("how many epee do i have on deposit");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You have none of those deposited.", msg.Message);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item epee), "Couldn't find epee in test items");
        var item = new ItemObject(epee, Fixture.TestUser.World.Guid);
        item.Durability = item.MaximumDurability - 1;
        Assert.True(Fixture.TestUser.AddItem(item), "Couldn't add item to inventory");
        // Should refuse ("I don't want your junk...")
        Fixture.TestUser.Say("deposit epee");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("I don't want your junk. Ask a smith to fix it.", msg.Message);
        item.Durability = item.MaximumDurability;
        // Should now be depositable - except we have no money
        Fixture.TestUser.Stats.Gold = 0;
        Fixture.TestUser.Say("deposit epee");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("I'll need 50 coins to deposit that.", msg.Message);
        // Now we can deposit
        Fixture.TestUser.Stats.Gold = 1000;
        Fixture.TestUser.Say("deposit epee");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("Epee, that'll be 50 coins.", msg.Message);
        // Now we should have exactly one epee
        Fixture.TestUser.Say("how many epee do i have on deposit");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You have 1 of those deposited.", msg.Message);
    }

    [Fact]
    public void CheckGoldOnDeposit()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Fixture.TestUser.Say("how much gold do i have on deposit");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You have no coins on deposit.", msg.Message);
        // Now with gold
        Fixture.TestUser.Stats.Gold = 100000;
        Fixture.TestUser.Say("deposit 30000 gold");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("I'll take your 30000 coins.", msg.Message);
        // How much we got
        Fixture.TestUser.Say("how much gold do i have on deposit");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You have 30000 coins on deposit.", msg.Message);
        // Take it out, check again
        Fixture.TestUser.Say("withdraw 29999 coins");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("Here are your 29999 coins.", msg.Message);
        Fixture.TestUser.Say("how much gold do i have on deposit");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You have 1 coin on deposit.", msg.Message);
    }

    [Fact]
    public void BuyAllCategory()
    {
        Fixture.ResetTestUserStats();
        var before = Fixture.TestUser.Stats.Gold;
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Prayer Book", out Item junk),
            "Couldn't find prayer book in test items");
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        item.Count = item.MaximumStack;
        Fixture.TestUser.AddItem(item);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Bent Needle", out Item junk2),
            "Couldn't find bent needle in test items");
        var item2 = new ItemObject(junk, Fixture.TestUser.World.Guid);
        item2.Count = item2.MaximumStack;
        Fixture.TestUser.AddItem(item2);
        Fixture.TestUser.Say("Buy all of my junk");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        // The merchant pays MerchantBuybackPercentage of value, rounded per slot as the handler does.
        var buyback = Game.ActiveConfiguration.Constants.MerchantBuybackPercentage;
        var coins = (uint) Math.Round(item.Value * item.Count * buyback) +
                    (uint) Math.Round(item2.Value * item2.Count * buyback);
        Assert.Equal($"Certainly. That will be {coins} coins, TestUser.", msg.Message);
        Assert.Equal(Fixture.TestUser.Stats.Gold, before + coins);
        Assert.False(Fixture.TestUser.Inventory.ContainsName("Prayer Book"));
    }

    [Fact]
    public void BuyItem()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item junk), "Couldn't find epee in test items");
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Fixture.TestUser.AddItem(item);
        var before = Fixture.TestUser.Stats.Gold;
        Fixture.TestUser.Say("Buy 1 of my epee");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        var coins = (uint) Math.Round(item.Value * Game.ActiveConfiguration.Constants.MerchantBuybackPercentage);
        Assert.Equal($"Certainly. I will buy 1 of those for {coins} coins, {Fixture.TestUser.Name}.", msg.Message);
        Assert.Equal(Fixture.TestUser.Stats.Gold, before + coins);
        Assert.False(Fixture.TestUser.Inventory.ContainsName("Epee"));
    }

    [Fact]
    public void RepairAll()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item junk), "Couldn't find epee in test items");
        var before = Fixture.TestUser.Stats.Gold;
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Fixture.TestUser.AddItem(item);
        var item2 = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Fixture.TestUser.AddItem(item2);
        item.Durability /= 2;
        item2.Durability /= 2;
        Fixture.TestUser.Stats.Gold = 1;
        Fixture.TestUser.Say("repair all");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You'll need 250 more gold to repair all of it, I'm afraid.", msg.Message);
        Fixture.TestUser.Stats.Gold = 10000;
        Fixture.TestUser.Say("repair all");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.True(item.Durability == item.MaximumDurability);
        Assert.True(item2.Durability == item2.MaximumDurability);
        Assert.Equal("I repaired it all for 1000 coins.", msg.Message);
    }

    [Fact]
    public void RepairItem()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item junk),
            "Couldn't find epee in very test items");
        var before = Fixture.TestUser.Stats.Gold;
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Fixture.TestUser.AddItem(item);
        item.Durability /= 2;
        Fixture.TestUser.Stats.Gold = 1;
        Fixture.TestUser.Say("repair my epee");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You'll need 250 more gold to repair that, I'm afraid.", msg.Message);
        Fixture.TestUser.Stats.Gold = 10000;
        Fixture.TestUser.Say("repair my epee");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.True(item.Durability == item.MaximumDurability);
        Assert.Equal("I repaired your Epee for 250 coins.", msg.Message);
    }

    [Fact]
    public void DepositGold()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Fixture.TestUser.Say("how much gold do i have on deposit");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You have no coins on deposit.", msg.Message);
        // Not enough gold
        Fixture.TestUser.Say("deposit 30000 gold");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You don't have that much.", msg.Message);
        Fixture.TestUser.Stats.Gold = 100000;
        Fixture.TestUser.Say("deposit 30000 gold");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("I'll take your 30000 coins.", msg.Message);
        // How much we got
        Fixture.TestUser.Say("how much gold do i have on deposit");
        msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("You have 30000 coins on deposit.", msg.Message);
        Assert.Equal((uint)70000, Fixture.TestUser.Stats.Gold);
    }

    [Fact]
    public void DepositItem()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item junk),
            "Couldn't find prayer book in test items");
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        item.Durability = item.MaximumDurability;
        Fixture.TestUser.AddItem(item);
        Fixture.TestUser.Say("deposit epee");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("Epee, that'll be 50 coins.", msg.Message);
        Assert.False(Fixture.TestUser.Inventory.ContainsName("Epee"));
        Assert.True(Fixture.TestUser.Vault.Items.ContainsKey("Epee"));
        Assert.Equal((uint)1, Fixture.TestUser.Vault.Items["Epee"]);
    }

    [Fact]
    public void WithdrawGold()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        var before = Fixture.TestUser.Stats.Gold;
        Fixture.TestUser.Vault.AddGold(30000);
        Fixture.TestUser.Say("withdraw 30000 coins");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("Here are your 30000 coins.", msg.Message);
        Assert.Equal((uint)0, Fixture.TestUser.Vault.CurrentGold);
        Assert.Equal(Fixture.TestUser.Stats.Gold, before + 30000);
    }

    // Spoken-trigger banking (MerchantController) is a separate implementation from the
    // MerchantMenuItem handlers on User; both must conserve gold.
    [Fact]
    public void SpokenPartialWithdrawalConservesVaultGold()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Fixture.TestUser.Vault.AddGold(30000);
        Fixture.TestUser.Stats.Gold = 0;

        Fixture.TestUser.Say("withdraw 10000 coins");

        Assert.Equal((uint) 20000, Fixture.TestUser.Vault.CurrentGold);
        Assert.Equal((uint) 10000, Fixture.TestUser.Stats.Gold);
    }

    // The quote validated ownership across every slot holding the item, but acceptance only ever
    // removes from the selected slot — so two stacks of one paid twice for a single removal.
    [Fact]
    public void SellingMoreThanTheSelectedSlotHoldsIsRefused()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Mouse Tail", out Item reagent),
            "Couldn't find mouse tail in test items");
        // Two separate single-item stacks of the same item.
        var first = new ItemObject(reagent, Fixture.TestUser.World.Guid) { Count = 1 };
        var second = new ItemObject(reagent, Fixture.TestUser.World.Guid) { Count = 1 };
        Assert.True(Fixture.TestUser.AddItem(first, 1), "Couldn't add first stack");
        Assert.True(Fixture.TestUser.AddItem(second, 2), "Couldn't add second stack");
        Fixture.TestUser.Stats.Gold = 0;

        // Aggregate ownership is 2, but slot 1 holds only 1.
        Fixture.TestUser.ShowSellConfirm(merchant, 1, 2);
        Fixture.TestUser.SellItemAccept(merchant);

        Assert.Equal((uint) 0, Fixture.TestUser.Stats.Gold);
        Assert.Equal(2, Fixture.TestUser.Inventory.Count);
    }

    // Parcel send took the requested quantity on trust: RemoveItem's result was discarded and the
    // requested amount was mailed regardless, so asking for more than you own duplicated it.
    [Fact]
    public void SendingAParcelForMoreThanIsOwnedIsRefused()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item epee),
            "Couldn't find epee in test items");
        var item = new ItemObject(epee, Fixture.TestUser.World.Guid);
        Assert.True(Fixture.TestUser.AddItem(item), "Couldn't add item to inventory");
        Fixture.TestUser.Stats.Gold = 100000;

        Fixture.TestUser.ShowMerchantSendParcelQuantity(merchant, item);
        Fixture.TestUser.ShowMerchantSendParcelRecipient(merchant, 2);
        Fixture.TestUser.ShowMerchantSendParcelAccept(merchant, Fixture.TestUser.Name);

        // The one epee stays put, and no parcel is created for the two that were requested.
        Assert.True(Fixture.TestUser.Inventory.ContainsName("Epee"));
        Assert.Empty(Game.World.WorldState.GetOrCreate<ParcelStore>(Fixture.TestUser.GuidReference).Items);
    }

    // The sale is priced before anything is removed, so a refusal must leave the goods intact.
    [Fact]
    public void SpokenSaleIsRefusedWithoutRemovingItemsWhenPaymentWouldNotFit()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item junk), "Couldn't find epee in test items");
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Assert.True(Fixture.TestUser.AddItem(item), "Couldn't add item to inventory");
        Fixture.TestUser.Stats.Gold = Game.ActiveConfiguration.Constants.PlayerMaxGold;

        Fixture.TestUser.Say("Buy 1 of my epee");

        Assert.True(Fixture.TestUser.Inventory.ContainsName("Epee"));
        Assert.Equal(Game.ActiveConfiguration.Constants.PlayerMaxGold, Fixture.TestUser.Stats.Gold);
    }

    // Topping up an existing stack is a separate branch of the spoken withdrawal and must
    // still debit the vault, or the item is duplicated on every repetition.
    [Fact]
    public void SpokenItemWithdrawalOntoAnExistingStackDebitsTheVault()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Mouse Tail", out Item reagent),
            "Couldn't find mouse tail in test items");
        var stack = new ItemObject(reagent, Fixture.TestUser.World.Guid) { Count = 2 };
        Assert.True(Fixture.TestUser.AddItem(stack), "Couldn't add item to inventory");
        Fixture.TestUser.Vault.AddItem(reagent.Name);

        Fixture.TestUser.Say("withdraw Mouse Tail");

        Assert.Equal(3, stack.Count);
        Assert.False(Fixture.TestUser.Vault.Items.ContainsKey(reagent.Name));
    }

    [Fact]
    public void WithdrawGoldIsRefusedWhenTheUserCannotCarryIt()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Fixture.TestUser.Vault.AddGold(30000);
        Fixture.TestUser.Stats.Gold = Game.ActiveConfiguration.Constants.PlayerMaxGold;

        Fixture.TestUser.Say("withdraw 30000 coins");

        Assert.Equal((uint)30000, Fixture.TestUser.Vault.CurrentGold);
        Assert.Equal(Game.ActiveConfiguration.Constants.PlayerMaxGold, Fixture.TestUser.Stats.Gold);
        Assert.DoesNotContain("Here are your", Fixture.TestUser.MessagesReceived.Last().Message);
    }

    [Fact]
    public void WithdrawItem()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item junk), "Couldn't find epee in test items");
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Fixture.TestUser.AddItem(item);
        Fixture.TestUser.Vault.AddItem(item.Name);
    }

    [Fact]
    public void WithdrawStackableItem()
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm - With Casting", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Bent Needle", out Item junk),
            "Couldn't find bent needle in test items");
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        item.Count = item.MaximumStack - 1;
        Fixture.TestUser.AddItem(item);
        var item2 = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Fixture.TestUser.Vault.AddItem(item2.Name, (ushort)item2.Count);
        Fixture.TestUser.Say("withdraw Bent Needle");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("Here's your Bent Needle back.", msg.Message);
        Assert.Equal(item.Count, item.MaximumStack);
    }

    // Learn-flow continuation state is consumed atomically by the accept handlers and is
    // bound to the merchant and flow (skill/spell) that established it. Replayed,
    // cross-flow, and cross-merchant accept packets must all be ignored.

    private Castable GetLearnable(string name)
    {
        Assert.True(Game.World.WorldData.TryGetValueByIndex(name, out Castable castable),
            $"Couldn't find {name} in test castables");
        return castable;
    }

    private void ForgetIfKnown(Casting.Book book, Castable castable)
    {
        if (book.Contains(castable.Id))
            Assert.True(book.Remove(book.SlotOf(castable.Id)), $"Couldn't remove {castable.Name}");
    }

    [Fact]
    public void LearnPrerequisitesArePopulatedFromWorldData()
    {
        // Pins the wire surface: Prerequisites carried maxOccurs="0" in the schema
        // for years and only worked because the old generator ignored occurrence
        // constraints. If this fails, prerequisite data is not surviving XML load.
        var skill = GetLearnable("TestPrerequisite");
        var req = skill.Requirements.FirstOrDefault(x => x.Prerequisites != null);
        Assert.NotNull(req);
        var prereq = Assert.Single(req.Prerequisites.Prerequisite);
        Assert.Equal("TestNoCookie", prereq.Value);
        Assert.Equal(0, prereq.Level);
    }

    [Fact]
    public void LearnSkillAcceptReplayIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var user = Fixture.TestUser;
        var skill = GetLearnable("TestNoCookie");
        ForgetIfKnown(user.SkillBook, skill);
        user.ShowLearnSkillDisagree(merchant);
        user.Stats.Gold = 10000;

        user.ShowLearnSkill(merchant, skill);
        user.ShowLearnSkillAccept(merchant);
        Assert.True(user.SkillBook.Contains(skill.Id), "Skill was not learned");
        Assert.Equal(10000u - 1234u, user.Stats.Gold);

        // Replaying the accept must not deduct requirements again or duplicate the entry
        var ex = Record.Exception(() => user.ShowLearnSkillAccept(merchant));
        Assert.Null(ex);
        Assert.Equal(10000u - 1234u, user.Stats.Gold);
        Assert.Equal(1, user.SkillBook.Count(x => x.Castable.Id == skill.Id));
    }

    [Fact]
    public void LearnSkillAcceptFromSpellFlowIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var user = Fixture.TestUser;
        var spell = GetLearnable("TestAddBlind");
        ForgetIfKnown(user.SpellBook, spell);
        user.ShowLearnSpellDisagree(merchant);
        user.Stats.Gold = 10000;

        // Select a spell, then dispatch the skill accept: the spell must not enter the
        // skill book and nothing may be deducted
        user.ShowLearnSpell(merchant, spell);
        var ex = Record.Exception(() => user.ShowLearnSkillAccept(merchant));
        Assert.Null(ex);
        Assert.False(user.SkillBook.Contains(spell.Id), "Spell was placed in the skill book");
        Assert.Equal(10000u, user.Stats.Gold);

        // The legitimate spell accept still works afterward
        user.ShowLearnSpellAccept(merchant);
        Assert.True(user.SpellBook.Contains(spell.Id), "Spell was not learned");
        Assert.Equal(10000u - 1234u, user.Stats.Gold);
    }

    [Fact]
    public void LearnSkillAcceptFromOtherMerchantIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var otherMap = Game.World.WorldState.Get<MapObject>("40001") ??
                       throw new InvalidDataException("Fixture map 40001 missing from world data");
        var otherMerchant = otherMap.Objects.OfType<Merchant>().FirstOrDefault(x => x.Name == "Maria");
        Assert.NotNull(otherMerchant);
        Assert.NotEqual(merchant.Id, otherMerchant.Id);
        var user = Fixture.TestUser;
        var skill = GetLearnable("TestNoCookie");
        ForgetIfKnown(user.SkillBook, skill);
        user.ShowLearnSkillDisagree(merchant);
        user.Stats.Gold = 10000;

        // Pending state was established at one merchant; an accept naming another must be ignored
        user.ShowLearnSkill(merchant, skill);
        var ex = Record.Exception(() => user.ShowLearnSkillAccept(otherMerchant));
        Assert.Null(ex);
        Assert.False(user.SkillBook.Contains(skill.Id), "Skill was learned through the wrong merchant");
        Assert.Equal(10000u, user.Stats.Gold);
    }

    [Fact]
    public void LearnSkillAcceptAlreadyKnownIsIgnored()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var user = Fixture.TestUser;
        var skill = GetLearnable("TestNoCookie");
        ForgetIfKnown(user.SkillBook, skill);
        user.ShowLearnSkillDisagree(merchant);
        user.Stats.Gold = 10000;

        user.ShowLearnSkill(merchant, skill);
        user.ShowLearnSkillAccept(merchant);
        Assert.True(user.SkillBook.Contains(skill.Id), "Skill was not learned");

        // A second full flow for an already-known castable must not deduct or duplicate
        user.ShowLearnSkill(merchant, skill);
        var goldBefore = user.Stats.Gold;
        var ex = Record.Exception(() => user.ShowLearnSkillAccept(merchant));
        Assert.Null(ex);
        Assert.Equal(goldBefore, user.Stats.Gold);
        Assert.Equal(1, user.SkillBook.Count(x => x.Castable.Id == skill.Id));
    }

}