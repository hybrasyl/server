using System.Linq;
using Hybrasyl;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using Xunit;

namespace HybrasylTests;

[Collection("Hybrasyl")]
public class Merchants
{
    public Merchants(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    public HybrasylFixture Fixture { get; set; }

    [Fact]
    public void CheckOnDeposit()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
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
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
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
        Fixture.ResetUserStats();
        var before = Fixture.TestUser.Stats.Gold;
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
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
        var coins = item.Value * item.Count + item2.Value * item2.Count;
        Assert.Equal($"Certainly. That will be {coins} coins, TestUser.", msg.Message);
        Assert.Equal(Fixture.TestUser.Stats.Gold, before + coins);
        Assert.False(Fixture.TestUser.Inventory.ContainsName("Prayer Book"));
    }

    [Fact]
    public void BuyItem()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item junk), "Couldn't find epee in test items");
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Fixture.TestUser.AddItem(item);
        var before = Fixture.TestUser.Stats.Gold;
        Fixture.TestUser.Say("Buy 1 of my epee");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal($"Certainly. I will buy 1 of those for {item.Value} coins, {Fixture.TestUser.Name}.", msg.Message);
        Assert.Equal(Fixture.TestUser.Stats.Gold, before + item.Value);
        Assert.False(Fixture.TestUser.Inventory.ContainsName("Epee"));
    }

    [Fact]
    public void RepairAll()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
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
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
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
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
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
        Assert.Equal(Fixture.TestUser.Stats.Gold, (uint) 70000);
    }

    [Fact]
    public void DepositItem()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
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
        Assert.Equal(Fixture.TestUser.Vault.Items["Epee"], (uint) 1);
    }

    [Fact]
    public void WithdrawGold()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
        var before = Fixture.TestUser.Stats.Gold;
        Fixture.TestUser.Vault.AddGold(30000);
        Fixture.TestUser.Say("withdraw 30000 coins");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("Here are your 30000 coins.", msg.Message);
        Assert.Equal(Fixture.TestUser.Vault.CurrentGold, (uint) 0);
        Assert.Equal(Fixture.TestUser.Stats.Gold, before + 30000);
    }

    [Fact]
    public void WithdrawItem()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item junk), "Couldn't find epee in test items");
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Fixture.TestUser.AddItem(item);
        Fixture.TestUser.Vault.AddItem(item.Name);
    }

    [Fact]
    public void WithdrawStackableItem()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Teleport("XUnit Test Realm", 8, 8);
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Bent Needle", out Item junk),
            "Couldn't find bent needle in test items");
        var item = new ItemObject(junk, Fixture.TestUser.World.Guid);
        item.Count = item.MaximumStack - 1;
        Fixture.TestUser.AddItem(item);
        var item2 = new ItemObject(junk, Fixture.TestUser.World.Guid);
        Fixture.TestUser.Vault.AddItem(item2.Name, (ushort) item2.Count);
        Fixture.TestUser.Say("withdraw bent needle");
        var msg = Fixture.TestUser.MessagesReceived.Last();
        Assert.Equal("Maria", msg.Speaker.Name);
        Assert.Equal("Here's your Bent Needle back.", msg.Message);
        Assert.Equal(item.Count, item.MaximumStack);
    }
}